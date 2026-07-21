namespace PSP.TopupService.Domain.Topups.Enums;

/// <summary>Status of a <see cref="Entities.ReverseRecord"/>.</summary>
public enum ReverseStatus
{
    /// <summary>Reversal request has been sent to the Bank; awaiting confirmation.</summary>
    Initiated = 1,

    /// <summary>The Bank has confirmed the refund; transaction is fully reversed.</summary>
    Completed = 2,

    /// <summary>The reversal itself failed and requires manual intervention.</summary>
    Failed = 3,
}
