using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Events;

/// <summary>
/// Raised when the Bank confirms the payment was successful. Triggers the
/// topup call against the mobile operator (Hamrah-e-Aval).
/// </summary>
public sealed record PaymentCompletedEvent : IDomainEvent
{
    public PaymentCompletedEvent(
        Guid topupId,
        TransactionReference bankReference,
        DateTime confirmedAtUtc,
        Guid correlationId)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        TopupId = topupId;
        BankReference = bankReference;
        ConfirmedAtUtc = confirmedAtUtc;
        CorrelationId = correlationId;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }

    public Guid TopupId { get; }

    public TransactionReference BankReference { get; }

    public DateTime ConfirmedAtUtc { get; }

    public Guid CorrelationId { get; }
}
