using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftHandlerRegistry _registry = Substitute.For<ITemplateDraftHandlerRegistry>();
    private readonly ITemplateDraftHandler _handler = Substitute.For<ITemplateDraftHandler>();
    private readonly ITemplateDraftCache _cache = Substitute.For<ITemplateDraftCache>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public ParseTemplateDraftUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
        _registry.GetHandler(Arg.Any<string>()).Returns(_handler);
    }

    private ParseTemplateDraftUseCase CreateSut() => new(_registry, _cache, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenValidFile_ShouldScanStoreDraftAndReturnPlaceholders()
    {
        // Arrange
        var placeholders = new List<string> { "customer.name", "invoice.total" };
        _handler.ExtractPlaceholdersAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(placeholders);
        _cache.StoreAsync(_projectId, Arg.Any<Stream>(), "invoice.docx", ".docx", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("draft-abc");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-docx"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "invoice.docx"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-abc");
        result.Placeholders.Should().BeEquivalentTo(placeholders);
        await _handler.Received(1).ValidateSecurityAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).StoreAsync(
            _projectId,
            Arg.Any<Stream>(),
            "invoice.docx",
            ".docx",
            Arg.Is<IReadOnlyList<string>>(p => p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerFailsSecurity_ShouldThrowDomainValidationExceptionWithoutCaching()
    {
        // Arrange
        _handler.ValidateSecurityAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainValidationException("VBA macro project detected."));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("macro-docx"));

        // Act
        var act = () => CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "macro.docx"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*VBA macro project detected*");
        await _handler.DidNotReceive().ExtractPlaceholdersAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().StoreAsync(Arg.Any<ProjectId>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenScannerReturnsEmpty_ShouldStoreEntryWithEmptyPlaceholders()
    {
        // Arrange
        _handler.ExtractPlaceholdersAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _cache.StoreAsync(_projectId, Arg.Any<Stream>(), "empty.html", ".html", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("draft-empty");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "empty.html"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-empty");
        result.Placeholders.Should().BeEmpty();
    }
}
