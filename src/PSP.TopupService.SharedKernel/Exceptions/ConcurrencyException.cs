using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.SharedKernel.Exceptions;

/// <summary>
/// Raised when an optimistic-concurrency conflict is detected (another writer
/// updated the same row since it was read). Maps to HTTP 409 Conflict.
/// </summary>
public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(BaseEntity entity)
        : base($"Concurrency conflict on {entity.GetType().Name} (id={entity.Id}). The entity was modified by another writer.")
    {
        EntityId = entity.Id;
        EntityType = entity.GetType().Name;
    }

    public ConcurrencyException(string entityType, Guid entityId)
        : base($"Concurrency conflict on {entityType} (id={entityId}). The entity was modified by another writer.")
    {
        EntityId = entityId;
        EntityType = entityType;
    }

    public override string Code => "Concurrency.Conflict";

    public Guid EntityId { get; }

    public string EntityType { get; }
}
