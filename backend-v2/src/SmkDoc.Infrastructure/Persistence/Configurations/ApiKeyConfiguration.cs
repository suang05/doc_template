using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ApiKeyId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v));
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100)
            .HasConversion(v => v.Value, v => ApiKeyName.Create(v));
        builder.Property(e => e.CallerApp).IsRequired().HasMaxLength(50);
        builder.Property(e => e.KeyHash).IsRequired().HasMaxLength(255)
            .HasConversion(v => v.Value, v => new Sha256Hash(v));
        builder.Property(e => e.Expiration)
            .HasColumnName("expires_at")
            .HasConversion(v => v.ExpiresAt, v => ExpirationPolicy.FromExisting(v));
        builder.Property(e => e.Scope)
            .HasConversion(s => s.Name, v => ApiKeyScope.FromDisplayName<ApiKeyScope>(v))
            .HasMaxLength(20)
            .HasDefaultValue(ApiKeyScope.ReadWrite);
        builder.Ignore(e => e.IsRevoked);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
