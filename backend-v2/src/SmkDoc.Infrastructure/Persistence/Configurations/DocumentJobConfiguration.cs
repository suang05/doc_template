using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Converters;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentJobConfiguration : IEntityTypeConfiguration<DocumentJob>
{
    public void Configure(EntityTypeBuilder<DocumentJob> builder)
    {
        builder.ToTable("document_jobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new DocumentJobId(v));
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.Property(e => e.TemplateId)
            .HasConversion(id => id.Value, v => new TemplateId(v));
        builder.Property(e => e.TemplateSlug).IsRequired().HasMaxLength(DocumentJob.MaxTemplateSlugLength);
        builder.Property(e => e.CallerIdentity).IsRequired().HasMaxLength(DocumentJob.MaxCallerIdentityLength);
        builder.Property(e => e.IdempotencyKey).IsRequired(false).HasMaxLength(DocumentJob.MaxIdempotencyKeyLength);
        builder.Property(e => e.RequestFingerprint).IsRequired(false).HasMaxLength(128);
        builder.Property(e => e.OutputFormat)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion(
                f => f.Name,
                v => SmkDoc.Domain.Common.Enumeration.FromDisplayName<OutputFormat>(v));
        builder.Property(e => e.WebhookUrl)
            .IsRequired(false)
            .HasMaxLength(DocumentJob.MaxWebhookUrlLength)
            .HasConversion(
                u => u == null ? null : u.ToString(),
                v => string.IsNullOrWhiteSpace(v) ? null : new Uri(v, UriKind.Absolute));
        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(s => s.Name, v => DocumentJobStatus.FromName(v));
        builder.Property(e => e.LockedBy).IsRequired(false).HasMaxLength(128);
        builder.Property(e => e.ResultStorageKey).IsRequired(false).HasMaxLength(512);
        builder.Property(e => e.LastErrorCode).IsRequired(false).HasMaxLength(DocumentJob.MaxErrorCodeLength);
        builder.Property(e => e.LastErrorMessage).IsRequired(false).HasMaxLength(DocumentJob.MaxErrorMessageLength);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.Status, e.AvailableAt, e.CreatedAt });
        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(e => new { e.ProjectId, e.IdempotencyKey })
            .HasFilter("idempotency_key IS NOT NULL");

        builder.HasMany(e => e.Items)
            .WithOne(i => i.DocumentJob)
            .HasForeignKey(i => i.DocumentJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
