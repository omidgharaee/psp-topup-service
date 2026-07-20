using System.Text.RegularExpressions;
using PSP.TopupService.SharedKernel.Guards;
using PSP.TopupService.SharedKernel.ValueObjects;

namespace PSP.TopupService.Domain.Topups.ValueObjects;

/// <summary>
/// An Iranian mobile number in the canonical <c>09XXXXXXXXX</c> form (11 digits).
/// Normalisation is applied on construction so two numbers with different
/// whitespace/prefix spellings compare equal.
/// </summary>
public sealed partial class MobileNumber : ValueObject
{
    private const int CanonicalLength = 11;

    private static readonly Regex ValidationPattern = ValidationRegex();

    private MobileNumber(string value)
    {
        Value = value;
    }

    /// <summary>The canonical <c>09XXXXXXXXX</c> representation.</summary>
    public string Value { get; }

    public static MobileNumber Create(string raw)
    {
        var normalized = Normalize(raw);
        Guard.NotNullOrWhiteSpace(normalized, nameof(raw));

        if (!ValidationPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                $"'{raw}' is not a valid Iranian mobile number. Expected 11 digits starting with 09.",
                nameof(raw));
        }

        return new MobileNumber(normalized);
    }

    /// <summary>
    /// Tries to create a <see cref="MobileNumber"/>; returns null on invalid input
    /// instead of throwing (useful for validation pipelines).
    /// </summary>
    public static MobileNumber? TryCreate(string raw) =>
        TryNormalize(raw, out var normalized) && ValidationPattern.IsMatch(normalized)
            ? new MobileNumber(normalized)
            : null;

    /// <summary>Returns the number in international <c>+989XXXXXXXXX</c> form.</summary>
    public string ToInternational() => "+98" + Value[1..];

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();

        // +98 9XXXXXXXXX -> 09XXXXXXXXX
        if (trimmed.StartsWith("+98", StringComparison.Ordinal))
        {
            trimmed = "0" + trimmed[3..];
        }
        else if (trimmed.StartsWith("0098", StringComparison.Ordinal))
        {
            trimmed = "0" + trimmed[4..];
        }
        else if (trimmed.StartsWith("98", StringComparison.Ordinal) && trimmed.Length == 12)
        {
            trimmed = "0" + trimmed[2..];
        }

        // strip spaces, dashes, dots, parentheses
        return trimmed.Replace(" ", string.Empty).Replace("-", string.Empty)
                      .Replace(".", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);
    }

    private static bool TryNormalize(string raw, out string normalized)
    {
        try
        {
            normalized = Normalize(raw);
            return normalized.Length == CanonicalLength;
        }
        catch (Exception)
        {
            normalized = string.Empty;
            return false;
        }
    }

    [GeneratedRegex(@"^09\d{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ValidationRegex();
}
