using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => new { e.DocumentId, e.Version }).IsUnique();
        builder.Property(e => e.DocumentId)
            .HasConversion(id => id.Value, v => new DocumentId(v));
        builder.Property(e => e.ChangeNote).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(e => e.TemplateVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<GenerationLog>()
            .WithMany()
            .HasForeignKey(e => e.GenerationLogId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
