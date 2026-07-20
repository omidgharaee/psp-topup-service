namespace PSP.TopupService.Application.Topups.Commands.PerformAdvice;

/// <summary>Retry policy for the Bank Advice saga.</summary>
public sealed class AdviceOptions
{
    public const string SectionName = "Advice";

    /// <summary>Maximum advice attempts before the transaction is flagged AdviceFailed.</summary>
    public int MaxRetries { get; set; } = 10;

    /// <summary>Delay before the next advice attempt is scheduled in the outbox.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Exponential back-off multiplier applied to RetryDelay on each attempt.</summary>
    public double BackoffMultiplier { get; set; } = 1.5;
}
