using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public class ParseTemplateDraftUseCaseTests
{
    private readonly Mock<ITemplateScannerService> _scanner = new();
    private readonly Mock<ITemplateDraftCache> _cache = new();

    private ParseTemplateDraftUseCase CreateSut() => new(_scanner.Object, _cache.Object);

    [Fact]
    public async Task ParseAsync_ValidFile_StoresDraftAndReturnsPlaceholders()
    {
        // Arrange
        var placeholders = new List<string> { "customer.name", "invoice.total" };
        _scanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(placeholders);
        _cache.Setup(c => c.StoreAsync(It.IsAny<TemplateDraftEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("draft-abc");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-docx"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "invoice.docx"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-abc");
        result.Placeholders.Should().BeEquivalentTo(placeholders);
        _cache.Verify(c => c.StoreAsync(It.Is<TemplateDraftEntry>(e =>
            e.FileExtension == ".docx" && e.FileName == "invoice.docx"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParseAsync_ScannerReturnsEmpty_StoresEntryWithEmptyPlaceholders()
    {
        // Arrange
        _scanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".html", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _cache.Setup(c => c.StoreAsync(It.IsAny<TemplateDraftEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("draft-empty");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "empty.html"), CancellationToken.None);

        // Assert
        result.Placeholders.Should().BeEmpty();
        result.DraftId.Should().Be("draft-empty");
    }
}
