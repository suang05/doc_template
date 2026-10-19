using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new TemplateId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v));
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100)
            .HasConversion(v => v.Value, v => TemplateName.Create(v));
        builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();
        builder.Property(e => e.Slug)
            .HasConversion(s => s.Value, v => new TemplateSlug(v))
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.CurrentVersion)
            .WithMany()
            .HasForeignKey(e => e.CurrentVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Project>()
            .WithMany(p => p.Templates)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
