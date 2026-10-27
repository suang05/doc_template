using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateHtml;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries;

public sealed class PreviewTemplateHtmlUseCaseTests
{
    private readonly IRenderEngine _htmlEngine = Substitute.For<IRenderEngine>();

    public PreviewTemplateHtmlUseCaseTests()
    {
        _htmlEngine.EngineType.Returns(RenderEngineType.Html);
    }

    private PreviewTemplateHtmlUseCase CreateSut() => new([_htmlEngine]);

    [Fact]
    public async Task ExecuteAsync_ShouldRenderStream_WithoutTouchingStorageOrDatabase()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(expectedPdf));

        string bufferHtml = "<html><body><h1>Direct Editor Buffer</h1></body></html>";
        string sampleJson = "{\"title\": \"Test\"}";
        var query = new PreviewTemplateHtmlQuery(bufferHtml, sampleJson);

        // Act
        await using var stream = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        stream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(expectedPdf);

        await _htmlEngine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(), sampleJson, OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySampleData_ShouldUseEmptyJsonObject()
    {
        // Arrange
        byte[] expectedPdf = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Stream");
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(expectedPdf));

        var query = new PreviewTemplateHtmlQuery("<html></html>", null);

        // Act
        await using var stream = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        stream.Should().NotBeNull();
        await _htmlEngine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(), "{}", OutputFormat.Pdf, Arg.Any<CancellationToken>());
    }
}
