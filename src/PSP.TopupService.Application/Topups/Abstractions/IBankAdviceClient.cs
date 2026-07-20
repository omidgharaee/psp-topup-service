using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Abstractions;

/// <summary>
/// Abstraction over the Bank's Advice (finalization) endpoint. After a
/// successful MCI topup, the Bank must be told to finalize the payment; only
/// then is the transaction terminal-success. The client owns its own Polly
/// pipeline; callers see a definitive outcome.
/// </summary>
public interface IBankAdviceClient
{
    /// <summary>
    /// Sends an Advice (finalization) for a payment. Returns the Bank's advice
    /// reference on success; throws on failure (transient or terminal).
    /// </summary>
    Task<BankAdviceResult> AdviceAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful Bank Advice call.</summary>
public sealed record BankAdviceResult
{
    /// <summary>The reference returned by the Bank for the advice (stored as the aggregate's final BankReference).</summary>
    public required TransactionReference AdviceReference { get; init; }

    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
