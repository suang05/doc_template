using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;
using SmkDoc.Application.Modules.Authoring.Fonts.Services;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Queries;

public sealed class GetFontCatalogUseCaseTests
{
    private readonly IFontCatalogService _fontCatalogService = Substitute.For<IFontCatalogService>();

    [Fact]
    public async Task ExecuteAsync_ShouldDelegateToFontCatalogService()
    {
        // Arrange
        var expectedCatalog = new FontCatalogDto(
            GoogleFonts: [new FontDescriptorDto("Sarabun", "Sarabun", FontSource.GoogleFonts)],
            CustomFonts: [new FontDescriptorDto("CompanyFont", "CompanyFont (Custom)", FontSource.MinIO, "CompanyFont.ttf")],
            AllFontNames: ["CompanyFont.ttf", "Sarabun"]
        );

        _fontCatalogService.GetCatalogAsync(Arg.Any<CancellationToken>())
            .Returns(expectedCatalog);

        var sut = new GetFontCatalogUseCase(_fontCatalogService);

        // Act
        var result = await sut.ExecuteAsync(new GetFontCatalogQuery());

        // Assert
        result.Should().Be(expectedCatalog);
        await _fontCatalogService.Received(1).GetCatalogAsync(Arg.Any<CancellationToken>());
    }
}

