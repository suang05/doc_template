using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

public class PreviewDocumentUseCaseTests
{
    private readonly Mock<ITemplateRepository>          _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();
    private readonly Mock<IRenderEngine>                _mockEngine       = new();

    private PreviewDocumentUseCase CreateSut() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockStorage.Object,
        [_mockEngine.Object]
    );

    [Fact]
    public async Task ExecuteAsync_ShouldHaveZeroSideEffects_AndNeverUploadToStorage()
    {
        // Arrange
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Ephemeral Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"buyer\": \"สมศรี\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body><Field name=\"buyer\" /><h2>Preview: {{buyer}}</h2></body></html>"
        );

        // Act
        byte[] previewBytes = await CreateSut().ExecuteAsync("sale-contract", request);

        // Assert
        previewBytes.Should().NotBeNullOrEmpty();

        // Zero side-effects verification:
        _mockStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockStorage.Verify(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugIsNull_AndHtmlProvided_ShouldRenderDirectlyWithoutDbLookup()
    {
        // Arrange
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Unsaved Template Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body>Hello Unsaved</body></html>"
        );

        // Act
        byte[] previewBytes = await CreateSut().ExecuteAsync(null, request);

        // Assert
        previewBytes.Should().NotBeNullOrEmpty();
        _mockTemplateRepo.Verify(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldReturnDirectStreamWithoutStorageSideEffects()
    {
        // Arrange
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Direct Stream Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body>Stream Test</body></html>"
        );

        // Act
        await using var streamResult = await CreateSut().ExecuteStreamAsync(null, request);

        // Assert
        streamResult.Should().NotBeNull();
        using var reader = new StreamReader(streamResult);
        string text = await reader.ReadToEndAsync();
        text.Should().Contain("%PDF-1.4 Direct Stream Preview");

        _mockStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
