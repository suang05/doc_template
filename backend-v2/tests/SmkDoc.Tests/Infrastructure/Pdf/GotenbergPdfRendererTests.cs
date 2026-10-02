using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Infrastructure;
using SmkDoc.Infrastructure.Pdf;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Pdf;

public class GotenbergPdfRendererTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handlerFunc(request);
    }

    [Fact]
    public async Task RenderHtmlToPdfStreamAsync_WhenSuccessful_ShouldReturnStream()
    {
        // Arrange
        var expectedPdfBytes = "%PDF-1.4 Mock Chromium Output"u8.ToArray();
        var handler = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.PathAndQuery.Should().Be("/forms/chromium/convert/html");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedPdfBytes)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var renderer = new GotenbergPdfRenderer(httpClient, NullLogger<GotenbergPdfRenderer>.Instance);

        // Act
        await using var resultStream = await renderer.RenderHtmlToPdfStreamAsync("<html><body>Hello</body></html>");

        // Assert
        resultStream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await resultStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(expectedPdfBytes);
    }

    [Fact]
    public async Task RenderOfficeToPdfStreamAsync_WhenSuccessful_ShouldReturnStream()
    {
        // Arrange
        var expectedPdfBytes = "%PDF-1.4 Mock LibreOffice Output"u8.ToArray();
        var handler = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.PathAndQuery.Should().Be("/forms/libreoffice/convert");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedPdfBytes)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var renderer = new GotenbergPdfRenderer(httpClient, NullLogger<GotenbergPdfRenderer>.Instance);

        using var officeStream = new MemoryStream("Mock Docx Content"u8.ToArray());

        // Act
        await using var resultStream = await renderer.RenderOfficeToPdfStreamAsync(officeStream, "document.docx");

        // Assert
        resultStream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await resultStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(expectedPdfBytes);
    }

    [Fact]
    public async Task RenderHtmlToPdfStreamAsync_WhenGotenbergReturns4xx_ShouldThrowRenderExceptionWithoutRetrying()
    {
        // Arrange
        int callCount = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            callCount++;
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid HTML template")
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var renderer = new GotenbergPdfRenderer(httpClient, NullLogger<GotenbergPdfRenderer>.Instance);

        // Act
        Func<Task> act = async () => await renderer.RenderHtmlToPdfStreamAsync("<html><body>Error</body></html>");

        // Assert
        var ex = await act.Should().ThrowAsync<RenderException>();
        ex.Which.TemplateSlug.Should().Be("html");
        ex.Which.EngineType.Should().Be("GotenbergChromium");
        callCount.Should().Be(1, "4xx client errors should not trigger retries");
    }

    [Fact]
    public void DependencyInjection_ShouldRegisterGotenbergWithResiliencePipeline()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=user;Password=pass",
            ["GotenbergUrl"] = "http://gotenberg:3000",
            ["Minio:Endpoint"] = "localhost:9000"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var pdfRenderer = serviceProvider.GetService<IPdfRenderer>();
        pdfRenderer.Should().NotBeNull();
        pdfRenderer.Should().BeOfType<GotenbergPdfRenderer>();
    }
}
