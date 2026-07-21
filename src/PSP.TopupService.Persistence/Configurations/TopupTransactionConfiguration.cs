using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="TopupTransaction"/> aggregate root. Value objects are
/// stored as owned columns; child entities are owned by the root; soft-delete
/// and optimistic-concurrency are wired into the schema.
/// </summary>
public sealed class TopupTransactionConfiguration : IEntityTypeConfiguration<TopupTransaction>
{
    public void Configure(EntityTypeBuilder<TopupTransaction> builder)
    {
        builder.ToTable("topup_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnName("status");

        builder.Property(t => t.FailureReason)
            .HasConversion<string>()
            .HasMaxLength(48)
            .HasColumnName("failure_reason");

        builder.Property(t => t.CorrelationId)
            .HasColumnName("correlation_id");

        // Aggregate version (logical, incremented by the domain on each transition).
        builder.Property(t => t.Version)
            .HasColumnName("version");

        // RowVersion mapped to PostgreSQL xmin for optimistic concurrency.
        builder.Property(t => t.RowVersion)
            .IsRowVersion()
            .HasColumnName("row_version");

        builder.Property(t => t.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.Property(t => t.FailureMessage)
            .HasMaxLength(2048)
            .HasColumnName("failure_message");

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(256)
            .HasColumnName("idempotency_key");

        builder.Property(t => t.CreatedOnUtc).HasColumnName("created_on_utc");
        builder.Property(t => t.ModifiedOnUtc).HasColumnName("modified_on_utc");
        builder.Property(t => t.CreatedBy).HasMaxLength(128).HasColumnName("created_by");
        builder.Property(t => t.ModifiedBy).HasMaxLength(128).HasColumnName("modified_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted");
        builder.Property(t => t.DeletedOnUtc).HasColumnName("deleted_on_utc");

        // ----- Value objects: stored as owned complex properties. -----
        builder.OwnsOne(t => t.MobileNumber, mo =>
        {
            mo.Property(m => m.Value)
                .HasMaxLength(16)
                .HasColumnName("mobile_number")
                .IsRequired();
        });

        builder.OwnsOne(t => t.Amount, mo =>
        {
            mo.Property(m => m.Value)
                .HasPrecision(18, 2)
                .HasColumnName("amount")
                .IsRequired();
            mo.Property(m => m.Currency)
                .HasMaxLength(3)
                .HasColumnName("currency")
                .IsRequired();
        });

        builder.OwnsOne(t => t.BankReference, ro =>
        {
            ro.Property(r => r.Value).HasMaxLength(128).HasColumnName("bank_reference_value");
            ro.Property(r => r.Source).HasMaxLength(32).HasColumnName("bank_reference_source");
        });

        builder.OwnsOne(t => t.MciReference, ro =>
        {
            ro.Property(r => r.Value).HasMaxLength(128).HasColumnName("mci_reference_value");
            ro.Property(r => r.Source).HasMaxLength(32).HasColumnName("mci_reference_source");
        });

        // ----- Child entities: owned, single-instance reverse record. -----
        builder.OwnsOne(t => t.Reverse, ro =>
        {
            ro.ToTable("topup_reversals");
            ro.Property(r => r.Id).HasColumnName("id");
            ro.Property(r => r.TopupId).HasColumnName("topup_id");
            ro.Property(r => r.Reason).HasConversion<string>().HasMaxLength(48).HasColumnName("reason");
            ro.Property(r => r.RequestedBy).HasMaxLength(128).HasColumnName("requested_by");
            ro.Property(r => r.InitiatedAtUtc).HasColumnName("initiated_at_utc");
            ro.Property(r => r.CompletedAtUtc).HasColumnName("completed_at_utc");
            ro.Property(r => r.Status).HasConversion<string>().HasMaxLength(24).HasColumnName("status");
            ro.Property(r => r.CreatedOnUtc).HasColumnName("created_on_utc");
            ro.Property(r => r.ModifiedOnUtc).HasColumnName("modified_on_utc");
            ro.Property(r => r.IsDeleted).HasColumnName("is_deleted");
            ro.OwnsOne(r => r.BankReversalReference!, brr =>
            {
                brr.Property(v => v.Value).HasMaxLength(128).HasColumnName("bank_reversal_reference_value");
                brr.Property(v => v.Source).HasMaxLength(32).HasColumnName("bank_reversal_reference_source");
            });
            ro.WithOwner().HasForeignKey(r => r.TopupId);
        });

        // ----- Child entities: owned collection of attempts. -----
        builder.OwnsMany(t => t.Attempts, ao =>
        {
            ao.ToTable("topup_attempts");
            ao.WithOwner().HasForeignKey(a => a.TopupId);
            ao.HasKey(a => a.Id);
            ao.Property(a => a.Id).HasColumnName("id");
            ao.Property(a => a.TopupId).HasColumnName("topup_id");
            ao.Property(a => a.AttemptNumber).HasColumnName("attempt_number");
            ao.Property(a => a.StartedAtUtc).HasColumnName("started_at_utc");
            ao.Property(a => a.FinishedAtUtc).HasColumnName("finished_at_utc");
            ao.Property(a => a.Status).HasConversion<string>().HasMaxLength(24).HasColumnName("status");
            ao.Property(a => a.ProviderReference).HasMaxLength(128).HasColumnName("provider_reference");
            ao.Property(a => a.ErrorMessage).HasMaxLength(2048).HasColumnName("error_message");
            ao.Property(a => a.HttpStatusCode).HasColumnName("http_status_code");
            ao.Property(a => a.CreatedOnUtc).HasColumnName("created_on_utc");
            ao.Property(a => a.ModifiedOnUtc).HasColumnName("modified_on_utc");
            ao.Property(a => a.IsDeleted).HasColumnName("is_deleted");
            ao.HasIndex(a => new { a.TopupId, a.AttemptNumber }).IsUnique();
        });

        // ----- Indexes -----
        builder.HasIndex(t => t.CorrelationId);
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.IsDeleted, t.CreatedOnUtc });

        // Soft-delete: filter out deleted rows from every query by default.
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
