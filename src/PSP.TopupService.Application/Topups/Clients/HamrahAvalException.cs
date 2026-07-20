using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.Application.Topups.Clients;

/// <summary>
/// Raised when the Hamrah-e-Aval provider fails terminally — every Polly
/// retry has been exhausted and the fallback policy kicked in. The reverse
/// payment flow is triggered as a result.
/// </summary>
public sealed class HamrahAvalException : InfrastructureException
{
    public HamrahAvalException(Guid topupId, string message)
        : base($"Hamrah-e-Aval topup failed for transaction {topupId}: {message}")
    {
        TopupId = topupId;
    }

    public HamrahAvalException(Guid topupId, string message, Exception innerException)
        : base($"Hamrah-e-Aval topup failed for transaction {topupId}: {message}", innerException)
    {
        TopupId = topupId;
    }

    public override string Code => "Topup.ProviderFailed";

    public Guid TopupId { get; }

    /// <summary>True when the failure was transient (timeout/5xx) and a retry may eventually succeed.</summary>
    public override bool IsTransient => false; // we already retried; the next attempt is a fresh message
}
