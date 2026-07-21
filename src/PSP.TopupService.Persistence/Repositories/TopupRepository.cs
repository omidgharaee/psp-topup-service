using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITopupRepository"/>. Reads/writes the
/// <see cref="TopupTransaction"/> aggregate including its owned children.
/// Soft-delete rows are filtered by the global query filter on the aggregate.
/// </summary>
public sealed class TopupRepository : ITopupRepository
{
    private readonly TopupDbContext _context;

    public TopupRepository(TopupDbContext context)
    {
        _context = context;
    }

    public Task<TopupTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Topups
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TopupTransaction?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // SELECT ... FOR UPDATE — pessimistic lock; ensures only one consumer
        // at a time can transition a given aggregate. Required when the broker
        // may redeliver messages concurrently.
        return await _context.Topups
            .FromSqlInterpolated($"SELECT * FROM topup_transactions WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TopupTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.Topups
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<TopupTransaction> AddAsync(TopupTransaction topup, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Topups.AddAsync(topup, cancellationToken);
        return entry.Entity;
    }

    public void Update(TopupTransaction topup) => _context.Topups.Update(topup);
}
