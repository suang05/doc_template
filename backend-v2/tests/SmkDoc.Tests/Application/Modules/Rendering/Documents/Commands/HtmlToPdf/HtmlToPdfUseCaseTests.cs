using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;

public sealed class HtmlToPdfUseCaseTests
{
    private readonly IPdfRenderer _pdfRenderer = Substitute.For<IPdfRenderer>();

    private HtmlToPdfUseCase CreateSut() => new(_pdfRenderer);

    [Fact]
    public async Task ExecuteAsync_ShouldCallRenderer_AndReturnPdfStream()
    {
        // Arrange
        var request = new HtmlToPdfCommand("<h1>Test</h1>", "<header></header>", "<footer></footer>");
        var expectedBytes = Encoding.UTF8.GetBytes("fake_pdf_content");
        var expectedStream = new MemoryStream(expectedBytes);

        _pdfRenderer.RenderHtmlToPdfStreamAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        // Act
        await using var resultStream = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        // Assert
        resultStream.Should().BeSameAs(expectedStream);

        await _pdfRenderer.Received(1).RenderHtmlToPdfStreamAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            Arg.Any<CancellationToken>());
    }
}
