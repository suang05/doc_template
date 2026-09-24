using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests;

public class TemplateVersionTests
{
    // ── GetRenderEngineType ────────────────────────────────────────────────

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsXlsx_ReturnsExcel()
    {
        var version = new TemplateVersion { FileFormat = TemplateFormat.Xlsx };
        version.GetRenderEngineType().Should().Be(RenderEngineType.Excel);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsDocx_ReturnsDocx()
    {
        var version = new TemplateVersion { FileFormat = TemplateFormat.Docx };
        version.GetRenderEngineType().Should().Be(RenderEngineType.Docx);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsHtml_ReturnsHtml()
    {
        var version = new TemplateVersion { FileFormat = TemplateFormat.Html };
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsPdf_ReturnsHtml()
    {
        // Pdf does not have its own render engine — falls back to Html
        var version = new TemplateVersion { FileFormat = TemplateFormat.Pdf };
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsNull_ReturnsHtml()
    {
        var version = new TemplateVersion { FileFormat = null };
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    // ── Default property values ────────────────────────────────────────────

    [Fact]
    public void NewTemplateVersion_ShouldHaveUniqueId()
    {
        var v1 = new TemplateVersion();
        var v2 = new TemplateVersion();
        v1.Id.Should().NotBe(v2.Id);
        v1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewTemplateVersion_ShouldDefaultToDraftStatus()
    {
        var version = new TemplateVersion();
        version.Status.Should().Be(TemplateVersionStatus.Draft);
    }

    [Fact]
    public void NewTemplateVersion_StorageKey_ShouldDefaultToEmpty()
    {
        var version = new TemplateVersion();
        version.StorageKey.Should().NotBeNull();
        version.StorageKey.Should().BeEmpty();
    }

    // ── GetRenderEngineType is deterministic ───────────────────────────────

    [Theory]
    [InlineData(TemplateFormat.Html,  RenderEngineType.Html)]
    [InlineData(TemplateFormat.Docx,  RenderEngineType.Docx)]
    [InlineData(TemplateFormat.Xlsx,  RenderEngineType.Excel)]
    [InlineData(TemplateFormat.Pdf,   RenderEngineType.Html)]
    public void GetRenderEngineType_AllFormats_MapsCorrectly(TemplateFormat format, RenderEngineType expected)
    {
        var version = new TemplateVersion { FileFormat = format };
        version.GetRenderEngineType().Should().Be(expected);
    }
}
