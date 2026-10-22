using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;

public sealed class RenderStatelessDocumentUseCaseTests
{
    private readonly IRenderEngine _docxEngine = Substitute.For<IRenderEngine>();

    public RenderStatelessDocumentUseCaseTests()
    {
        _docxEngine.EngineType.Returns(RenderEngineType.Docx);
    }

    private RenderStatelessDocumentUseCase CreateSut() => new([_docxEngine]);

    [Fact]
    public async Task ExecuteAsync_WithDocxFile_ShouldCallDocxEngine()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(expectedPdf));

        using var memoryStream = new MemoryStream();
        var result = await CreateSut().ExecuteAsync(memoryStream, "template.docx", "{\"name\":\"test\"}");

        result.Should().BeEquivalentTo(expectedPdf);
        await _docxEngine.Received(1).RenderStreamAsync(memoryStream, "{\"name\":\"test\"}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyJson_ShouldUseEmptyObject()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(expectedPdf));

        using var memoryStream = new MemoryStream();
        var result = await CreateSut().ExecuteAsync(memoryStream, "template.docx", null);

        result.Should().BeEquivalentTo(expectedPdf);
        await _docxEngine.Received(1).RenderStreamAsync(memoryStream, "{}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStreamAsync_WithDocxFile_ShouldReturnStream()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(expectedPdf));

        using var memoryStream = new MemoryStream();
        await using var streamResult = await CreateSut().ExecuteStreamAsync(memoryStream, "template.docx", "{\"name\":\"test\"}");

        streamResult.Should().NotBeNull();
        using var ms = new MemoryStream();
        await streamResult.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(expectedPdf);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedExtension_ShouldThrowException()
    {
        using var memoryStream = new MemoryStream();

        Func<Task> action = async () => await CreateSut().ExecuteAsync(memoryStream, "template.xyz", null);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Unsupported file extension: .xyz");
    }
}
