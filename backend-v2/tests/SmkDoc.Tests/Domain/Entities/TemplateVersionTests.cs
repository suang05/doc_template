using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class TemplateVersionTests
{
    private static readonly DateTimeOffset Now = TestConstants.BaselineTime;

    private static TemplateVersion CreateArchivedVersion()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", Now);
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
    public void RenderEngineType_MapsFormatToEngine(TemplateFormat? format, RenderEngineType expectedEngine)
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "", format, null, Now);
        version.RenderEngineType.Should().Be(expectedEngine);
    }

    // ── Default property values ────────────────────────────────────────────

    [Fact]
    public void NewTemplateVersion_ShouldHaveUniqueId()
    {
        var v1 = TemplateVersion.Draft(TemplateId.New(), 1, "", null, null, Now);
        var v2 = TemplateVersion.Draft(TemplateId.New(), 1, "", null, null, Now);
        v1.Id.Should().NotBe(v2.Id);
        v1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewTemplateVersion_ShouldDefaultToDraftStatus()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "", null, null, Now);
        version.Status.Should().Be(TemplateVersionStatus.Draft);
    }

    [Fact]
    public void NewTemplateVersion_StorageKey_ShouldDefaultToEmpty()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "", null, null, Now);
        version.StorageKey.Should().NotBeNull();
        version.StorageKey.Should().BeEmpty();
    }

    // ── Lifecycle and Immutability ─────────────────────────────────────────

    [Fact]
    public void Publish_WhenDraft_SetsPublishedAndAuditTimestamp()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", Now);
        var pubTime = Now.AddHours(1);

        version.Publish(pubTime);

        version.Status.Should().Be(TemplateVersionStatus.Published);
        version.UpdatedAt.Should().Be(pubTime);
    }

    [Fact]
    public void Archive_WhenDraftOrPublished_SetsArchivedAndAuditTimestamp()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", Now);
        var archTime = Now.AddHours(2);

        version.Archive(archTime);

        version.Status.Should().Be(TemplateVersionStatus.Archived);
        version.UpdatedAt.Should().Be(archTime);
    }

    [Fact]
    public void UpdateStorageKey_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateStorageKey("new-key", Now.AddMinutes(5));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateDataSchema_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateDataSchema("{}", "{}", Now.AddMinutes(5));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void UpdateMappingsSnapshot_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdateMappingsSnapshot("[]", Now.AddMinutes(5));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void Publish_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.Publish(Now.AddMinutes(5));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Draft_WithInvalidVersionNumber_ThrowsDomainValidationException(int invalidVersion)
    {
        var act = () => TemplateVersion.Draft(TemplateId.New(), invalidVersion, "key", TemplateFormat.Html, "user", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void UpdatePageSettings_WhenDraft_SetsPageSettingsJsonAndGetPageSettingsReturnsMatchingObject()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", Now);
        var settings = new TemplatePageSettings(
            PaperSize: "A3",
            Orientation: "landscape",
            MarginTopMm: 20.0,
            MarginBottomMm: 20.0,
            MarginLeftMm: 15.0,
            MarginRightMm: 15.0,
            ShowPageNumber: false,
            HeaderHtml: "<div>Header</div>",
            FooterHtml: "<div>Footer</div>");

        version.UpdatePageSettings(settings, Now.AddMinutes(5));

        version.PageSettingsJson.Should().NotBeNullOrEmpty();
        var retrieved = version.GetPageSettings();
        retrieved.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void GetPageSettings_WhenPageSettingsJsonIsNull_ReturnsDefaultSettings()
    {
        var version = TemplateVersion.Draft(TemplateId.New(), 1, "key", TemplateFormat.Html, "author", Now);
        version.GetPageSettings().Should().BeEquivalentTo(TemplatePageSettings.Default);
    }

    [Fact]
    public void UpdatePageSettings_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = CreateArchivedVersion();
        var act = () => version.UpdatePageSettings(new TemplatePageSettings(), Now.AddMinutes(5));

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }
}
