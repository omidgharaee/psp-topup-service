namespace PSP.TopupService.Application.Common.Inbox;

/// <summary>
/// Idempotent consume-local pattern. Before processing a message, the consumer
/// attempts to mark it as processed; if it was already processed (unique-key
/// violation on MessageId+Consumer), the message is a replay and must be skipped.
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Tries to record that a message is being processed by a consumer.
    /// Returns false if the message was already processed (idempotency replay).
    /// The row is committed in the SAME transaction as the business change.
    /// </summary>
    Task<bool> TryMarkProcessingAsync(Guid messageId, string consumer, string? payloadHash, CancellationToken cancellationToken = default);
}
