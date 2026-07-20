using PSP.TopupService.SharedKernel.Guards;
using PSP.TopupService.SharedKernel.ValueObjects;

namespace PSP.TopupService.Domain.Topups.ValueObjects;

/// <summary>
/// An opaque reference returned by an external system (Bank trace, MCI
/// reference). Used as an idempotency key on the consumer side and stored for
/// later reconciliation. Two references are equal iff their values match
/// case-insensitively.
/// </summary>
public sealed class TransactionReference : ValueObject
{
    private const int MaxLength = 128;

    private TransactionReference(string value, string source)
    {
        Value = value;
        Source = source;
    }

    /// <summary>The reference token as returned by the external system.</summary>
    public string Value { get; }

    /// <summary>The originating system, e.g. "BANK", "MCI", "REVERSAL".</summary>
    public string Source { get; }

    public static TransactionReference Create(string value, string source)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        Guard.NotNullOrWhiteSpace(source, nameof(source));

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Reference value cannot exceed {MaxLength} characters.", nameof(value));
        }

        return new TransactionReference(value.Trim(), source.Trim().ToUpperInvariant());
    }

    /// <summary>Creates a fresh reference using a URL-safe random token (e.g. for our own RRN).</summary>
    public static TransactionReference Generate(string source)
    {
        Guard.NotNullOrWhiteSpace(source, nameof(source));
        return new TransactionReference(Guid.NewGuid().ToString("N"), source.Trim().ToUpperInvariant());
    }

    public override string ToString() => $"{Source}:{Value}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Source;
    }
}
