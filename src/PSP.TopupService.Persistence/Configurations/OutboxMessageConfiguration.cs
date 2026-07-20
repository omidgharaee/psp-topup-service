using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSP.TopupService.Persistence.Outbox;
using PSP.TopupService.Persistence.Outbox.Enums;

namespace PSP.TopupService.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="OutboxMessage"/> table. Indexes target the publisher
/// worker's polling query so the common case (find pending rows, ordered) is an
/// index-only scan.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Type).HasMaxLength(256).HasColumnName("type").IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").HasColumnName("payload").IsRequired();
        builder.Property(m => m.Version).HasColumnName("version");
        builder.Property(m => m.CorrelationId).HasColumnName("correlation_id");
        builder.Property(m => m.RoutingKey).HasMaxLength(128).HasColumnName("routing_key");
        builder.Property(m => m.Status).HasColumnName("status");
        builder.Property(m => m.AttemptCount).HasColumnName("attempt_count");
        builder.Property(m => m.MaxAttempts).HasColumnName("max_attempts");
        builder.Property(m => m.OccurredOnUtc).HasColumnName("occurred_on_utc");
        builder.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on_utc");
        builder.Property(m => m.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(m => m.LastError).HasMaxLength(2048).HasColumnName("last_error");
        builder.Property(m => m.DeadLetterAfterUtc).HasColumnName("dead_letter_after_utc");

        // Partial index covering the worker's most common query: pending rows
        // (or rows whose lock has expired), ordered by occurrence.
        builder.HasIndex(m => new { m.Status, m.OccurredOnUtc })
            .HasFilter($"status = {(int)OutboxMessageStatus.Pending}");

        builder.HasIndex(m => m.CorrelationId);
    }
}
