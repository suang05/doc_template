using System.Text;
using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

public sealed class PreviewDocumentUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();

    public PreviewDocumentUseCaseTests()
    {
        _engine.EngineType.Returns(RenderEngineType.Html);
    }

    private PreviewDocumentUseCase CreateSut() => new(
        _templateRepo,
        _storage,
        [_engine]
    );

    [Fact]
    public async Task ExecuteAsync_ShouldHaveZeroSideEffects_AndNeverUploadToStorage()
    {
        // Arrange
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Ephemeral Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"buyer\": \"สมศรี\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body><Field name=\"buyer\" /><h2>Preview: {{buyer}}</h2></body></html>",
            Slug: "sale-contract"
        );

        // Act
        await using var previewStream = await CreateSut().ExecuteAsync(request);

        // Assert
        previewStream.Should().NotBeNull();

        // Zero side-effects verification:
        await _storage.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().GetPresignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugIsNull_AndHtmlProvided_ShouldRenderDirectlyWithoutDbLookup()
    {
        // Arrange
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Unsaved Template Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body>Hello Unsaved</body></html>"
        );

        // Act
        await using var previewStream = await CreateSut().ExecuteAsync(request);

        // Assert
        previewStream.Should().NotBeNull();
        await _templateRepo.DidNotReceive().GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnDirectStreamWithoutStorageSideEffects()
    {
        // Arrange
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Direct Stream Preview")));

        using var jsonDoc = JsonDocument.Parse("{\"key\": \"val\"}");
        var request = new PreviewDocumentQuery(
            Payload: jsonDoc.RootElement,
            Html: "<html><body>Stream Test</body></html>"
        );

        // Act
        await using var streamResult = await CreateSut().ExecuteAsync(request);

        // Assert
        streamResult.Should().NotBeNull();
        using var reader = new StreamReader(streamResult);
        string text = await reader.ReadToEndAsync();
        text.Should().Contain("%PDF-1.4 Direct Stream Preview");

        await _storage.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugIsNull_AndHtmlIsNull_ThrowsDomainValidationException()
    {
        // Arrange
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new PreviewDocumentQuery(Payload: jsonDoc.RootElement, Html: null);

        // Act
        var act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoPublishedVersion_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var template = TemplateBuilder.ATemplate().WithoutCurrentVersion().Build();
        _templateRepo.GetBySlugAsync("tpl-no-version", Arg.Any<CancellationToken>())
            .Returns(template);

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new PreviewDocumentQuery(Payload: jsonDoc.RootElement, Html: null, Slug: "tpl-no-version");

        // Act
        var act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }
}
