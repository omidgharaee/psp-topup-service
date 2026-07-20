using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised each time a topup attempt against the mobile operator is started.
/// One topup transaction may produce several of these as the resilience policy
/// (Polly) retries.
/// </summary>
public sealed record TopupAttemptStartedEvent : IDomainEvent
{
    public TopupAttemptStartedEvent(Guid topupId, int attemptNumber, Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        AttemptNumber = attemptNumber;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public int AttemptNumber { get; }

    public Guid CorrelationId { get; }
}
