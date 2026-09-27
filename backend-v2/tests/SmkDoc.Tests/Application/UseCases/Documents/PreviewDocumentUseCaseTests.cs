using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Application.UseCases.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests;

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
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 Ephemeral Preview"));

        var useCase = new PreviewDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockEngine.Object }
        );

        using var jsonDoc = JsonDocument.Parse("{\"buyer\": \"สมศรี\"}");
        var request = new PreviewDocumentRequest(
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
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 Unsaved Template Preview"));

        var useCase = new PreviewDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new[] { _mockEngine.Object }
        );

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentRequest(
            Data: jsonDoc.RootElement,
            Html: "<html><body>Hello Unsaved</body></html>"
        );

        // Act
        byte[] previewBytes = await useCase.ExecuteAsync(null, request);

        // Assert
        previewBytes.Should().NotBeNullOrEmpty();
        _mockTemplateRepo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Template, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
