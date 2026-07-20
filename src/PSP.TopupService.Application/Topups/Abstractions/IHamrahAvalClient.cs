using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Abstractions;

/// <summary>
/// Abstraction over the Hamrah-e-Aval (MCI) mobile topup provider. The
/// concrete HTTP client with Polly resilience lives in Infrastructure. The
/// Application layer depends only on this contract so the resilience policies
/// can be replaced or mocked in tests.
/// </summary>
public interface IHamrahAvalClient
{
    /// <summary>
    /// Requests a mobile topup from the operator. Returns the operator's
    /// reference on success; throws <c>HamrahAvalException</c> on terminal
    /// failure (after all Polly retries are exhausted).
    /// </summary>
    Task<HamrahAvalTopupResult> TopupAsync(
        Guid topupId,
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful topup call to the mobile operator.</summary>
public sealed record HamrahAvalTopupResult
{
    /// <summary>The reference returned by the operator (stored as MciReference).</summary>
    public required TransactionReference ProviderReference { get; init; }

    /// <summary>UTC instant the operator confirmed the topup.</summary>
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
