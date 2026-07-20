using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.Persistence.Interceptors;

/// <summary>
/// Generates <see cref="AuditLog"/> entries for every state change on a
/// <see cref="TopupTransaction"/>. Runs inside SaveChangesAsync so the audit
/// trail cannot be bypassed by application code — it commits in the same
/// transaction as the business change.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICorrelationContext _correlation;

    public AuditSaveChangesInterceptor(ICorrelationContext correlation)
    {
        _correlation = correlation;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is TopupDbContext context)
        {
            WriteAuditEntries(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditEntries(TopupDbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<TopupTransaction>())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            context.AuditLogs.Add(new AuditLog
            {
                TopupId = entry.Entity.Id,
                Actor = _correlation.Actor,
                Action = entry.State == EntityState.Added ? "Create" : "Update",
                BeforeState = entry.State == EntityState.Modified ? SerializePrevious(entry) : null,
                AfterState = SerializeCurrent(entry),
                CorrelationId = _correlation.CorrelationId,
                RequestId = _correlation.RequestId == Guid.Empty ? null : _correlation.RequestId,
                TraceId = _correlation.TraceId,
                RemoteIp = _correlation.RemoteIp,
                OccurredOnUtc = DateTime.UtcNow,
            });
        }
    }

    private static string SerializeCurrent(PropertyValues values) =>
        System.Text.Json.JsonSerializer.Serialize(values.ToObject(), new System.Text.Json.JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        });

    private static string SerializePrevious(EntityEntry entry) =>
        entry.GetDatabaseValues() is { } dbValues
            ? SerializeCurrent(dbValues)
            : "{}";

    private static string SerializeCurrent(EntityEntry entry) =>
        entry.CurrentValues.ToObject() is { } current
            ? System.Text.Json.JsonSerializer.Serialize(current, current.GetType(), new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            })
            : "{}";
}
