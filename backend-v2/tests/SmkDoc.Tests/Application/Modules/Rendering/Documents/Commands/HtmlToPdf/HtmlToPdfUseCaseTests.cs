using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;

public class HtmlToPdfUseCaseTests
{
    private readonly Mock<IPdfRenderer> _pdfRendererMock = new();
    private HtmlToPdfUseCase CreateSut() => new(_pdfRendererMock.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldCallRenderer_AndReturnPdfBytes()
    {
        // Arrange
        var request = new HtmlToPdfCommand("<h1>Test</h1>", "<header></header>", "<footer></footer>");
        var expectedBytes = Encoding.UTF8.GetBytes("fake_pdf_content");

        _pdfRendererMock.Setup(r => r.RenderHtmlToPdfAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBytes);

        // Act
        var result = await CreateSut().ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(expectedBytes);

        _pdfRendererMock.Verify(r => r.RenderHtmlToPdfAsync(
            request.Html,
            request.HeaderHtml,
            request.FooterHtml,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
