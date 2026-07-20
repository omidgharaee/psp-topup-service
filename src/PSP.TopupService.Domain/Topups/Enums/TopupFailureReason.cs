namespace PSP.TopupService.Domain.Topups.Enums;

/// <summary>
/// Categorises why a topup transitioned to a failure/reversed state. Stored on
/// the aggregate so operators can later reconcile and audit without parsing logs.
/// </summary>
public enum TopupFailureReason
{
    /// <summary>No failure recorded.</summary>
    None = 0,

    /// <summary>The Bank declined the payment or returned a failure status.</summary>
    PaymentDeclined = 1,

    /// <summary>The payment confirmation event never arrived within the SLA window.</summary>
    PaymentTimeout = 2,

    /// <summary>All retries against the mobile operator were exhausted without success.</summary>
    TopupProviderError = 3,

    /// <summary>The mobile operator explicitly rejected the topup (invalid number, blocked, etc.).</summary>
    TopupRejected = 4,

    /// <summary>An unrecoverable infrastructure error occurred (timeout, 5xx after retries).</summary>
    InfrastructureError = 5,

    /// <summary>The transaction was reversed by an operator or an upstream reversal event.</summary>
    Reversed = 6,
}
