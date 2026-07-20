using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when the mobile operator confirms the topup succeeded. Terminal
/// success state for the aggregate.
/// </summary>
public sealed record TopupCompletedEvent : IDomainEvent
{
    public TopupCompletedEvent(
        Guid topupId,
        TransactionReference mciReference,
        DateTime completedAtUtc,
        Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        MciReference = mciReference;
        CompletedAtUtc = completedAtUtc;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public TransactionReference MciReference { get; }

    public DateTime CompletedAtUtc { get; }

    public Guid CorrelationId { get; }
}
