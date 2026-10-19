using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class UserProjectRoleConfiguration : IEntityTypeConfiguration<UserProjectRole>
{
    public void Configure(EntityTypeBuilder<UserProjectRole> builder)
    {
        builder.ToTable("user_project_roles");
        builder.HasKey(e => new { e.UserId, e.ProjectId });
        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v));
        builder.Property(e => e.Role)
            .HasConversion(r => r.Name, v => RoleType.FromDisplayName<RoleType>(v))
            .HasMaxLength(20);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<User>()
            .WithMany(u => u.ProjectRoles)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Project>()
            .WithMany(p => p.UserRoles)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
