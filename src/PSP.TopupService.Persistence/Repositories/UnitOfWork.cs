using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>. Wraps SaveChangesAsync
/// in the ambient transaction started by the EF Core provider. The Transaction
/// behaviour calls this exactly once per command, after the handler returns, so
/// the aggregate state change and the enqueued outbox message commit together.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TopupDbContext _context;

    public UnitOfWork(TopupDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new TransactionWrapper(transaction);
    }

    private sealed class TransactionWrapper : IAsyncDisposable
    {
        private readonly IDbContextTransaction _transaction;
        private bool _disposed;

        public TransactionWrapper(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _transaction.DisposeAsync();
        }
    }
}
