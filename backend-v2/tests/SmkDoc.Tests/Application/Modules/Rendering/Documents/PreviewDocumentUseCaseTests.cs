using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents;

public class PreviewDocumentUseCaseTests
{
    private readonly Mock<IRepository<Template>>        _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();
    private readonly Mock<IRenderEngine>                _mockEngine       = new();

    [Fact]
    public async Task ExecuteAsync_ShouldHaveZeroSideEffects_AndNeverUploadToStorage()
    {
        // Arrange
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Ephemeral Preview")));

        var useCase = new PreviewDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockEngine.Object }
        );

        using var jsonDoc = JsonDocument.Parse("{\"buyer\": \"สมศรี\"}");
        var request = new PreviewDocumentQuery(
            Data: jsonDoc.RootElement,
            Html: "<html><body><Field name=\"buyer\" /><h2>Preview: {{buyer}}</h2></body></html>"
        );

        // Act
        byte[] previewBytes = await useCase.ExecuteAsync("sale-contract", request);

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

        var useCase = new PreviewDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockEngine.Object }
        );

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Data: jsonDoc.RootElement,
            Html: "<html><body>Hello Unsaved</body></html>"
        );

        // Act
        byte[] previewBytes = await useCase.ExecuteAsync(null, request);

        // Assert
        previewBytes.Should().NotBeNullOrEmpty();
        _mockTemplateRepo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Template, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldReturnDirectStreamWithoutStorageSideEffects()
    {
        // Arrange
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Direct Stream Preview")));

        var useCase = new PreviewDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockEngine.Object }
        );

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Data: jsonDoc.RootElement,
            Html: "<html><body>Stream Test</body></html>"
        );

        // Act
        await using var streamResult = await useCase.ExecuteStreamAsync(null, request);

        // Assert
        streamResult.Should().NotBeNull();
        using var reader = new StreamReader(streamResult);
        string text = await reader.ReadToEndAsync();
        text.Should().Contain("%PDF-1.4 Direct Stream Preview");

        _mockStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
