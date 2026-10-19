using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(64)
            .HasConversion(h => h.Value, v => new Sha256Hash(v));
        builder.Property(e => e.ReplacedByTokenHash).HasMaxLength(64)
            .HasConversion(h => h == null ? null : h.Value, v => v == null ? null : new Sha256Hash(v));
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.UserId, e.ExpiresAt })
            .HasFilter("\"revoked_at\" IS NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
