using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

public sealed class PreviewTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftCache _cache = Substitute.For<ITemplateDraftCache>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();

    private PreviewTemplateDraftUseCase CreateSut() => new(_cache, [_engine]);

    [Fact]
    public async Task ExecuteAsync_WhenValidDraftId_ShouldRenderStreamWithoutSideEffects()
    {
        // Arrange
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-preview");
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("content"), "t.docx", ".docx", ["field"]);

        _cache.GetAsync("draft-1", Arg.Any<CancellationToken>()).Returns(entry);
        _engine.EngineType.Returns(RenderEngineType.Docx);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(pdfBytes));

        // Act
        await using var resultStream = await CreateSut().ExecuteAsync(new PreviewTemplateDraftQuery("draft-1", "{\"field\":\"val\"}"), CancellationToken.None);

        // Assert
        resultStream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await resultStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(pdfBytes);

        // Zero side-effects — no cache eviction:
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDraftNotFound_ShouldThrowDraftExpiredException()
    {
        // Arrange
        _cache.GetAsync("gone", Arg.Any<CancellationToken>()).Returns((TemplateDraftEntry?)null);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new PreviewTemplateDraftQuery("gone", "{}"), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }
}
