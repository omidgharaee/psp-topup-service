using PSP.TopupService.Domain.Topups;

namespace PSP.TopupService.Application.Common.Abstractions;

/// <summary>
/// Repository contract for the <see cref="TopupTransaction"/> aggregate.
/// The Application layer depends only on this contract; the concrete EF Core
/// implementation lives in Persistence. This keeps the dependency rule strict
/// and the domain free of any persistence concerns.
/// </summary>
public interface ITopupRepository
{
    /// <summary>Adds a new topup aggregate to the unit of work (not yet persisted).</summary>
    Task<TopupTransaction> AddAsync(TopupTransaction topup, CancellationToken cancellationToken = default);

    /// <summary>Loads a topup by its primary key. Returns null when not found.</summary>
    Task<TopupTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a topup by id and acquires a pessimistic lock for the duration of the unit of work.</summary>
    Task<TopupTransaction?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a previously-saved topup by its idempotency key, if any.</summary>
    Task<TopupTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Marks the aggregate as modified. Most persistence change-tracking handles this implicitly.</summary>
    void Update(TopupTransaction topup);
}
