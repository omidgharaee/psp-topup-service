namespace PSP.TopupService.SharedKernel.Entities;

/// <summary>Marker for entities that participate in the audit log.</summary>
public interface IAuditable
{
    DateTime CreatedOnUtc { get; }
    DateTime ModifiedOnUtc { get; }
    string? CreatedBy { get; }
    string? ModifiedBy { get; }
}

/// <summary>Marker for entities that are soft-deleted rather than physically removed.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedOnUtc { get; }
}

/// <summary>Marker for entities that use optimistic concurrency control.</summary>
public interface IVersioned
{
    byte[] RowVersion { get; }
}
