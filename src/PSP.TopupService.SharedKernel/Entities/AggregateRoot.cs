namespace PSP.TopupService.SharedKernel.Entities;

/// <summary>
/// Marker base class for an aggregate root — the consistency boundary within
/// which all invariants must hold. Only aggregate roots are loaded/saved by
/// repositories; child entities are mutated only through the root.
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier.</typeparam>
public abstract class AggregateRoot<TId> : BaseEntity
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id)
        : base(id is Guid g ? g : Guid.NewGuid())
    {
        AggregateId = id;
    }

    /// <summary>
    /// The domain-typed identifier of the aggregate (may differ from <see cref="BaseEntity.Id"/>
    /// when the aggregate uses a strongly-typed id).
    /// </summary>
    public TId AggregateId { get; protected set; } = default!;
}
