using System.Text;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCaseTests
{
    private readonly ITemplateScannerService _scannerService = Substitute.For<ITemplateScannerService>();
    private readonly IDocxSecurityScanner _docxSecurityScanner = Substitute.For<IDocxSecurityScanner>();
    private readonly ITemplateDraftCache _cache = Substitute.For<ITemplateDraftCache>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public ParseTemplateDraftUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(_projectId);
        _docxSecurityScanner.Scan(Arg.Any<Stream>()).Returns(new DocxScanResult(true, []));
    }

    private ParseTemplateDraftUseCase CreateSut() =>
        new(_scannerService, _docxSecurityScanner, _cache, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenValidDocx_ShouldScanSecurityAndExtractPlaceholdersAndStoreDraft()
    {
        // Arrange
        var detailedPlaceholders = new List<TemplatePlaceholder>
        {
            TemplatePlaceholder.Object("customer.name"),
            TemplatePlaceholder.Object("invoice.total")
        };
        _scannerService.ScanDetailedPlaceholdersAsync(Arg.Any<Stream>(), ".docx", Arg.Any<CancellationToken>())
            .Returns(detailedPlaceholders);
        _cache.StoreAsync(_projectId, Arg.Any<Stream>(), "invoice.docx", ".docx", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("draft-abc");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-docx"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "invoice.docx"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-abc");
        result.Placeholders.Should().BeEquivalentTo(new[] { "customer.name", "invoice.total" });
        result.DetailedPlaceholders.Should().BeEquivalentTo(detailedPlaceholders);
        _docxSecurityScanner.Received(1).Scan(Arg.Any<Stream>());
        await _cache.Received(1).StoreAsync(
            _projectId,
            Arg.Any<Stream>(),
            "invoice.docx",
            ".docx",
            Arg.Is<IReadOnlyList<string>>(p => p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocxFailsSecurity_ShouldThrowDomainValidationExceptionWithoutCaching()
    {
        // Arrange
        _docxSecurityScanner.Scan(Arg.Any<Stream>())
            .Returns(new DocxScanResult(false, ["VBA macro project detected."]));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("macro-docx"));

        // Act
        var act = () => CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "macro.docx"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*VBA macro project detected*");
        await _scannerService.DidNotReceive().ScanDetailedPlaceholdersAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().StoreAsync(Arg.Any<ProjectId>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidHtml_ShouldExtractPlaceholdersWithoutDocxSecurityScan()
    {
        // Arrange
        _scannerService.ScanDetailedPlaceholdersAsync(Arg.Any<Stream>(), ".html", Arg.Any<CancellationToken>())
            .Returns([]);
        _cache.StoreAsync(_projectId, Arg.Any<Stream>(), "empty.html", ".html", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("draft-empty");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));

        // Act
        var result = await CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "empty.html"), CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-empty");
        result.Placeholders.Should().BeEmpty();
        result.DetailedPlaceholders.Should().BeEmpty();
        _docxSecurityScanner.DidNotReceive().Scan(Arg.Any<Stream>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnsupportedExtension_ShouldThrowDomainValidationException()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("malicious-exe"));

        // Act
        var act = () => CreateSut().ExecuteAsync(new ParseTemplateDraftCommand(stream, "test.exe"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Unsupported template file extension*");
    }
}
