namespace PSP.TopupService.Domain.Topups.Enums;

/// <summary>
/// Lifecycle of a topup transaction. Transitions are guarded by the
/// <see cref="Topups.TopupTransaction"/> aggregate; illegal transitions raise
/// <c>InvalidTopupStateException</c>.
/// </summary>
public enum TopupStatus
{
    /// <summary>
    /// Transaction created and persisted. The outbox event requesting payment
    /// is about to be (or has just been) published. No payment has been processed yet.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The payment request has been forwarded to the Bank gateway and we are
    /// waiting for the asynchronous <c>PaymentCompleted</c> event.
    /// </summary>
    PaymentInProgress = 2,

    /// <summary>
    /// The Bank has confirmed the payment was successful; the topup call to the
    /// mobile operator (Hamrah-e-Aval) has not yet been attempted.
    /// </summary>
    PaymentCompleted = 3,

    /// <summary>
    /// The topup call to the mobile operator is in progress (possibly mid-retry).
    /// </summary>
    TopupInProgress = 4,

    /// <summary>
    /// The mobile operator confirmed the topup, but the Bank Advice
    /// (finalization) call has not yet succeeded. The transaction can only
    /// reach <see cref="Completed"/> once the Bank acknowledges the advice.
    /// </summary>
    AdvicePending = 5,

    /// <summary>
    /// The Bank Advice succeeded. Terminal success state.
    /// </summary>
    Completed = 6,

    /// <summary>
    /// A non-recoverable failure occurred and (if applicable) the payment was
    /// reversed. Terminal failure state.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// The payment was reversed (refund completed) after an unrecoverable topup
    /// failure. Terminal failure state with explicit reversal.
    /// </summary>
    Reversed = 8,
}
