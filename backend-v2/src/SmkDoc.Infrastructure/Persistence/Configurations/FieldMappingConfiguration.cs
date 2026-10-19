using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class FieldMappingConfiguration : IEntityTypeConfiguration<FieldMapping>
{
    public void Configure(EntityTypeBuilder<FieldMapping> builder)
    {
        builder.ToTable("field_mappings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.Value, v => new TemplateId(v));
        builder.HasIndex(e => new { e.TemplateId, e.Placeholder }).IsUnique();
        builder.Property(e => e.Placeholder).IsRequired().HasMaxLength(100);
        builder.Property(e => e.SourcePath).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Transform).HasMaxLength(50);
        builder.Property(e => e.DataSourceType)
            .HasConversion(d => d.Value, v => DataSourceType.FromString(v))
            .HasMaxLength(20).HasDefaultValue(DataSourceType.Json);
        builder.Property(e => e.DatasetAlias).HasMaxLength(50)
            .HasConversion(a => a == null ? null : a.Value, v => v == null ? null : DatasetAlias.Create(v));
        builder.Property(e => e.ResultPath).HasMaxLength(300);
        builder.Property(e => e.MathExpression).HasMaxLength(500);

        builder.HasOne(e => e.Template)
            .WithMany(t => t.FieldMappings)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
