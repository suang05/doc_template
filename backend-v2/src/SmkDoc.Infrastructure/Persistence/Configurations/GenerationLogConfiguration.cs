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
        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(e => new { e.ProjectId, e.CallerApp, e.CreatedAt })
            .IsDescending(false, false, true);

        ConfigureScalarProperties(builder);
        ConfigureValueConversions(builder);
        ConfigureForeignKeys(builder);
    }

    private static void ConfigureScalarProperties(EntityTypeBuilder<GenerationLog> builder)
    {
        builder.Property(e => e.CallerApp).HasMaxLength(50);
        builder.Property(e => e.TriggerSource).HasMaxLength(20);
        builder.Property(e => e.OutputKey).HasMaxLength(500);
        builder.Property(e => e.InputData).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
    }

    private static void ConfigureValueConversions(EntityTypeBuilder<GenerationLog> builder)
    {
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.Property(e => e.TemplateId)
            .HasConversion(id => ToNullableGuid(id), v => ToNullableTemplateId(v));
        builder.Property(e => e.ApiKeyId)
            .HasConversion(id => ToNullableGuid(id), v => ToNullableApiKeyId(v));
        builder.Property(e => e.OutputFormat)
            .HasConversion(f => FormatToName(f), v => ParseOutputFormat(v))
            .HasMaxLength(10);
        builder.Property(e => e.PayloadHashSha256)
            .HasConversion(h => HashToString(h), v => ParseSha256Hash(v))
            .HasMaxLength(64);
        builder.Property(e => e.Status)
            .HasConversion(s => s.Name, v => GenerationStatus.FromName(v))
            .IsRequired()
            .HasMaxLength(20);
    }

    private static void ConfigureForeignKeys(EntityTypeBuilder<GenerationLog> builder)
    {
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

    private static Guid? ToNullableGuid(TemplateId? id) => id?.Value;
    private static Guid? ToNullableGuid(ApiKeyId? id) => id?.Value;
    private static TemplateId? ToNullableTemplateId(Guid? v) => v.HasValue ? new TemplateId(v.Value) : null;
    private static ApiKeyId? ToNullableApiKeyId(Guid? v) => v.HasValue ? new ApiKeyId(v.Value) : null;
    private static string? FormatToName(OutputFormat? f) => f?.Name;
    private static OutputFormat? ParseOutputFormat(string? v) => v == null ? null : OutputFormat.FromDisplayName<OutputFormat>(v);
    private static string? HashToString(Sha256Hash? h) => h?.Value;
    private static Sha256Hash? ParseSha256Hash(string? v) => v == null ? null : new Sha256Hash(v);
}
