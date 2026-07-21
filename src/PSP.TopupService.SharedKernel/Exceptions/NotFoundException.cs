namespace PSP.TopupService.SharedKernel.Exceptions;

/// <summary>
/// Raised when an entity referenced by id cannot be found. Maps to HTTP 404.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public override string Code => "Common.NotFound";

    public string EntityName { get; }

    public object Key { get; }
}
