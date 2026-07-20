using PSP.TopupService.Application.Common.Outbox;

namespace PSP.TopupService.Application.Common.Abstractions;

/// <summary>
/// Unit of Work: commits all changes made within a single command handler in
/// one atomic database transaction. Outbox messages enqueued during the
/// transaction are persisted in the same commit so that the domain state and
/// the published-events intent cannot diverge.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all tracked changes and dispatched outbox messages atomically.
    /// Throws on failure; callers should let the exception propagate to the
    /// pipeline's <c>UnhandledExceptionBehavior</c>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction explicitly. Use when multiple SaveChanges calls
    /// must share a single transaction (rare; prefer the implicit one).
    /// </summary>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
