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
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", TemplateFormat.Xlsx, null, now);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Excel);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsDocx_ReturnsDocx()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", TemplateFormat.Docx, null, now);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Docx);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsHtml_ReturnsHtml()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", TemplateFormat.Html, null, now);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsPdf_ReturnsHtml()
    {
        // Pdf does not have its own render engine — falls back to Html
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", TemplateFormat.Pdf, null, now);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    [Fact]
    public void GetRenderEngineType_WhenFileFormatIsNull_ReturnsHtml()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, now);
        version.GetRenderEngineType().Should().Be(RenderEngineType.Html);
    }

    // ── Default property values ────────────────────────────────────────────

    [Fact]
    public void NewTemplateVersion_ShouldHaveUniqueId()
    {
        var now = DateTimeOffset.UtcNow;
        var v1 = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, now);
        var v2 = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, now);
        v1.Id.Should().NotBe(v2.Id);
        v1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewTemplateVersion_ShouldDefaultToDraftStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, now);
        version.Status.Should().Be(TemplateVersionStatus.Draft);
    }

    [Fact]
    public void NewTemplateVersion_StorageKey_ShouldDefaultToEmpty()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, now);
        version.StorageKey.Should().NotBeNull();
        version.StorageKey.Should().BeEmpty();
    }

    // ── Lifecycle and Immutability ─────────────────────────────────────────

    [Fact]
    public void Publish_WhenDraft_SetsPublishedAndAuditTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        var pubTime = now.AddHours(1);

        version.Publish(pubTime);

        version.Status.Should().Be(TemplateVersionStatus.Published);
        version.UpdatedAt.Should().Be(pubTime);
    }

    [Fact]
    public void Archive_WhenDraftOrPublished_SetsArchivedAndAuditTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        var archTime = now.AddHours(2);

        version.Archive(archTime);

        version.Status.Should().Be(TemplateVersionStatus.Archived);
        version.UpdatedAt.Should().Be(archTime);
    }

    [Fact]
    public void UpdateStorageKey_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        version.Archive(now);

        var act = () => version.UpdateStorageKey("new-key", DateTimeOffset.UtcNow);

        var ex = act.Should().Throw<SmkDoc.Domain.Exceptions.ArchivedVersionImmutableException>();
        ex.Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateDataSchema_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        version.Archive(now);

        var act = () => version.UpdateDataSchema("{}", "{}", DateTimeOffset.UtcNow);

        var ex = act.Should().Throw<SmkDoc.Domain.Exceptions.ArchivedVersionImmutableException>();
        ex.Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateMappingsSnapshot_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        version.Archive(now);

        var act = () => version.UpdateMappingsSnapshot("[]", DateTimeOffset.UtcNow);

        var ex = act.Should().Throw<SmkDoc.Domain.Exceptions.ArchivedVersionImmutableException>();
        ex.Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void Publish_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", now);
        version.Archive(now);

        var act = () => version.Publish(DateTimeOffset.UtcNow);

        var ex = act.Should().Throw<SmkDoc.Domain.Exceptions.ArchivedVersionImmutableException>();
        ex.Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Draft_WithInvalidVersionNumber_ThrowsDomainValidationException(int invalidVersion)
    {
        var act = () => TemplateVersion.Draft(Guid.NewGuid(), invalidVersion, "key", TemplateFormat.Html, "user", DateTimeOffset.UtcNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }
}
