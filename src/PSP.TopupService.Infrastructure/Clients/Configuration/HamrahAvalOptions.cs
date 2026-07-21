namespace PSP.TopupService.Infrastructure.Clients.Configuration;

/// <summary>
/// Strongly-typed configuration for the Hamrah-e-Aval (MCI) HTTP client and its
/// Polly resilience pipeline. All values come from appsettings / environment.
/// </summary>
public sealed class HamrahAvalOptions
{
    public const string SectionName = "HamrahAval";

    /// <summary>Base URL of the operator's topup API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5070";

    /// <summary>API key / bearer token sent on every request.</summary>
    public string ApiKey { get; set; } = "test-key";

    /// <summary>Per-request HTTP timeout before Polly cancels the call.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Number of retries on a transient failure.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Base delay for the exponential backoff between retries.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Consecutive failures before the circuit opens.</summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>How long the circuit stays open before probing again.</summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Configurable simulated failure rate (0..1) — used to exercise the policies.</summary>
    public double SimulatedFailureRate { get; set; }
}
