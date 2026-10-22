using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCaseTests
{
    private readonly ITemplateScannerService _scanner = Substitute.For<ITemplateScannerService>();
    private readonly ITemplateDraftCache _cache = Substitute.For<ITemplateDraftCache>();

    private ParseTemplateDraftUseCase CreateSut() => new(_scanner, _cache);

    [Fact]
    public async Task ExecuteAsync_WhenValidFile_ShouldStoreDraftAndReturnPlaceholders()
    {
        // Arrange
        var placeholders = new List<string> { "customer.name", "invoice.total" };
        _scanner.ScanPlaceholdersAsync(Arg.Any<Stream>(), ".docx", Arg.Any<CancellationToken>())
            .Returns(placeholders);
        _cache.StoreAsync(Arg.Any<TemplateDraftEntry>(), Arg.Any<CancellationToken>())
            .Returns("draft-abc");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-docx"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "invoice.docx"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-abc");
        result.Placeholders.Should().BeEquivalentTo(placeholders);
        await _cache.Received(1).StoreAsync(
            Arg.Is<TemplateDraftEntry>(e => e.FileExtension == ".docx" && e.FileName == "invoice.docx"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenScannerReturnsEmpty_ShouldStoreEntryWithEmptyPlaceholders()
    {
        // Arrange
        _scanner.ScanPlaceholdersAsync(Arg.Any<Stream>(), ".html", Arg.Any<CancellationToken>())
            .Returns([]);
        _cache.StoreAsync(Arg.Any<TemplateDraftEntry>(), Arg.Any<CancellationToken>())
            .Returns("draft-empty");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "empty.html"), CancellationToken.None);

        // Assert
        result.Placeholders.Should().BeEmpty();
        result.DraftId.Should().Be("draft-empty");
    }
}
