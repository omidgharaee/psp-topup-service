namespace PSP.TopupService.Persistence;

/// <summary>
/// Append-only audit trail of every state transition on the Topup aggregate.
/// Written by an EF Core interceptor on each SaveChanges that mutates a
/// TopupTransaction, so the audit cannot be skipped by application code.
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    /// <summary>The aggregate id the entry refers to.</summary>
    public Guid TopupId { get; set; }

    /// <summary>Actor who initiated the change (from the correlation context).</summary>
    public string? Actor { get; set; }

    /// <summary>Action performed (e.g. "Create", "MarkPaymentCompleted").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Snapshot of the previous state (JSON), for diff/replay.</summary>
    public string? BeforeState { get; set; }

    /// <summary>Snapshot of the new state (JSON).</summary>
    public string? AfterState { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? RequestId { get; set; }

    public string? TraceId { get; set; }

    public string? RemoteIp { get; set; }

    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
}
