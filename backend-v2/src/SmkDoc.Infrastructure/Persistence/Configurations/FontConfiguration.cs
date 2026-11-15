using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class FontConfiguration : IEntityTypeConfiguration<Font>
{
    public void Configure(EntityTypeBuilder<Font> builder)
    {
        builder.ToTable("fonts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new FontId(v));
        builder.Property(e => e.FamilyName).IsRequired().HasMaxLength(Font.MaxFamilyNameLength);
        builder.Property(e => e.Weight).IsRequired().HasMaxLength(Font.MaxWeightLength);
        builder.Property(e => e.Style).IsRequired().HasMaxLength(Font.MaxStyleLength);
        builder.Property(e => e.Format).IsRequired().HasMaxLength(Font.MaxFormatLength);
        builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(Font.MaxStorageKeyLength);
        builder.Property(e => e.ChecksumSha256).IsRequired().HasMaxLength(64)
            .HasConversion(h => h.Value, v => new Sha256Hash(v));
        builder.Property(e => e.IsSystemFont).HasDefaultValue(false);
        builder.Property(e => e.ProjectId)
            .IsRequired(false)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new ProjectId(v.Value) : null);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.ProjectId, e.FamilyName, e.Weight, e.Style });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
