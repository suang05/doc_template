using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new UserId(v));
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.Email).IsRequired().HasMaxLength(255)
            .HasConversion(m => m.Value, v => EmailAddress.Create(v));
        builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
        builder.Property(e => e.FirstName).HasMaxLength(100);
        builder.Property(e => e.LastName).HasMaxLength(100);
        builder.Property(e => e.SystemRole)
            .HasConversion(r => r.Name, v => SystemRole.FromDisplayName<SystemRole>(v))
            .HasMaxLength(30)
            .HasDefaultValue(SystemRole.Member);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
