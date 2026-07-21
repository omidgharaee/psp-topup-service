using PSP.TopupService.Domain.Topups.Entities;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.Events;
using PSP.TopupService.Domain.Topups.Exceptions;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;
using PSP.TopupService.SharedKernel.Results;

namespace PSP.TopupService.Domain.Topups;

/// <summary>
/// Aggregate root modelling a single mobile topup transaction. Owns the
/// lifecycle, all state transitions, invariants and the events that drive the
/// outbox-driven asynchronous flow with the Bank and the mobile operator.
/// </summary>
/// <remarks>
/// All state changes MUST go through the behaviour methods. No public setters
/// exist — encapsulation enforces the invariants at compile time.
/// </remarks>
public sealed class TopupTransaction : AggregateRoot<Guid>
{
    private readonly List<TopupAttempt> _attempts = [];

    private TopupTransaction()
    {
        // EF Core constructor — required for materialisation, never used in code.
        MobileNumber = null!;
        Amount = null!;
    }

    private TopupTransaction(
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId,
        string createdBy)
        : base(Guid.NewGuid())
    {
        MobileNumber = mobileNumber;
        Amount = amount;
        CorrelationId = correlationId;
        Status = TopupStatus.Pending;
        CreatedBy = createdBy;
        FailureReason = TopupFailureReason.None;
        Version = 1;
        RaiseEvent(new TopupCreatedEvent(Id, mobileNumber, amount, correlationId));
    }

    public MobileNumber MobileNumber { get; private set; }

    public Money Amount { get; private set; }

    public TopupStatus Status { get; private set; }

    public TopupFailureReason FailureReason { get; private set; }

    /// <summary>
    /// Correlation id propagated end-to-end (API → outbox → broker → consumer → external calls).
    /// </summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Reference assigned by the Bank once the payment is initiated.</summary>
    public TransactionReference? BankReference { get; private set; }

    /// <summary>Reference returned by the mobile operator on a successful topup.</summary>
    public TransactionReference? MciReference { get; private set; }

    /// <summary>UTC instant at which the topup reached a terminal state (Completed/Failed/Reversed).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Free-text reason captured on terminal failure, for operators.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>Optimistic-concurrency version (EF Core maps to xmin/rowversion).</summary>
    public int Version { get; private set; }

    /// <summary>Idempotency key supplied by the API client; identical requests collapse to one transaction.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Reverse record, present only when the payment has been reversed.</summary>
    public ReverseRecord? Reverse { get; private set; }

    /// <summary>The set of attempts against the mobile operator, in chronological order.</summary>
    public IReadOnlyCollection<TopupAttempt> Attempts => _attempts.AsReadOnly();

    /// <summary>Number of attempts recorded so far.</summary>
    public int AttemptCount => _attempts.Count;

    /// <summary>True when the aggregate has reached any terminal state.</summary>
    public bool IsTerminal => Status is TopupStatus.Completed or TopupStatus.Failed or TopupStatus.Reversed;

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a brand-new topup transaction in the <see cref="TopupStatus.Pending"/> state.
    /// </summary>
    public static TopupTransaction Create(
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId,
        string createdBy,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(mobileNumber);
        ArgumentNullException.ThrowIfNull(amount);

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(correlationId));
        }

        var transaction = new TopupTransaction(mobileNumber, amount, correlationId, createdBy);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            transaction.IdempotencyKey = idempotencyKey.Trim();
        }

        return transaction;
    }

    // -----------------------------------------------------------------------
    // State transitions — Payment phase
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marks the payment as in-progress once the Bank reference has been obtained
    /// and the request forwarded to the Bank gateway.
    /// </summary>
    public Result MarkPaymentInitiated(TransactionReference bankReference)
    {
        ArgumentNullException.ThrowIfNull(bankReference);

        if (Status != TopupStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotPending",
                $"Payment can only be initiated from Pending (current: {Status})."));
        }

        BankReference = bankReference;
        TransitionTo(TopupStatus.PaymentInProgress);
        RaiseEvent(new PaymentInitiatedEvent(Id, bankReference, CorrelationId));
        return Result.Success();
    }

    /// <summary>
    /// Marks the payment as confirmed by the Bank. Allowed from
    /// <see cref="TopupStatus.PaymentInProgress"/> only.
    /// </summary>
    public Result MarkPaymentCompleted(TransactionReference bankReference, DateTime confirmedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bankReference);

        if (Status != TopupStatus.PaymentInProgress)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotAwaitingPayment",
                $"Payment completion requires PaymentInProgress state (current: {Status})."));
        }

        BankReference = bankReference;
        TransitionTo(TopupStatus.PaymentCompleted);
        RaiseEvent(new PaymentCompletedEvent(Id, bankReference, confirmedAtUtc, CorrelationId));
        return Result.Success();
    }

    /// <summary>
    /// Marks the payment as failed terminally (Bank declined). Allowed from
    /// <see cref="TopupStatus.Pending"/> or <see cref="TopupStatus.PaymentInProgress"/>.
    /// No reversal is required because the payment never settled.
    /// </summary>
    public Result MarkPaymentFailed(TopupFailureReason reason, string message)
    {
        if (Status != TopupStatus.Pending && Status != TopupStatus.PaymentInProgress)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotAwaitingPaymentFailure",
                $"Payment failure can only be applied before completion (current: {Status})."));
        }

        Fail(reason, message);
        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // State transitions — Topup (mobile operator) phase
    // -----------------------------------------------------------------------

    /// <summary>
    /// Begins a new topup attempt against the mobile operator. Returns the
    /// attempt so the caller can later mark it succeeded or failed.
    /// </summary>
    public Result<TopupAttempt> StartTopupAttempt()
    {
        if (Status != TopupStatus.PaymentCompleted && Status != TopupStatus.TopupInProgress)
        {
            return Result.Failure<TopupAttempt>(Error.Conflict(
                "Topup.CannotStartTopup",
                $"Topup can only start after payment is completed (current: {Status})."));
        }

        if (Status == TopupStatus.PaymentCompleted)
        {
            TransitionTo(TopupStatus.TopupInProgress);
        }

        var attempt = new TopupAttempt(Id, _attempts.Count + 1);
        _attempts.Add(attempt);
        RaiseEvent(new TopupAttemptStartedEvent(Id, attempt.AttemptNumber, CorrelationId));
        return Result.Success(attempt);
    }

    /// <summary>
    /// Marks the mobile topup as successful and records the operator reference.
    /// Does NOT complete the transaction: it moves to <see cref="TopupStatus.AdvicePending"/>,
    /// awaiting the Bank Advice (finalization) call. The transaction only
    /// becomes <see cref="TopupStatus.Completed"/> once the advice succeeds.
    /// Allowed from <see cref="TopupStatus.TopupInProgress"/> only.
    /// </summary>
    public Result MarkTopupSucceeded(TransactionReference mciReference, DateTime succeededAtUtc)
    {
        ArgumentNullException.ThrowIfNull(mciReference);

        if (Status != TopupStatus.TopupInProgress)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotInProgress",
                $"Topup success requires TopupInProgress state (current: {Status})."));
        }

        if (_attempts.Count == 0 || _attempts.All(a => a.Status != TopupAttemptStatus.Succeeded))
        {
            return Result.Failure(Error.Failure(
                "Topup.NoSuccessfulAttempt",
                "Cannot mark topup succeeded: no successful attempt has been recorded."));
        }

        MciReference = mciReference;
        TransitionTo(TopupStatus.AdvicePending);
        RaiseEvent(new TopupCompletedEvent(Id, mciReference, succeededAtUtc, CorrelationId));
        return Result.Success();
    }

    /// <summary>
    /// Records that the Bank Advice (finalization) succeeded. Transitions the
    /// aggregate to its terminal <see cref="TopupStatus.Completed"/> state.
    /// Allowed from <see cref="TopupStatus.AdvicePending"/> only.
    /// </summary>
    public Result MarkAdviceCompleted(TransactionReference adviceReference, DateTime completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(adviceReference);

        if (Status != TopupStatus.AdvicePending)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotAwaitingAdvice",
                $"Advice completion requires AdvicePending state (current: {Status})."));
        }

        BankReference = adviceReference;
        CompletedAtUtc = completedAtUtc;
        AdviceRetryCount = 0;
        TransitionTo(TopupStatus.Completed);
        return Result.Success();
    }

    /// <summary>
    /// Records a failed advice attempt and schedules the next retry (or flags
    /// terminal advice failure when <paramref name="maxRetries"/> is reached).
    /// Allowed from <see cref="TopupStatus.AdvicePending"/> only.
    /// </summary>
    public Result MarkAdviceAttemptFailed(string message, int maxRetries)
    {
        if (Status != TopupStatus.AdvicePending)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotAwaitingAdvice",
                $"Advice failure requires AdvicePending state (current: {Status})."));
        }

        AdviceRetryCount++;
        AdviceLastError = message;

        if (AdviceRetryCount >= maxRetries)
        {
            FailureReason = TopupFailureReason.AdviceFailed;
            FailureMessage = $"Bank advice failed terminally after {AdviceRetryCount} attempts: {message}";
        }

        return Result.Success();
    }

    /// <summary>Number of advice attempts recorded so far.</summary>
    public int AdviceRetryCount { get; private set; }

    /// <summary>Last error message captured during an advice attempt.</summary>
    public string? AdviceLastError { get; private set; }

    /// <summary>
    /// Marks the topup as terminally failed after all retries were exhausted.
    /// The caller is expected to follow this with <see cref="InitiateReversal"/>.
    /// Allowed from <see cref="TopupStatus.TopupInProgress"/> only.
    /// </summary>
    public Result MarkTopupFailed(TopupFailureReason reason, string message)
    {
        if (Status != TopupStatus.TopupInProgress)
        {
            return Result.Failure(Error.Conflict(
                "Topup.NotInProgressForFailure",
                $"Topup failure requires TopupInProgress state (current: {Status})."));
        }

        FailureReason = reason;
        FailureMessage = message;
        RaiseEvent(new TopupFailedEvent(Id, reason, message, CorrelationId));
        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // State transitions — Reversal phase
    // -----------------------------------------------------------------------

    /// <summary>
    /// Initiates a reversal of the settled payment after a terminal topup failure.
    /// Allowed from <see cref="TopupStatus.TopupInProgress"/> (post-failure) only.
    /// </summary>
    public Result InitiateReversal(string requestedBy)
    {
        if (Status != TopupStatus.TopupInProgress)
        {
            return Result.Failure(Error.Conflict(
                "Topup.CannotReverse",
                $"Reversal can only be initiated from TopupInProgress after failure (current: {Status})."));
        }

        if (Reverse is not null)
        {
            return Result.Failure(Error.Conflict(
                "Topup.AlreadyReversing",
                "A reversal is already in progress for this transaction."));
        }

        Reverse = new ReverseRecord(Id, FailureReason, requestedBy);
        Touch();
        return Result.Success();
    }

    /// <summary>
    /// Records that the Bank has completed the reversal (refund settled).
    /// Transitions the aggregate to the terminal <see cref="TopupStatus.Reversed"/> state.
    /// </summary>
    public Result MarkPaymentReversed(TransactionReference bankReversalReference, DateTime reversedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bankReversalReference);

        if (Reverse is null)
        {
            return Result.Failure(Error.Failure(
                "Topup.NoReversalInProgress",
                "Cannot mark payment reversed: no reversal was initiated."));
        }

        Reverse.MarkCompleted(bankReversalReference);
        CompletedAtUtc = reversedAtUtc;
        TransitionTo(TopupStatus.Reversed);
        RaiseEvent(new PaymentReversedEvent(Id, bankReversalReference, FailureReason, reversedAtUtc, CorrelationId));
        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void TransitionTo(TopupStatus next)
    {
        Status = next;
        Version++;
        Touch();
    }

    private void Fail(TopupFailureReason reason, string message)
    {
        FailureReason = reason;
        FailureMessage = message;
        CompletedAtUtc = DateTime.UtcNow;
        TransitionTo(TopupStatus.Failed);
        RaiseEvent(new TopupFailedEvent(Id, reason, message, CorrelationId));
    }
}
