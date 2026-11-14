using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Queries;

public sealed class GetFontCatalogUseCaseTests
{
    private readonly IFontCatalogPort _fontCatalogPort = Substitute.For<IFontCatalogPort>();

    [Fact]
    public async Task ExecuteAsync_ShouldDelegateToFontCatalogPort()
    {
        // Arrange
        var expectedCatalog = new FontCatalogDto(
            GoogleFonts: [new FontDescriptorDto("Sarabun", "Sarabun", FontSource.GoogleFonts)],
            CustomFonts: [new FontDescriptorDto("CompanyFont", "CompanyFont (Custom)", FontSource.MinIO, "CompanyFont.ttf")],
            AllFontNames: ["CompanyFont.ttf", "Sarabun"]
        );

        _fontCatalogPort.GetCatalogAsync(Arg.Any<CancellationToken>())
            .Returns(expectedCatalog);

        var sut = new GetFontCatalogUseCase(_fontCatalogPort);

        // Act
        var result = await sut.ExecuteAsync(new GetFontCatalogQuery(), CancellationToken.None);

        // Assert
        result.Should().Be(expectedCatalog);
        await _fontCatalogPort.Received(1).GetCatalogAsync(Arg.Any<CancellationToken>());
    }
}

