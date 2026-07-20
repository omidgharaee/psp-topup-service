namespace PSP.TopupService.Persistence.Outbox.Enums;

/// <summary>Lifecycle of an <see cref="OutboxMessage"/>.</summary>
public enum OutboxMessageStatus
{
    /// <summary>Just inserted; awaiting the publisher worker to pick it up.</summary>
    Pending = 0,

    /// <summary>A worker has leased it and is currently publishing. Other workers should skip it.</summary>
    InProgress = 1,

    /// <summary>Successfully published and acknowledged by the broker.</summary>
    Published = 2,

    /// <summary>Failed terminally (max attempts reached or poison). Requires manual intervention.</summary>
    DeadLettered = 3,
}
