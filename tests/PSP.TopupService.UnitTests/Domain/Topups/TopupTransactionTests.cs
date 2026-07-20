using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.Events;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.UnitTests.Domain.Topups;

/// <summary>
/// Verifies the <see cref="TopupTransaction"/> aggregate state machine: legal
/// transitions, illegal-transition rejection, event raising and invariants.
/// This is the most important test suite in the codebase — the aggregate is
/// the source of truth for every payment.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
public class TopupTransactionTests
{
    private static readonly MobileNumber Mobile = MobileNumber.Create("09121234567");
    private static readonly Money Amount = Money.Create(50_000);

    // -----------------------------------------------------------------------
    // Creation
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Should_Produce_Pending_Transaction_With_TopupCreatedEvent()
    {
        var correlation = Guid.NewGuid();

        var tx = TopupTransaction.Create(Mobile, Amount, correlation, "client-1", "idem-1");

        tx.Id.Should().NotBeEmpty();
        tx.Status.Should().Be(TopupStatus.Pending);
        tx.MobileNumber.Should().Be(Mobile);
        tx.Amount.Should().Be(Amount);
        tx.CorrelationId.Should().Be(correlation);
        tx.IsTerminal.Should().BeFalse();
        tx.AttemptCount.Should().Be(0);
        tx.IdempotencyKey.Should().Be("idem-1");
        tx.Version.Should().Be(1);
        tx.DomainEvents.OfType<TopupCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_Should_Throw_When_CorrelationId_Empty()
    {
        var act = () => TopupTransaction.Create(Mobile, Amount, Guid.Empty, "client");
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // Payment phase
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkPaymentInitiated_Should_Transition_To_PaymentInProgress()
    {
        var tx = NewTransaction();
        var bankRef = TransactionReference.Generate("BANK");

        var result = tx.MarkPaymentInitiated(bankRef);

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.PaymentInProgress);
        tx.BankReference.Should().Be(bankRef);
        tx.DomainEvents.OfType<PaymentInitiatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkPaymentInitiated_Should_Fail_When_Not_Pending()
    {
        var tx = NewTransaction();
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));

        var second = tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Topup.NotPending");
    }

    [Fact]
    public void MarkPaymentCompleted_Should_Transition_To_PaymentCompleted()
    {
        var tx = NewTransaction();
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        var confirmed = DateTime.UtcNow;

        var result = tx.MarkPaymentCompleted(tx.BankReference!, confirmed);

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.PaymentCompleted);
        tx.DomainEvents.OfType<PaymentCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkPaymentCompleted_Should_Fail_When_Not_InProgress()
    {
        var tx = NewTransaction();

        var result = tx.MarkPaymentCompleted(TransactionReference.Generate("BANK"), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.NotAwaitingPayment");
    }

    [Fact]
    public void MarkPaymentFailed_Should_Transition_To_Failed_From_Pending()
    {
        var tx = NewTransaction();

        var result = tx.MarkPaymentFailed(TopupFailureReason.PaymentDeclined, "Bank declined");

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.Failed);
        tx.FailureReason.Should().Be(TopupFailureReason.PaymentDeclined);
        tx.FailureMessage.Should().Be("Bank declined");
        tx.IsTerminal.Should().BeTrue();
        tx.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkPaymentFailed_Should_Fail_After_Payment_Completed()
    {
        var tx = NewTransaction();
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);

        var result = tx.MarkPaymentFailed(TopupFailureReason.PaymentDeclined, "late decline");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.NotAwaitingPaymentFailure");
    }

    // -----------------------------------------------------------------------
    // Topup phase
    // -----------------------------------------------------------------------

    [Fact]
    public void StartTopupAttempt_Should_Transition_To_TopupInProgress_And_Record_Attempt()
    {
        var tx = PaidTransaction();

        var result = tx.StartTopupAttempt();

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.TopupInProgress);
        tx.AttemptCount.Should().Be(1);
        var attempt = tx.Attempts.Single();
        attempt.AttemptNumber.Should().Be(1);
        attempt.TopupId.Should().Be(tx.Id);
        tx.DomainEvents.OfType<TopupAttemptStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void StartTopupAttempt_Should_Allow_Multiple_Attempts_When_InProgress()
    {
        var tx = PaidTransaction();

        tx.StartTopupAttempt();
        var second = tx.StartTopupAttempt();

        second.IsSuccess.Should().BeTrue();
        tx.AttemptCount.Should().Be(2);
        tx.Attempts.Last().AttemptNumber.Should().Be(2);
    }

    [Fact]
    public void StartTopupAttempt_Should_Fail_When_Payment_Not_Completed()
    {
        var tx = NewTransaction();

        var result = tx.StartTopupAttempt();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.CannotStartTopup");
    }

    [Fact]
    public void MarkTopupSucceeded_Should_Reach_AdvicePending_After_Successful_Attempt()
    {
        var tx = PaidTransaction();
        var attemptResult = tx.StartTopupAttempt();
        attemptResult.Value!.MarkSucceeded("MCI-REF-123");
        var mciRef = TransactionReference.Create("MCI-REF-123", "MCI");

        var result = tx.MarkTopupSucceeded(mciRef, DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.AdvicePending);
        tx.MciReference.Should().Be(mciRef);
        tx.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void MarkTopupSucceeded_Should_Fail_When_No_Successful_Attempt()
    {
        var tx = PaidTransaction();
        tx.StartTopupAttempt();
        var mciRef = TransactionReference.Create("MCI-REF-123", "MCI");

        var result = tx.MarkTopupSucceeded(mciRef, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.NoSuccessfulAttempt");
    }

    [Fact]
    public void MarkAdviceCompleted_Should_Reach_Terminal_Completed_From_AdvicePending()
    {
        var tx = PaidTransaction();
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);
        var adviceRef = TransactionReference.Create("ADV-1", "ADVICE");

        var result = tx.MarkAdviceCompleted(adviceRef, DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.Completed);
        tx.IsTerminal.Should().BeTrue();
        tx.BankReference.Should().Be(adviceRef);
    }

    [Fact]
    public void MarkAdviceCompleted_Should_Fail_When_Not_In_AdvicePending()
    {
        var tx = PaidTransaction();
        var adviceRef = TransactionReference.Create("ADV-1", "ADVICE");

        var result = tx.MarkAdviceCompleted(adviceRef, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.NotAwaitingAdvice");
    }

    [Fact]
    public void MarkAdviceAttemptFailed_Should_Flag_Terminal_When_Max_Retries_Reached()
    {
        var tx = PaidTransaction();
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);

        var result = tx.MarkAdviceAttemptFailed("bank down", maxRetries: 1);

        result.IsSuccess.Should().BeTrue();
        tx.AdviceRetryCount.Should().Be(1);
        tx.FailureReason.Should().Be(TopupFailureReason.AdviceFailed);
        tx.FailureMessage.Should().Contain("Bank advice failed terminally");
    }

    [Fact]
    public void MarkAdviceAttemptFailed_Should_Not_Flag_Terminal_Below_Max_Retries()
    {
        var tx = PaidTransaction();
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);

        var result = tx.MarkAdviceAttemptFailed("bank down", maxRetries: 5);

        result.IsSuccess.Should().BeTrue();
        tx.AdviceRetryCount.Should().Be(1);
        tx.FailureReason.Should().Be(TopupFailureReason.None);
    }

    [Fact]
    public void MarkTopupFailed_Should_Record_Failure_Without_Terminal_Transition()
    {
        var tx = PaidTransaction();
        tx.StartTopupAttempt();

        var result = tx.MarkTopupFailed(TopupFailureReason.TopupProviderError, "all retries exhausted");

        result.IsSuccess.Should().BeTrue();
        tx.FailureReason.Should().Be(TopupFailureReason.TopupProviderError);
        tx.FailureMessage.Should().Be("all retries exhausted");
        tx.Status.Should().Be(TopupStatus.TopupInProgress); // still in progress — reversal pending
        tx.DomainEvents.OfType<TopupFailedEvent>().Should().ContainSingle();
    }

    // -----------------------------------------------------------------------
    // Reversal phase
    // -----------------------------------------------------------------------

    [Fact]
    public void InitiateReversal_After_MarkTopupFailed_Then_Complete_Reversal_Should_Be_Reversed()
    {
        var tx = PaidTransaction();
        tx.StartTopupAttempt();
        tx.MarkTopupFailed(TopupFailureReason.TopupProviderError, "exhausted");

        var init = tx.InitiateReversal("reconciliation-bot");
        var reverse = tx.MarkPaymentReversed(TransactionReference.Generate("REVERSAL"), DateTime.UtcNow);

        init.IsSuccess.Should().BeTrue();
        reverse.IsSuccess.Should().BeTrue();
        tx.Status.Should().Be(TopupStatus.Reversed);
        tx.IsTerminal.Should().BeTrue();
        tx.Reverse.Should().NotBeNull();
        tx.Reverse!.Status.Should().Be(ReverseStatus.Completed);
        tx.DomainEvents.OfType<PaymentReversedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void InitiateReversal_Should_Fail_When_Not_In_TopupInProgress()
    {
        var tx = PaidTransaction();

        var result = tx.InitiateReversal("bot");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.CannotReverse");
    }

    [Fact]
    public void InitiateReversal_Should_Fail_Twice()
    {
        var tx = PaidTransaction();
        tx.StartTopupAttempt();
        tx.MarkTopupFailed(TopupFailureReason.TopupProviderError, "x");
        tx.InitiateReversal("bot");

        var second = tx.InitiateReversal("bot");

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Topup.AlreadyReversing");
    }

    [Fact]
    public void MarkPaymentReversed_Should_Fail_When_No_Reversal_Started()
    {
        var tx = PaidTransaction();

        var result = tx.MarkPaymentReversed(TransactionReference.Generate("REVERSAL"), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Topup.NoReversalInProgress");
    }

    // -----------------------------------------------------------------------
    // Full happy-path scenario
    // -----------------------------------------------------------------------

    [Fact]
    public void Full_Happy_Path_Should_Produce_All_Expected_Domain_Events_In_Order()
    {
        var tx = TopupTransaction.Create(Mobile, Amount, Guid.NewGuid(), "client");
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);
        tx.MarkAdviceCompleted(TransactionReference.Create("ADV-1", "ADVICE"), DateTime.UtcNow);

        var eventTypes = tx.DomainEvents.Select(e => e.GetType()).ToArray();
        eventTypes.Should().Equal(
            typeof(TopupCreatedEvent),
            typeof(PaymentInitiatedEvent),
            typeof(PaymentCompletedEvent),
            typeof(TopupAttemptStartedEvent),
            typeof(TopupCompletedEvent));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static TopupTransaction NewTransaction() =>
        TopupTransaction.Create(Mobile, Amount, Guid.NewGuid(), "client");

    private static TopupTransaction PaidTransaction()
    {
        var tx = NewTransaction();
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        return tx;
    }
}
