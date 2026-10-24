using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ProjectId(v));
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.Property(e => e.CompanyId)
            .HasConversion(id => id.Value, v => new CompanyId(v));
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200)
            .HasConversion(n => n.Value, v => ProjectName.Create(v));
        builder.Property(e => e.Slug).IsRequired().HasMaxLength(100)
            .HasConversion(Converters.DomainValueConverters.ProjectSlugConverter);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
