namespace PSP.TopupService.Worker.Options;

/// <summary>Tuning parameters for the outbox publisher background service.</summary>
public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    /// <summary>How long the worker sleeps between polling cycles.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum number of rows leased per cycle.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>How long a leased row is considered "owned" by a worker before it can be reclaimed.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Whether the expired-lease sweeper runs on the same loop.</summary>
    public bool ReclaimExpiredLeases { get; set; } = true;
}
