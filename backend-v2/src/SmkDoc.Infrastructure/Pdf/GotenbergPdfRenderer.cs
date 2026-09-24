using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Pdf;

public class GotenbergPdfRenderer : IPdfRenderer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GotenbergPdfRenderer> _logger;

    public GotenbergPdfRenderer(HttpClient httpClient, ILogger<GotenbergPdfRenderer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<byte[]> RenderHtmlToPdfAsync(string html, string? headerHtml = null, string? footerHtml = null, CancellationToken ct = default)
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

        var response = await _httpClient.PostAsync("/forms/chromium/convert/html", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gotenberg Chromium HTML render failed ({StatusCode}): {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Gotenberg PDF render failed: {err}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<byte[]> RenderOfficeToPdfAsync(Stream officeStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(officeStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "files", fileName);

        var response = await _httpClient.PostAsync("/forms/libreoffice/convert", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gotenberg LibreOffice render failed ({StatusCode}): {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Gotenberg Office convert failed: {err}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
