using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class ProjectQuotaConfiguration : IEntityTypeConfiguration<ProjectQuota>
{
    public void Configure(EntityTypeBuilder<ProjectQuota> builder)
    {
        builder.ToTable("project_quotas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ProjectQuotaId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => e.ProjectId).IsUnique();

        builder.Property(e => e.MaxRequestsPerMinute).HasDefaultValue(ProjectQuota.DefaultMaxRequestsPerMinute);
        builder.Property(e => e.MaxConcurrentJobs).HasDefaultValue(ProjectQuota.DefaultMaxConcurrentJobs);
        builder.Property(e => e.MaxBatchItems).HasDefaultValue(ProjectQuota.DefaultMaxBatchItems);
        builder.Property(e => e.MaxStorageBytes).HasDefaultValue(ProjectQuota.DefaultMaxStorageBytes);
        builder.Property(e => e.MaxActiveApiKeys).HasDefaultValue(ProjectQuota.DefaultMaxActiveApiKeys);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
