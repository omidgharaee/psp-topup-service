using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSP.TopupService.Persistence.Inbox;

namespace PSP.TopupService.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="InboxMessage"/> idempotency table. The unique index on
/// (MessageId, Consumer) is the linchpin of idempotent consumption: a duplicate
/// insert raises a unique-violation that the consumer uses as a stop-signal.
/// </summary>
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasMaxLength(320).HasColumnName("id");
        builder.Property(m => m.MessageId).HasColumnName("message_id");
        builder.Property(m => m.Consumer).HasMaxLength(128).HasColumnName("consumer").IsRequired();
        builder.Property(m => m.ReceivedOnUtc).HasColumnName("received_on_utc");
        builder.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on_utc");
        builder.Property(m => m.PayloadHash).HasMaxLength(128).HasColumnName("payload_hash");

        builder.HasIndex(m => new { m.MessageId, m.Consumer }).IsUnique();
    }
}
