using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

public sealed class PreviewTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftCache _cache = Substitute.For<ITemplateDraftCache>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();
    private readonly IRenderEngineFactory _engineFactory = Substitute.For<IRenderEngineFactory>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public PreviewTemplateDraftUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
        _engine.EngineType.Returns(RenderEngineType.Docx);
        _engineFactory.GetEngine(Arg.Any<RenderEngineType>()).Returns(_engine);
    }

    private PreviewTemplateDraftUseCase CreateSut() => new(_cache, _engineFactory, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenValidDraftId_ShouldRenderStreamWithoutSideEffects()
    {
        // Arrange
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-preview");
        var entry = new TemplateDraftEntry("draft-1", _projectId.Value, "drafts/t.docx", "t.docx", ".docx", ["field"]);

        _cache.GetAsync(_projectId, "draft-1", Arg.Any<CancellationToken>()).Returns(entry);
        _cache.OpenStreamAsync(entry, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("docx-stream")));
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
        await _cache.DidNotReceive().RemoveAsync(_projectId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDraftNotFound_ShouldThrowDraftExpiredException()
    {
        // Arrange
        _cache.GetAsync(_projectId, "gone", Arg.Any<CancellationToken>()).Returns((TemplateDraftEntry?)null);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new PreviewTemplateDraftQuery("gone", "{}"), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }
}
