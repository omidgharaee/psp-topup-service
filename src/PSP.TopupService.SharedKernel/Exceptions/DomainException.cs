namespace PSP.TopupService.SharedKernel.Exceptions;

/// <summary>
/// Base type for all domain-layer exceptions. Prefer returning a failed
/// <c>Result</c> for expected business-rule violations; reserve exceptions for
/// truly unexpected, non-recoverable conditions inside the domain.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Stable machine-readable code identifying the failure.</summary>
    public abstract string Code { get; }
}
