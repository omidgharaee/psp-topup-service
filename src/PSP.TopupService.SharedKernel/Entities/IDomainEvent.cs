namespace PSP.TopupService.SharedKernel.Entities;

/// <summary>
/// Marker interface for a domain event — something that happened in the past
/// and is of interest to other parts of the domain. Implementations live in
/// Domain and are dispatched after the aggregate is persisted.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique identifier of this event instance (used for idempotent dispatch).</summary>
    Guid EventId { get; }

    /// <summary>The UTC instant at which the event was raised.</summary>
    DateTime OccurredOnUtc { get; }
}
