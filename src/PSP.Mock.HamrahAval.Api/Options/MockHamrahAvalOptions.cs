namespace PSP.Mock.HamrahAval.Api.Options;

/// <summary>
/// Failure-injection configuration for the Hamrah-e-Aval (MCI) topup
/// simulator. Operators flip these to exercise the Topup service's Polly
/// pipeline (retry / circuit breaker / fallback) in integration tests.
/// </summary>
public sealed class MockHamrahAvalOptions
{
    public const string SectionName = "MockHamrahAval";

    /// <summary>Fraction of topup calls to fail (0..1).</summary>
    public double FailureRate { get; set; }

    /// <summary>Maximum artificial delay applied to every call.</summary>
    public int MaxDelayMs { get; set; }

    /// <summary>When true, every call returns 503 (used to trip the circuit breaker).</summary>
    public bool AlwaysFail { get; set; }
}
