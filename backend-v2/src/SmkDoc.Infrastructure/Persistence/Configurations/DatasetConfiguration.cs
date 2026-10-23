using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.ToTable("datasets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new DatasetId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => e.ProjectId);
        builder.Property(e => e.DataConnectionId)
            .HasConversion(id => id.Value, v => new DataConnectionId(v));
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200)
            .HasConversion(v => v.Value, v => DatasetName.Create(v));
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.SqlQuery).IsRequired().HasColumnType("text")
            .HasConversion(DomainValueConverters.SqlQueryConverter);
        builder.Property(e => e.CacheSeconds).HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.DataConnection)
            .WithMany()
            .HasForeignKey(e => e.DataConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
