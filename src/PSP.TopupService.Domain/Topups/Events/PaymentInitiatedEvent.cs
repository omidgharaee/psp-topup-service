using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when the topup transitions to <c>PaymentInProgress</c>, i.e. the
/// payment request has been forwarded to the Bank gateway and we are awaiting
/// the asynchronous completion event.
/// </summary>
public sealed record PaymentInitiatedEvent : IDomainEvent
{
    public PaymentInitiatedEvent(Guid topupId, TransactionReference bankReference, Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        BankReference = bankReference;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public TransactionReference BankReference { get; }

    public Guid CorrelationId { get; }
}
