using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SmkDoc.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.AvailableAt).IsRequired();
        builder.Property(e => e.LockedBy).IsRequired(false).HasMaxLength(128);
        builder.Property(e => e.LastError).IsRequired(false).HasMaxLength(2000);
        builder.Property(e => e.Attempts).HasDefaultValue(0);

        builder.HasIndex(e => new { e.AvailableAt, e.OccurredAt })
            .HasDatabaseName("ix_outbox_messages_unprocessed")
            .HasFilter("processed_at IS NULL AND dead_lettered_at IS NULL");
    }
}
