namespace PSP.TopupService.SharedKernel.Entities;

/// <summary>
/// Base class for all entities (aggregate roots and their internal entities).
/// Encapsulates identity, audit fields, soft-delete, optimistic concurrency and
/// domain-event collection. The set of changes is exposed read-only; aggregates
/// mutate state through behaviour methods and may raise events via
/// <see cref="RaiseEvent"/>.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedOnUtc = DateTime.UtcNow;
        ModifiedOnUtc = DateTime.UtcNow;
    }

    protected BaseEntity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity id cannot be empty.", nameof(id));
        }

        Id = id;
        CreatedOnUtc = DateTime.UtcNow;
        ModifiedOnUtc = DateTime.UtcNow;
    }

    /// <summary>Primary key. Guid provides global uniqueness across services and shards.</summary>
    public Guid Id { get; protected set; }

    /// <summary>UTC creation timestamp (audit).</summary>
    public DateTime CreatedOnUtc { get; protected set; }

    /// <summary>UTC last-modification timestamp (audit), updated on each mutation.</summary>
    public DateTime ModifiedOnUtc { get; protected set; }

    /// <summary>Optional creator identity for audit (populated from authenticated principal).</summary>
    public string? CreatedBy { get; protected set; }

    /// <summary>Optional last-modifier identity for audit.</summary>
    public string? ModifiedBy { get; protected set; }

    /// <summary>Soft-delete marker. Deleted entities are retained for audit and reconciliation.</summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>Optional UTC deletion timestamp.</summary>
    public DateTime? DeletedOnUtc { get; protected set; }

    /// <summary>
    /// Optimistic-concurrency token. EF Core maps this to a rowversion/xmin column.
    /// </summary>
    public byte[] RowVersion { get; protected set; } = [];

    /// <summary>Domain events raised but not yet dispatched.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Raises a domain event for eventual dispatch after persistence.</summary>
    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Removes a single domain event (called after it has been dispatched).</summary>
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    /// <summary>Clears all pending domain events without dispatching.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Marks the entity as soft-deleted. Audit-only; does not remove the row.</summary>
    public virtual void SoftDelete(string? deletedBy = null)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedOnUtc = DateTime.UtcNow;
        ModifiedOnUtc = DateTime.UtcNow;
        ModifiedBy = deletedBy;
    }

    /// <summary>Restores a soft-deleted entity.</summary>
    public virtual void Restore()
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        DeletedOnUtc = null;
        ModifiedOnUtc = DateTime.UtcNow;
    }

    /// <summary>Updates the modification audit fields. Called by repositories on save.</summary>
    protected void Touch(string? modifiedBy = null)
    {
        ModifiedOnUtc = DateTime.UtcNow;
        if (modifiedBy is not null)
        {
            ModifiedBy = modifiedBy;
        }
    }

    public static bool operator ==(BaseEntity? left, BaseEntity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);

    public override bool Equals(object? obj) =>
        obj is BaseEntity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
