using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.Persistence.Inbox;

/// <summary>
/// EF Core implementation of <see cref="IInboxStore"/>. The unique index on
/// (MessageId, Consumer) is the idempotency gate: a duplicate insert throws a
/// Postgres unique-violation which we translate to <c>false</c> (replay).
/// </summary>
public sealed class EfInboxStore : IInboxStore
{
    private readonly TopupDbContext _context;

    public EfInboxStore(TopupDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryMarkProcessingAsync(Guid messageId, string consumer, string? payloadHash, CancellationToken cancellationToken = default)
    {
        var inboxEntry = new InboxMessage
        {
            Id = $"{consumer}:{messageId}",
            MessageId = messageId,
            Consumer = consumer,
            ReceivedOnUtc = DateTime.UtcNow,
            ProcessedOnUtc = DateTime.UtcNow,
            PayloadHash = payloadHash,
        };

        try
        {
            await _context.InboxMessages.AddAsync(inboxEntry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Duplicate (messageId, consumer): the message was already processed.
            // Detach the failed insert so it doesn't re-throw on the next SaveChanges.
            _context.ChangeTracker.Entries<InboxMessage>().ToList().ForEach(e => e.State = EntityState.Detached);
            return false;
        }
    }

    /// <summary>True for PostgreSQL unique-constraint violations (sqlstate 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;
}
