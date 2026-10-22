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
    public async Task ExecuteAsync_ShouldCallRenderer_AndReturnPdfBytes()
    {
        // Arrange
        var request = new HtmlToPdfCommand("<h1>Test</h1>", "<header></header>", "<footer></footer>");
        var expectedBytes = Encoding.UTF8.GetBytes("fake_pdf_content");

        _pdfRenderer.RenderHtmlToPdfAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            Arg.Any<CancellationToken>())
            .Returns(expectedBytes);

        // Act
        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(expectedBytes);

        await _pdfRenderer.Received(1).RenderHtmlToPdfAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            Arg.Any<CancellationToken>());
    }
}
