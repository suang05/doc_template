using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new DocumentId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => new { e.ProjectId, e.DocumentRef }).IsUnique();
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, v => v.HasValue ? new TemplateId(v.Value) : null);
        builder.Property(e => e.DocumentRef)
            .HasConversion(d => d.Value, v => DocumentReference.Create(v))
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
