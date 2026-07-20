using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Persistence.Context;
using PSP.TopupService.Persistence.Outbox.Enums;

namespace PSP.TopupService.Persistence.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutboxPublisher"/>. Uses a raw SQL
/// UPDATE ... RETURNING for the lease so the row selection and status mutation
/// happen atomically inside the database, eliminating worker races.
/// </summary>
public sealed class EfOutboxPublisher : IOutboxPublisher
{
    private readonly TopupDbContext _context;

    public EfOutboxPublisher(TopupDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> LeasePendingAsync(int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var leasedUntil = DateTime.UtcNow.Add(leaseDuration);

        // Atomic lease: UPDATE the first N pending rows to InProgress and return them.
        // PostgreSQL syntax: UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED)
        var leasedIds = await _context.Database.SqlQueryRaw<Guid>(
            """
            WITH next AS (
                SELECT id FROM outbox_messages
                WHERE status = {0} AND (locked_until_utc IS NULL OR locked_until_utc < {1})
                ORDER BY occurred_on_utc
                LIMIT {2}
                FOR UPDATE SKIP LOCKED
            )
            UPDATE outbox_messages
            SET status = {3}, locked_until_utc = {4}, attempt_count = attempt_count + 1
            FROM next
            WHERE outbox_messages.id = next.id
            RETURNING outbox_messages.id
            """,
            (int)OutboxMessageStatus.Pending,
            DateTime.UtcNow,
            batchSize,
            (int)OutboxMessageStatus.InProgress,
            leasedUntil).ToListAsync(cancellationToken);

        if (leasedIds.Count == 0)
        {
            return Array.Empty<OutboxMessage>();
        }

        return await _context.OutboxMessages
            .Where(m => leasedIds.Contains(m.Id))
            .AsTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE outbox_messages
            SET status = {0}, processed_on_utc = {1}, locked_until_utc = NULL, last_error = NULL
            WHERE id = {2}
            """,
            (int)OutboxMessageStatus.Published,
            DateTime.UtcNow,
            messageId);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        // Increment already happened in LeasePending; here we decide: retry or dead-letter.
        var message = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message is null)
        {
            return;
        }

        var exhausted = message.AttemptCount >= message.MaxAttempts;
        message.LastError = error.Length > 2048 ? error[..2048] : error;

        if (exhausted)
        {
            message.Status = OutboxMessageStatus.DeadLettered;
            message.DeadLetterAfterUtc = DateTime.UtcNow;
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            message.LockedUntilUtc = DateTime.UtcNow.Add(TimeSpan.FromSeconds(Math.Min(60, message.AttemptCount * 5)));
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ReclaimExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        // Any InProgress row whose lease has expired must be reset to Pending so
        // a crashed worker does not strand it forever.
        return await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE outbox_messages
            SET status = {0}
            WHERE status = {1} AND locked_until_utc < {2}
            """,
            (int)OutboxMessageStatus.Pending,
            (int)OutboxMessageStatus.InProgress,
            DateTime.UtcNow);
    }
}
