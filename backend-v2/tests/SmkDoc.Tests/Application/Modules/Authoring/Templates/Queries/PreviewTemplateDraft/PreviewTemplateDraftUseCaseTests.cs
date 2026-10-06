using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

public class PreviewTemplateDraftUseCaseTests
{
    private readonly Mock<ITemplateDraftCache> _cache = new();
    private readonly Mock<IRenderEngine> _engine = new();

    private PreviewTemplateDraftUseCase CreateSut() => new(_cache.Object, [_engine.Object]);

    [Fact]
    public async Task PreviewAsync_ValidDraftId_RendersWithoutSideEffects()
    {
        // Arrange
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-preview");
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("content"), "t.docx", ".docx", ["field"]);

        _cache.Setup(c => c.GetAsync("draft-1", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _engine.Setup(e => e.EngineType).Returns(RenderEngineType.Docx);
        _engine.Setup(e =>
                e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await CreateSut().ExecuteAsync(new PreviewTemplateDraftQuery("draft-1", "{\"field\":\"val\"}"), CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(pdfBytes);
        // Zero side-effects — no cache eviction:
        _cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_DraftNotFound_ThrowsDraftExpiredException()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync("gone", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new PreviewTemplateDraftQuery("gone", "{}"), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }
}
