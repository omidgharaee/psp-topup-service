using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Entities;

/// <summary>
/// A single attempt to charge the topup with the mobile operator. Multiple
/// attempts may exist on a single <see cref="TopupTransaction"/> as the
/// resilience policy retries transient failures.
/// </summary>
public sealed class TopupAttempt : BaseEntity
{
    private TopupAttempt()
    {
        // EF Core constructor
    }

    internal TopupAttempt(Guid topupId, int attemptNumber)
    {
        TopupId = topupId;
        AttemptNumber = attemptNumber;
        StartedAtUtc = DateTime.UtcNow;
        Status = TopupAttemptStatus.InProgress;
    }

    public Guid TopupId { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public TopupAttemptStatus Status { get; private set; }

    /// <summary>The reference returned by the mobile operator on success (null otherwise).</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>The error message captured on a failed attempt (null on success).</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>HTTP status code returned by the provider, if applicable.</summary>
    public int? HttpStatusCode { get; private set; }

    public void MarkSucceeded(string providerReference, int? httpStatusCode = null)
    {
        if (Status != TopupAttemptStatus.InProgress)
        {
            return;
        }

        ProviderReference = providerReference;
        HttpStatusCode = httpStatusCode;
        Status = TopupAttemptStatus.Succeeded;
        FinishedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed(string errorMessage, int? httpStatusCode = null)
    {
        if (Status != TopupAttemptStatus.InProgress)
        {
            return;
        }

        ErrorMessage = errorMessage;
        HttpStatusCode = httpStatusCode;
        Status = TopupAttemptStatus.Failed;
        FinishedAtUtc = DateTime.UtcNow;
        Touch();
    }
}
