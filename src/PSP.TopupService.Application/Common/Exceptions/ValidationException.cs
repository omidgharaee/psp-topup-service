using FluentValidation.Results;

namespace PSP.TopupService.Application.Common.Exceptions;

/// <summary>
/// Aggregates one or more FluentValidation failures. Maps to HTTP 400 with a
/// per-field <c>errors</c> dictionary in the ProblemDetails response.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures have occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
    }

    public ValidationException(string propertyName, string error)
        : this(new[] { new ValidationFailure(propertyName, error) })
    {
    }

    /// <summary>Property name -> array of error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Stable code surfaced in the ProblemDetails <c>type</c> field.</summary>
    public const string Code = "Validation.Failed";
}
