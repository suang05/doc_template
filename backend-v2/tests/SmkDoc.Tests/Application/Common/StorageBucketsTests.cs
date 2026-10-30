using FluentAssertions;
using SmkDoc.Application.Common;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Application.Common;

public sealed class StorageBucketsTests
{
    [Fact]
    public void Templates_ShouldBeExactly_templates()
    {
        StorageBuckets.Templates.Should().Be("templates");
    }

    [Fact]
    public void Outputs_ShouldBeExactly_outputs()
    {
        StorageBuckets.Outputs.Should().Be("outputs");
    }

    [Fact]
    public void AuditPayloads_ShouldBeExactly_audit_payloads()
    {
        StorageBuckets.AuditPayloads.Should().Be("audit-payloads");
    }

    [Fact]
    public void Fonts_ShouldBeExactly_fonts()
    {
        StorageBuckets.Fonts.Should().Be("fonts");
    }

    [Fact]
    public void AllBucketNames_ShouldBeLowercase()
    {
        StorageBuckets.Templates.Should().Be(StorageBuckets.Templates.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
        StorageBuckets.Outputs.Should().Be(StorageBuckets.Outputs.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
        StorageBuckets.AuditPayloads.Should().Be(StorageBuckets.AuditPayloads.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
        StorageBuckets.Fonts.Should().Be(StorageBuckets.Fonts.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
    }

    [Fact]
    public void AllBucketNames_ShouldBeDistinct()
    {
        var names = new[] { StorageBuckets.Templates, StorageBuckets.Outputs, StorageBuckets.AuditPayloads, StorageBuckets.Fonts };
        names.Should().OnlyHaveUniqueItems("bucket names must not collide");
    }

    [Fact]
    public void TemplateVersionKey_ShouldFormatCanonicalImmutableKey()
    {
        var projectId = ProjectId.New();
        var templateId = TemplateId.New();

        StorageBuckets.TemplateVersionKey(projectId, templateId, 3, ".HTML")
            .Should().Be($"templates/{projectId.Value:N}/{templateId.Value:N}/v3.html");
        StorageBuckets.TemplateVersionKey(projectId, templateId, 1, "docx")
            .Should().Be($"templates/{projectId.Value:N}/{templateId.Value:N}/v1.docx");
    }

    [Fact]
    public void TenantFontKey_ShouldFormatTenantPartitionedFontKey()
    {
        var projectId = ProjectId.New();

        StorageBuckets.TenantFontKey(projectId, "Sarabun_Bold.ttf")
            .Should().Be($"fonts/{projectId.Value:N}/Sarabun_Bold.ttf");
    }
}
