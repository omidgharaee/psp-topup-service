using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.Domain.Topups.Entities;

/// <summary>
/// Records the reversal of a payment after a terminal topup failure. A topup
/// transaction carries at most one reversal; the record is created when the
/// reversal is initiated and updated once the Bank confirms the refund.
/// </summary>
public sealed class ReverseRecord : BaseEntity
{
    private ReverseRecord()
    {
        // EF Core constructor
    }

    internal ReverseRecord(Guid topupId, TopupFailureReason reason, string requestedBy)
    {
        TopupId = topupId;
        Reason = reason;
        RequestedBy = requestedBy;
        InitiatedAtUtc = DateTime.UtcNow;
        Status = ReverseStatus.Initiated;
    }

    public Guid TopupId { get; private set; }

    public TopupFailureReason Reason { get; private set; }

    public string RequestedBy { get; private set; } = string.Empty;

    public DateTime InitiatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public ReverseStatus Status { get; private set; }

    /// <summary>The reference returned by the Bank for the reversal (set once completed).</summary>
    public TransactionReference? BankReversalReference { get; private set; }

    public void MarkCompleted(TransactionReference bankReversalReference)
    {
        ArgumentNullException.ThrowIfNull(bankReversalReference);
        if (Status != ReverseStatus.Initiated)
        {
            return;
        }

        BankReversalReference = bankReversalReference;
        Status = ReverseStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed(string reason)
    {
        if (Status != ReverseStatus.Initiated)
        {
            return;
        }

        Status = ReverseStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();
    }
}
