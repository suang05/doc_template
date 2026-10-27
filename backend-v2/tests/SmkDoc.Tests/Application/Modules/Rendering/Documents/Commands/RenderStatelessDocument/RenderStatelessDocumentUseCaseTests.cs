using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
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
    public async Task ExecuteAsync_WithDocxFile_ShouldCallDocxEngineAndReturnStream()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var expectedStream = new MemoryStream(expectedPdf);

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        using var memoryStream = new MemoryStream();
        var command = new RenderStatelessDocumentCommand(memoryStream, "template.docx", "{\"name\":\"test\"}");
        var resultStream = await CreateSut().ExecuteAsync(command);

        resultStream.Should().BeSameAs(expectedStream);
        await _docxEngine.Received(1).RenderStreamAsync(memoryStream, "{\"name\":\"test\"}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyJson_ShouldUseEmptyObject()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var expectedStream = new MemoryStream(expectedPdf);

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        using var memoryStream = new MemoryStream();
        var command = new RenderStatelessDocumentCommand(memoryStream, "template.docx", null);
        var resultStream = await CreateSut().ExecuteAsync(command);

        resultStream.Should().BeSameAs(expectedStream);
        await _docxEngine.Received(1).RenderStreamAsync(memoryStream, "{}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedExtension_ShouldThrowException()
    {
        using var memoryStream = new MemoryStream();
        var command = new RenderStatelessDocumentCommand(memoryStream, "template.xyz", null);

        Func<Task> action = async () => await CreateSut().ExecuteAsync(command);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Unsupported file extension: .xyz");
    }
}
