using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Engines.Fonts;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Engines.Fonts;

public sealed class FontCatalogServiceTests
{
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public FontCatalogServiceTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    private FontCatalogService CreateSut() => new(_storage, _projectContext);

    [Fact]
    public void GetCuratedGoogleFonts_ShouldContainStandardThaiFonts()
    {
        var sut = CreateSut();

        var googleFonts = sut.GetCuratedGoogleFonts();

        googleFonts.Should().NotBeEmpty();
        googleFonts.Should().Contain(f => f.FontFamily == "Sarabun" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Prompt" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Kanit" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Mitr" && f.Source == FontSource.GoogleFonts);
    }

    [Fact]
    public async Task GetCatalogAsync_WhenProjectResolved_ShouldCombineGoogleFontsAndMinIOFonts()
    {
        // Arrange
        var prefix = $"fonts/{_projectId.Value:N}/";
        _storage.ListObjectsAsync(StorageBuckets.Fonts, prefix, Arg.Any<CancellationToken>())
            .Returns([$"fonts/{_projectId.Value:N}/Corporate-Logo.ttf", $"fonts/{_projectId.Value:N}/Invoice-Sign.otf"]);

        var sut = CreateSut();

        // Act
        var catalog = await sut.GetCatalogAsync();

        // Assert
        catalog.GoogleFonts.Should().Contain(f => f.FontFamily == "Sarabun");
        catalog.CustomFonts.Should().HaveCount(2);
        catalog.CustomFonts.Should().Contain(f => f.FontFamily == "Corporate-Logo" && f.Source == FontSource.MinIO);
        catalog.CustomFonts.Should().Contain(f => f.FontFamily == "Invoice-Sign" && f.Source == FontSource.MinIO);
        catalog.AllFontNames.Should().Contain("Corporate-Logo.ttf");
        catalog.AllFontNames.Should().Contain("Invoice-Sign.otf");
        catalog.AllFontNames.Should().Contain("Sarabun");
        catalog.AllFontNames.Should().Contain("Sarabun-Regular.ttf");
    }

    [Fact]
    public async Task GetCatalogAsync_WhenProjectNotResolved_ShouldThrowTenantContextRequiredException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var sut = CreateSut();

        // Act
        var act = () => sut.GetCatalogAsync();

        // Assert
        await act.Should().ThrowAsync<TenantContextRequiredException>();
    }
}

