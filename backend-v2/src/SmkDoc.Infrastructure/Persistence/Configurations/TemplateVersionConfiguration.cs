using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.Value, v => new TemplateId(v));
        builder.HasIndex(e => new { e.TemplateId, e.Version }).IsUnique();
        builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Status)
            .HasConversion(s => s.Id, v => TemplateVersionStatus.FromValue<TemplateVersionStatus>(v))
            .HasDefaultValue(TemplateVersionStatus.Draft);
        builder.Property(e => e.FileFormat)
            .HasConversion(f => f == null ? null : f.Name,
                v => v == null ? null : TemplateFormat.FromDisplayName<TemplateFormat>(v))
            .HasMaxLength(10);
        builder.Property(e => e.DataSchema).HasColumnType("jsonb");
        builder.Property(e => e.SamplePayload).HasColumnType("jsonb");
        builder.Property(e => e.CommitMessage).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
