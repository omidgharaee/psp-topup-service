using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when the payment has been reversed (refund completed at the Bank).
/// Terminal failure state with explicit reversal.
/// </summary>
public sealed record PaymentReversedEvent : IDomainEvent
{
    public PaymentReversedEvent(
        Guid topupId,
        TransactionReference reversalReference,
        TopupFailureReason reason,
        DateTime reversedAtUtc,
        Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        ReversalReference = reversalReference;
        Reason = reason;
        ReversedAtUtc = reversedAtUtc;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public TransactionReference ReversalReference { get; }

    public TopupFailureReason Reason { get; }

    public DateTime ReversedAtUtc { get; }

    public Guid CorrelationId { get; }
}
