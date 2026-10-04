using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class TemplateVersionTests
{
    // ── GetRenderEngineType ────────────────────────────────────────────────

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsXlsx_ReturnsExcel()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", TemplateFormat.Xlsx, null);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Excel);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsDocx_ReturnsDocx()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", TemplateFormat.Docx, null);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Docx);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsHtml_ReturnsHtml()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", TemplateFormat.Html, null);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsPdf_ReturnsHtml()
    {
        // Pdf does not have its own render engine — falls back to Html
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", TemplateFormat.Pdf, null);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsNull_ReturnsHtml()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", null, null);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    // ── Default property values ────────────────────────────────────────────

    [Fact]
    public void NewTemplateVersion_ShouldHaveUniqueId()
    {
        var v1 = new TemplateVersion(Guid.NewGuid(), 1, "", null, null);
        var v2 = new TemplateVersion(Guid.NewGuid(), 1, "", null, null);
        v1.Id.Should().NotBe(v2.Id);
        v1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewTemplateVersion_ShouldDefaultToDraftStatus()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", null, null);
        version.Status.Should().Be(TemplateVersionStatus.Draft);
    }

    [Fact]
    public void NewTemplateVersion_StorageKey_ShouldDefaultToEmpty()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "", null, null);
        version.StorageKey.Should().NotBeNull();
        version.StorageKey.Should().BeEmpty();
    }

}
