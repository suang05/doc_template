using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Queries;

public sealed class FontQueriesUseCaseTests
{
    private readonly IFontCatalogPort _fontCatalog = Substitute.For<IFontCatalogPort>();

    [Fact]
    public async Task ListFonts_WhenResolved_ListsAllFontNamesFromCatalogPort()
    {
        // Arrange
        var catalogDto = new FontCatalogDto(
            [],
            [],
            ["Kanit-Bold.ttf", "Prompt-Regular.woff2", "Sarabun-Regular.ttf"]);
        _fontCatalog.GetCatalogAsync(Arg.Any<CancellationToken>())
            .Returns(catalogDto);

        var sut = new ListFontsUseCase(_fontCatalog);

        // Act
        var fonts = await sut.ExecuteAsync(new ListFontsQuery(), CancellationToken.None);

        // Assert
        fonts.Should().BeEquivalentTo(["Kanit-Bold.ttf", "Prompt-Regular.woff2", "Sarabun-Regular.ttf"]);
    }

    [Fact]
    public async Task GetFontBase64_WhenFontExists_DelegatesToFontCatalogPort()
    {
        // Arrange
        byte[] fontBytes = [0x00, 0x01, 0x00, 0x00, 0xAA, 0xBB];
        var expected = Convert.ToBase64String(fontBytes);
        _fontCatalog.GetFontBase64Async("Kanit-Bold.ttf", Arg.Any<CancellationToken>())
            .Returns(expected);

        var sut = new GetFontBase64UseCase(_fontCatalog);

        // Act
        var base64 = await sut.ExecuteAsync(new GetFontBase64Query("Kanit-Bold.ttf"), CancellationToken.None);

        // Assert
        base64.Should().Be(expected);
        await _fontCatalog.Received(1).GetFontBase64Async("Kanit-Bold.ttf", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FontQueries_WhenPortThrows_PropagateExceptions()
    {
        // Arrange
        _fontCatalog.GetCatalogAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TenantContextNotResolvedException());
        _fontCatalog.GetFontBase64Async("../other-tenant/Secret.ttf", Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainValidationException("Font file name must not contain path traversal segments."));

        var listSut = new ListFontsUseCase(_fontCatalog);
        var getSut = new GetFontBase64UseCase(_fontCatalog);

        // Act & Assert
        await listSut.Invoking(s => s.ExecuteAsync(new ListFontsQuery(), CancellationToken.None)).Should().ThrowAsync<TenantContextNotResolvedException>();
        await getSut.Invoking(s => s.ExecuteAsync(new GetFontBase64Query("../other-tenant/Secret.ttf"), CancellationToken.None))
            .Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*path traversal*");
    }
}
