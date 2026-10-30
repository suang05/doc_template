using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;

public sealed class RenderStatelessDocumentUseCaseTests
{
    private readonly IRenderEngine _docxEngine = Substitute.For<IRenderEngine>();
    private readonly IDocxSecurityScanner _securityScanner = Substitute.For<IDocxSecurityScanner>();

    public RenderStatelessDocumentUseCaseTests()
    {
        _docxEngine.EngineType.Returns(RenderEngineType.Docx);
        _securityScanner.Scan(Arg.Any<Stream>()).Returns(new DocxScanResult(true, []));
    }

    private RenderStatelessDocumentUseCase CreateSut() => new([_docxEngine], _securityScanner);

    [Fact]
    public async Task ExecuteAsync_WithSafeDocxFile_ShouldScanAndCallDocxEngine()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var expectedStream = new MemoryStream(expectedPdf);

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("docx-bytes"));
        var command = new RenderStatelessDocumentCommand(memoryStream, "template.docx", "{\"name\":\"test\"}");
        var resultStream = await CreateSut().ExecuteAsync(command);

        resultStream.Should().BeSameAs(expectedStream);
        _securityScanner.Received(1).Scan(Arg.Any<Stream>());
        await _docxEngine.Received(1).RenderStreamAsync(Arg.Any<Stream>(), "{\"name\":\"test\"}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsafeDocxFile_ShouldThrowDomainValidationExceptionWithoutRendering()
    {
        _securityScanner.Scan(Arg.Any<Stream>())
            .Returns(new DocxScanResult(false, ["External relationship SSRF detected."]));

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("unsafe-docx"));
        var command = new RenderStatelessDocumentCommand(memoryStream, "unsafe.docx", "{}");

        Func<Task> action = async () => await CreateSut().ExecuteAsync(command);

        await action.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*External relationship SSRF detected*");
        await _docxEngine.DidNotReceive().RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyJson_ShouldUseEmptyObject()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var expectedStream = new MemoryStream(expectedPdf);

        _docxEngine
            .RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("docx-bytes"));
        var command = new RenderStatelessDocumentCommand(memoryStream, "template.docx", null);
        var resultStream = await CreateSut().ExecuteAsync(command);

        resultStream.Should().BeSameAs(expectedStream);
        await _docxEngine.Received(1).RenderStreamAsync(Arg.Any<Stream>(), "{}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
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
