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

        builder.Property(e => e.KeyPrefix)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("smk_live");

        builder.Property(e => e.KeyId)
            .IsRequired(false)
            .HasMaxLength(12);

        builder.HasIndex(e => e.KeyId)
            .IsUnique()
            .HasFilter("key_id IS NOT NULL");

        builder.Property(e => e.KeySuffix)
            .IsRequired(false)
            .HasMaxLength(4);

        builder.Property(e => e.SecretHash)
            .HasColumnName("secret_hash_bytes")
            .HasColumnType("bytea")
            .IsRequired(false)
            .HasConversion(
                v => v.HasValue ? v.Value.ToArray() : null,
                b => b == null ? null : new ReadOnlyMemory<byte>(b));

        builder.Property(e => e.KeyHash)
            .IsRequired(false)
            .HasMaxLength(255)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => string.IsNullOrWhiteSpace(v) ? null : new Sha256Hash(v));

        builder.Property(e => e.Expiration)
            .HasColumnName("expires_at")
            .IsRequired(false)
            .HasConversion(
                v => v != null ? v.ExpiresAt : null,
                v => ExpirationPolicy.FromExisting(v));

        builder.Property(e => e.Scope)
            .HasConversion(s => s.Name, v => ApiKeyScope.FromDisplayName<ApiKeyScope>(v))
            .HasMaxLength(20)
            .HasDefaultValue(ApiKeyScope.ReadWrite);

        builder.Ignore(e => e.IsRevoked);
        builder.Ignore(e => e.HasSecretHash);
        builder.Ignore(e => e.ExpiresAt);

        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
