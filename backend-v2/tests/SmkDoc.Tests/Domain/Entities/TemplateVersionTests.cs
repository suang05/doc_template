using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class TemplateVersionTests
{
    private static readonly DateTimeOffset Now = TestConstants.BaselineTime;

    private static TemplateVersion CreateArchivedVersion()
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", Now);
        version.Archive(Now);
        return version;
    }

    public static readonly TheoryData<TemplateFormat?, RenderEngineType> FormatEngineMappings = new()
    {
        { TemplateFormat.Xlsx, RenderEngineType.Excel },
        { TemplateFormat.Docx, RenderEngineType.Docx },
        { TemplateFormat.Html, RenderEngineType.Html },
        { TemplateFormat.Pdf, RenderEngineType.Html },
        { null, RenderEngineType.Html }
    };

    [Theory]
    [MemberData(nameof(FormatEngineMappings))]
    public void GetRenderEngineType_MapsFormatToEngine(TemplateFormat? format, RenderEngineType expectedEngine)
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", format, null, Now);
        version.GetRenderEngineType().Should().Be(expectedEngine);
    }

    // ── Default property values ────────────────────────────────────────────

    [Fact]
    public void NewTemplateVersion_ShouldHaveUniqueId()
    {
        var v1 = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, Now);
        var v2 = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, Now);
        v1.Id.Should().NotBe(v2.Id);
        v1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewTemplateVersion_ShouldDefaultToDraftStatus()
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, Now);
        version.Status.Should().Be(TemplateVersionStatus.Draft);
    }

    [Fact]
    public void NewTemplateVersion_StorageKey_ShouldDefaultToEmpty()
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "", null, null, Now);
        version.StorageKey.Should().NotBeNull();
        version.StorageKey.Should().BeEmpty();
    }

    // ── Lifecycle and Immutability ─────────────────────────────────────────

    [Fact]
    public void Publish_WhenDraft_SetsPublishedAndAuditTimestamp()
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", Now);
        var pubTime = Now.AddHours(1);

        version.Publish(pubTime);

        version.Status.Should().Be(TemplateVersionStatus.Published);
        version.UpdatedAt.Should().Be(pubTime);
    }

    [Fact]
    public void Archive_WhenDraftOrPublished_SetsArchivedAndAuditTimestamp()
    {
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "author", Now);
        var archTime = Now.AddHours(2);

        version.Archive(archTime);

        version.Status.Should().Be(TemplateVersionStatus.Archived);
        version.UpdatedAt.Should().Be(archTime);
    }

    [Fact]
    public void UpdateStorageKey_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateStorageKey("new-key", Now.AddMinutes(5));

        act.Should().Throw<ArchivedVersionImmutableException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateDataSchema_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateDataSchema("{}", "{}", Now.AddMinutes(5));

        act.Should().Throw<ArchivedVersionImmutableException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateMappingsSnapshot_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateMappingsSnapshot("[]", Now.AddMinutes(5));

        act.Should().Throw<ArchivedVersionImmutableException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void Publish_WhenArchived_ThrowsArchivedVersionImmutableException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.Publish(Now.AddMinutes(5));

        act.Should().Throw<ArchivedVersionImmutableException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Draft_WithInvalidVersionNumber_ThrowsDomainValidationException(int invalidVersion)
    {
        var act = () => TemplateVersion.Draft(Guid.NewGuid(), invalidVersion, "key", TemplateFormat.Html, "user", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }
}
