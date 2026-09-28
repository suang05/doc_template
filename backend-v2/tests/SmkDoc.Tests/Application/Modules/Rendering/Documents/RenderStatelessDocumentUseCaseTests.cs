using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Documents;

public class RenderStatelessDocumentUseCaseTests
{
    private static (RenderStatelessDocumentUseCase UseCase, Mock<IRenderEngine> DocxEngine) CreateUseCase()
    {
        var docxEngine = new Mock<IRenderEngine>();
        docxEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Docx);

        var useCase = new RenderStatelessDocumentUseCase(new[] { docxEngine.Object });
        return (useCase, docxEngine);
    }

    [Fact]
    public async Task ExecuteAsync_WithDocxFile_ShouldCallDocxEngine()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var (useCase, docxEngine) = CreateUseCase();
        
        docxEngine
            .Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPdf);

        using var memoryStream = new MemoryStream();
        var result = await useCase.ExecuteAsync(memoryStream, "template.docx", "{\"name\":\"test\"}");

        result.Should().BeEquivalentTo(expectedPdf);
        docxEngine.Verify(e => e.RenderAsync(memoryStream, "{\"name\":\"test\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyJson_ShouldUseEmptyObject()
    {
        var expectedPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var (useCase, docxEngine) = CreateUseCase();
        
        docxEngine
            .Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPdf);

        using var memoryStream = new MemoryStream();
        var result = await useCase.ExecuteAsync(memoryStream, "template.docx", null);

        result.Should().BeEquivalentTo(expectedPdf);
        docxEngine.Verify(e => e.RenderAsync(memoryStream, "{}", OutputFormat.Pdf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedExtension_ShouldThrowException()
    {
        var (useCase, _) = CreateUseCase();
        using var memoryStream = new MemoryStream();
        
        Func<Task> action = async () => await useCase.ExecuteAsync(memoryStream, "template.xyz", null);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Unsupported file extension: .xyz");
    }
}
