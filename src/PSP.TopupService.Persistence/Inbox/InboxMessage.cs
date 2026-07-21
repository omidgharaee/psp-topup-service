namespace PSP.TopupService.Persistence.Inbox;

/// <summary>
/// Idempotency record for consumed messages. The consumer writes a row in the
/// SAME transaction as the business change so that a redelivered message is a
/// no-op: the unique key (MessageId + Consumer) makes the second insert fail
/// before business logic runs.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>
    /// Composite id is composed in code as <c>{consumer}:{messageId}</c> so it
    /// maps to a unique primary key and the insert acts as a distributed lock.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The message id assigned by the broker (RabbitMQ / MassTransit).</summary>
    public Guid MessageId { get; set; }

    /// <summary>The consumer that processed the message (so the same message can be handled by multiple consumers).</summary>
    public string Consumer { get; set; } = string.Empty;

    public DateTime ReceivedOnUtc { get; set; } = DateTime.UtcNow;

    public DateTime ProcessedOnUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Payload hash for forensic reconciliation.</summary>
    public string? PayloadHash { get; set; }
}
