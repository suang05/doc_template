using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class BreakGlassGrantConfiguration : IEntityTypeConfiguration<BreakGlassGrant>
{
    public void Configure(EntityTypeBuilder<BreakGlassGrant> builder)
    {
        builder.ToTable("break_glass_grants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new BreakGlassGrantId(v));
        builder.Property(e => e.TargetProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.Property(e => e.SuperAdminUserId)
            .HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(e => e.GrantedByUserId)
            .HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(BreakGlassGrant.MaxReasonLength);
        builder.Property(e => e.GrantedAt).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.RevokedAt).IsRequired(false);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.SuperAdminUserId, e.TargetProjectId, e.ExpiresAt });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.TargetProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.SuperAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
