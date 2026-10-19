using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DataConnectionConfiguration : IEntityTypeConfiguration<DataConnection>
{
    public void Configure(EntityTypeBuilder<DataConnection> builder)
    {
        builder.ToTable("data_connections");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new DataConnectionId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => e.ProjectId);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100)
            .HasConversion(v => v.Value, v => ConnectionName.Create(v));
        builder.Property(e => e.Provider).IsRequired().HasMaxLength(50)
            .HasConversion(p => p.Name, v => SmkDoc.Domain.Common.Enumeration.FromDisplayName<DatabaseProvider>(v));
        builder.Property(e => e.EncryptedConnectionString).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
