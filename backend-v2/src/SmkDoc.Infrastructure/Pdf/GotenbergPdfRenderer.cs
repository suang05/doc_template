using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Pdf;

public class GotenbergPdfRenderer(HttpClient httpClient, ILogger<GotenbergPdfRenderer> logger) : IPdfRenderer
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<GotenbergPdfRenderer> _logger = logger;

    public async Task<Stream> RenderHtmlToPdfStreamAsync(string html, string? headerHtml = null, string? footerHtml = null, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var htmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(htmlContent, "files", "index.html");

        if (!string.IsNullOrWhiteSpace(headerHtml))
        {
            var hContent = new ByteArrayContent(Encoding.UTF8.GetBytes(headerHtml));
            hContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(hContent, "files", "header.html");
        }

        if (!string.IsNullOrWhiteSpace(footerHtml))
        {
            var fContent = new ByteArrayContent(Encoding.UTF8.GetBytes(footerHtml));
            fContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(fContent, "files", "footer.html");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html") { Content = content };
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Gotenberg Chromium HTML render failed ({StatusCode}): {Error}", response.StatusCode, err);
                throw new RenderException("html", "GotenbergChromium", err);
            }
            finally
            {
                response.Dispose();
            }
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task<byte[]> RenderHtmlToPdfAsync(string html, string? headerHtml = null, string? footerHtml = null, CancellationToken ct = default)
    {
        await using var stream = await RenderHtmlToPdfStreamAsync(html, headerHtml, footerHtml, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public async Task<Stream> RenderOfficeToPdfStreamAsync(Stream officeStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(officeStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "files", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/libreoffice/convert") { Content = content };
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Gotenberg LibreOffice render failed ({StatusCode}): {Error}", response.StatusCode, err);
                throw new RenderException(fileName, "GotenbergLibreOffice", err);
            }
            finally
            {
                response.Dispose();
            }
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task<byte[]> RenderOfficeToPdfAsync(Stream officeStream, string fileName, CancellationToken ct = default)
    {
        await using var stream = await RenderOfficeToPdfStreamAsync(officeStream, fileName, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
