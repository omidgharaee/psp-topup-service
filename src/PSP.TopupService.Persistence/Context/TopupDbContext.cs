using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Persistence.Inbox;
using PSP.TopupService.Persistence.Outbox;

namespace PSP.TopupService.Persistence.Context;

/// <summary>
/// EF Core DbContext for the Topup service. Owns:
/// - the <see cref="TopupTransaction"/> aggregate and its child entities,
/// - the transactional <see cref="OutboxMessage"/> table,
/// - the consumer idempotency <see cref="InboxMessage"/> table,
/// - the <see cref="AuditLog"/> append-only trail.
/// Naming convention is snake_case (EFCore.NamingConventions); table-per-type
/// is used for value objects via owned-type mapping.
/// </summary>
public sealed class TopupDbContext : DbContext
{
    public TopupDbContext(DbContextOptions<TopupDbContext> options)
        : base(options)
    {
    }

    public DbSet<TopupTransaction> Topups => Set<TopupTransaction>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case naming for all tables/columns is applied at the
        // DbContextOptionsBuilder level (UseSnakeCaseNamingConvention) so every
        // table name and column name follows the PostgreSQL convention without
        // duplicating it in each configuration.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TopupDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
