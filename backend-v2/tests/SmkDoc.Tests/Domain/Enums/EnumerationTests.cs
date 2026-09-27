using FluentAssertions;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Domain.Enums;

public class EnumerationTests
{
    // ── Enumeration Static Cache & Lookup ───────────────────────────────────

    [Fact]
    public void Enumeration_FromValue_ReturnsExpectedInstance()
    {
        var format = TemplateFormat.FromValue<TemplateFormat>(1);
        format.Should().Be(TemplateFormat.Html);
    }

    [Fact]
    public void Enumeration_FromDisplayName_IsCaseInsensitive()
    {
        var lower = TemplateFormat.FromDisplayName<TemplateFormat>("docx");
        var upper = TemplateFormat.FromDisplayName<TemplateFormat>("DOCX");

        lower.Should().Be(TemplateFormat.Docx);
        upper.Should().Be(TemplateFormat.Docx);
    }

    [Fact]
    public void Enumeration_TryFromValue_WorksCorrectly()
    {
        var found = TemplateFormat.TryFromValue<TemplateFormat>(2, out var result);
        found.Should().BeTrue();
        result.Should().Be(TemplateFormat.Docx);

        var notFound = TemplateFormat.TryFromValue<TemplateFormat>(999, out var invalidResult);
        notFound.Should().BeFalse();
        invalidResult.Should().BeNull();
    }

    [Fact]
    public void Enumeration_TryFromDisplayName_WorksCorrectly()
    {
        var found = TemplateFormat.TryFromDisplayName<TemplateFormat>("xlsx", out var result);
        found.Should().BeTrue();
        result.Should().Be(TemplateFormat.Xlsx);

        var notFound = TemplateFormat.TryFromDisplayName<TemplateFormat>("unknown_format", out var invalidResult);
        notFound.Should().BeFalse();
        invalidResult.Should().BeNull();
    }

    // ── TemplateFormat & TemplateVersion OCP Mapping ────────────────────────

    [Fact]
    public void TemplateFormat_DefaultEngineType_IsConfiguredProperly()
    {
        TemplateFormat.Html.DefaultEngineType.Should().Be(RenderEngineType.Html);
        TemplateFormat.Docx.DefaultEngineType.Should().Be(RenderEngineType.Docx);
        TemplateFormat.Xlsx.DefaultEngineType.Should().Be(RenderEngineType.Excel);
        TemplateFormat.Pdf.DefaultEngineType.Should().Be(RenderEngineType.Html);
    }

    [Theory]
    [InlineData(".docx", true, "docx")]
    [InlineData("docx", true, "docx")]
    [InlineData(".XLSX", true, "xlsx")]
    [InlineData(".html", true, "html")]
    [InlineData(".pdf", true, "pdf")]
    [InlineData(".unknown", false, null)]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void TemplateFormat_TryFromExtension_ResolvesProperly(string? ext, bool expectedSuccess, string? expectedName)
    {
        var success = TemplateFormat.TryFromExtension(ext, out var format);
        success.Should().Be(expectedSuccess);

        if (expectedSuccess)
        {
            format.Should().NotBeNull();
            format!.Name.Should().Be(expectedName);
        }
        else
        {
            format.Should().BeNull();
        }
    }
}

