using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when a new topup transaction has been created and persisted. Triggers
/// the outbox publication of the <c>PaymentRequested</c> integration event.
/// </summary>
public sealed record TopupCreatedEvent : IDomainEvent
{
    public TopupCreatedEvent(
        Guid topupId,
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        MobileNumber = mobileNumber;
        Amount = amount;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public MobileNumber MobileNumber { get; }

    public Money Amount { get; }

    /// <summary>Correlation id carried across the payment/topup flow.</summary>
    public Guid CorrelationId { get; }
}
