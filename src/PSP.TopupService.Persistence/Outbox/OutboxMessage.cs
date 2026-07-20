using PSP.TopupService.Persistence.Outbox.Enums;

namespace PSP.TopupService.Persistence.Outbox;

/// <summary>
/// A single row in the transactional outbox. Inserted in the SAME database
/// transaction as the business state change so the intent to publish survives
/// crashes. A background worker polls for unpublished rows and forwards them to
/// RabbitMQ, then marks them published.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Integration-event type name (used by the consumer to deserialize).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Payload serialized as JSON (the integration-event body).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Schema version of the payload, for forward compatibility.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Correlation id propagated end-to-end (also embedded in headers).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>RabbitMQ routing key / exchange name the publisher should target.</summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>Lifecycle status of the outbox message.</summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 10;

    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedOnUtc { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public string? LastError { get; set; }

    /// <summary>UTC instant after which the message is considered poison and dead-lettered.</summary>
    public DateTime? DeadLetterAfterUtc { get; set; }
}
