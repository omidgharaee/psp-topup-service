using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when a topup has failed terminally (e.g. all retries exhausted). This
/// typically triggers the reverse-payment flow.
/// </summary>
public sealed record TopupFailedEvent : IDomainEvent
{
    public TopupFailedEvent(
        Guid topupId,
        TopupFailureReason reason,
        string failureMessage,
        Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        Reason = reason;
        FailureMessage = failureMessage;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public TopupFailureReason Reason { get; }

    public string FailureMessage { get; }

    public Guid CorrelationId { get; }
}
