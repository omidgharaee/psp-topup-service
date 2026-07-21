namespace PSP.TopupService.SharedKernel.ValueObjects;

/// <summary>
/// Base class for value objects. Two value objects are equal when all their
/// atomic components are equal. Deriving classes implement
/// <see cref="GetEqualityComponents"/> to declare those components.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// The set of components that participate in equality. Order is preserved.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var component in GetEqualityComponents())
            {
                hash = (hash * 31) + (component?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }
}
