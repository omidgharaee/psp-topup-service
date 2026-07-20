using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Abstractions;

/// <summary>
/// Abstraction over the Bank gateway. The Topup service uses it only to
/// reverse a payment after a terminal topup failure; payment initiation is
/// done by the Bank mock in response to the PaymentRequested outbox event.
/// </summary>
public interface IBankClient
{
    /// <summary>
    /// Requests that the Bank reverse (refund) the payment for a topup.
    /// Returns the Bank's reversal reference on success; throws on terminal
    /// failure. Implementations are expected to retry transient failures
    /// internally (Polly) so the caller only sees a definitive outcome.
    /// </summary>
    Task<BankReversalResult> ReverseAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful Bank reversal.</summary>
public sealed record BankReversalResult
{
    /// <summary>The reference returned by the Bank for the reversal (stored as ReversalReference).</summary>
    public required TransactionReference ReversalReference { get; init; }

    /// <summary>UTC instant the Bank confirmed the reversal.</summary>
    public DateTime ReversedAtUtc { get; init; } = DateTime.UtcNow;
}
