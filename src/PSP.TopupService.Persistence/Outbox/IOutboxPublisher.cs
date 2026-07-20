using PSP.TopupService.Persistence.Outbox.Enums;

namespace PSP.TopupService.Persistence.Outbox;

/// <summary>
/// Reads pending outbox rows and atomically leases them for publication. The
/// lease is implemented as an UPDATE ... WHERE status = Pending RETURNING so
/// two workers racing on the same row cannot both win.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Atomically leases up to <paramref name="batchSize"/> pending outbox rows,
    /// marking them <see cref="OutboxMessageStatus.InProgress"/> with a lease
    /// expiry of <paramref name="leaseDuration"/>. Returns the leased rows.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> LeasePendingAsync(int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>Marks a message as successfully published.</summary>
    Task MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed: increments attempt count, records the error,
    /// and either re-queues (status=Pending) or dead-letters when max attempts
    /// are exhausted.
    /// </summary>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);

    /// <summary>Re-queues messages whose leases have expired back to Pending.</summary>
    Task<int> ReclaimExpiredLeasesAsync(CancellationToken cancellationToken = default);
}
