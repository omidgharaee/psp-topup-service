using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PSP.TopupService.Persistence.Configurations;

/// <summary>Maps the append-only <see cref="AuditLog"/> table.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.TopupId).HasColumnName("topup_id");
        builder.Property(a => a.Actor).HasMaxLength(128).HasColumnName("actor");
        builder.Property(a => a.Action).HasMaxLength(64).HasColumnName("action").IsRequired();
        builder.Property(a => a.BeforeState).HasColumnType("jsonb").HasColumnName("before_state");
        builder.Property(a => a.AfterState).HasColumnType("jsonb").HasColumnName("after_state");
        builder.Property(a => a.CorrelationId).HasColumnName("correlation_id");
        builder.Property(a => a.RequestId).HasColumnName("request_id");
        builder.Property(a => a.TraceId).HasMaxLength(64).HasColumnName("trace_id");
        builder.Property(a => a.RemoteIp).HasMaxLength(64).HasColumnName("remote_ip");
        builder.Property(a => a.OccurredOnUtc).HasColumnName("occurred_on_utc");

        builder.HasIndex(a => new { a.TopupId, a.OccurredOnUtc });
        builder.HasIndex(a => a.CorrelationId);
    }
}
