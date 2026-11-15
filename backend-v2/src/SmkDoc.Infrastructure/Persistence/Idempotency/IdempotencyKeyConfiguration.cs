using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SmkDoc.Infrastructure.Persistence.Idempotency;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ProjectId).IsRequired(false);
        builder.Property(e => e.TenantScopeId)
            .HasComputedColumnSql("COALESCE(\"ProjectId\", '00000000-0000-0000-0000-000000000000'::uuid)", stored: true);
        builder.Property(e => e.CredentialId).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Scope).IsRequired().HasMaxLength(160);
        builder.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(e => e.RequestFingerprint).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ResponseJson).IsRequired(false).HasColumnType("jsonb");
        builder.Property(e => e.ResponseHeadersJson).IsRequired(false).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.TenantScopeId, e.CredentialId, e.Scope, e.IdempotencyKey })
            .HasDatabaseName("UQ_idempotency_keys_scope")
            .IsUnique();

        builder.HasIndex(e => e.ExpiresAt);
    }
}
