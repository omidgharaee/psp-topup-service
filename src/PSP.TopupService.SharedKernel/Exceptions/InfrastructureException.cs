namespace PSP.TopupService.SharedKernel.Exceptions;

/// <summary>
/// Base type for infrastructure-layer failures (downstream HTTP timeouts,
/// broker disconnects, etc.). The global exception middleware maps these to
/// HTTP 503 Service Unavailable and triggers resiliency retries where applicable.
/// </summary>
public abstract class InfrastructureException : Exception
{
    protected InfrastructureException(string message)
        : base(message)
    {
    }

    protected InfrastructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Stable machine-readable code identifying the failure.</summary>
    public abstract string Code { get; }

    /// <summary>True when retrying the operation may succeed (transient failure).</summary>
    public virtual bool IsTransient => true;
}
