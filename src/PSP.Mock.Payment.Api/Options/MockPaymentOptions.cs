namespace PSP.Mock.Payment.Api.Options;

/// <summary>
/// Failure-injection configuration for the Payment-service simulator. Operators
/// flip these to exercise resiliency in integration tests and chaos drills.
/// </summary>
public sealed class MockPaymentOptions
{
    public const string SectionName = "MockPayment";

    /// <summary>Fraction of payment requests that fail (0..1).</summary>
    public double PaymentFailureRate { get; set; }

    /// <summary>Fraction of advice requests that fail (0..1).</summary>
    public double AdviceFailureRate { get; set; }

    /// <summary>Fraction of reversal requests that fail (0..1).</summary>
    public double ReverseFailureRate { get; set; }

    /// <summary>Seconds to wait before publishing the PaymentCompleted event.</summary>
    public int PaymentCompletionDelaySeconds { get; set; } = 1;

    /// <summary>Seconds to wait before publishing the AdviceCompleted event.</summary>
    public int AdviceCompletionDelaySeconds { get; set; }

    /// <summary>Seconds to wait before publishing the PaymentReversed event.</summary>
    public int ReverseCompletionDelaySeconds { get; set; }
}
