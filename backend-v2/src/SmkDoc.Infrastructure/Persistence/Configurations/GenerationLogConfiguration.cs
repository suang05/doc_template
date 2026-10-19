using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class GenerationLogConfiguration : IEntityTypeConfiguration<GenerationLog>
{
    public void Configure(EntityTypeBuilder<GenerationLog> builder)
    {
        builder.ToTable("generation_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.CreatedAt);
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, v => v.HasValue ? new TemplateId(v.Value) : null);
        builder.Property(e => e.ApiKeyId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, v => v.HasValue ? new ApiKeyId(v.Value) : null);
        builder.Property(e => e.CallerApp).HasMaxLength(50);
        builder.Property(e => e.TriggerSource).HasMaxLength(20);
        builder.Property(e => e.OutputKey).HasMaxLength(500);
        builder.Property(e => e.OutputFormat)
            .HasConversion(f => f == null ? null : f.Name,
                v => v == null ? null : OutputFormat.FromDisplayName<OutputFormat>(v))
            .HasMaxLength(10);
        builder.Property(e => e.PayloadHashSha256)
            .HasConversion(h => h == null ? null : h.Value, v => v == null ? null : new Sha256Hash(v))
            .HasMaxLength(64);
        builder.Property(e => e.Status)
            .HasConversion(s => s.Name, v => GenerationStatus.FromName(v))
            .IsRequired()
            .HasMaxLength(20);
        builder.Property(e => e.InputData).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(e => e.TemplateVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApiKey>()
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
