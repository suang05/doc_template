using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class TemplateDatasetConfiguration : IEntityTypeConfiguration<TemplateDataset>
{
    public void Configure(EntityTypeBuilder<TemplateDataset> builder)
    {
        builder.ToTable("template_datasets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => new { e.TemplateId, e.Alias }).IsUnique();
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.Value, v => new TemplateId(v));
        builder.Property(e => e.DatasetId)
            .HasConversion(id => id.Value, v => new DatasetId(v));
        builder.Property(e => e.Alias).IsRequired().HasMaxLength(50)
            .HasConversion(a => a.Value, v => DatasetAlias.Create(v));

        builder.HasOne(e => e.Template)
            .WithMany(t => t.TemplateDatasets)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Dataset>()
            .WithMany()
            .HasForeignKey(e => e.DatasetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
