using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentJobItemConfiguration : IEntityTypeConfiguration<DocumentJobItem>
{
    public void Configure(EntityTypeBuilder<DocumentJobItem> builder)
    {
        builder.ToTable("document_job_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.DocumentJobId)
            .HasConversion(id => id.Value, v => new DocumentJobId(v));
        builder.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.DocumentRef).IsRequired(false).HasMaxLength(DocumentJobItem.MaxDocumentRefLength);
        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(s => s.Name, v => DocumentJobStatus.FromName(v));
        builder.Property(e => e.OutputStorageKey).IsRequired(false).HasMaxLength(DocumentJobItem.MaxStorageKeyLength);
        builder.Property(e => e.ErrorCode).IsRequired(false).HasMaxLength(DocumentJobItem.MaxErrorCodeLength);
        builder.Property(e => e.ErrorMessage).IsRequired(false).HasMaxLength(DocumentJobItem.MaxErrorMessageLength);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.DocumentJobId, e.ItemIndex }).IsUnique();
    }
}
