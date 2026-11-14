using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Pdf;

public sealed partial class GotenbergPdfRenderer(HttpClient httpClient, ILogger<GotenbergPdfRenderer> logger) : IPdfRenderer
{
    public async Task<Stream> RenderHtmlToPdfStreamAsync(string html, string? headerHtml, string? footerHtml, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var htmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(htmlContent, "files", "index.html");

        using var hContent = !string.IsNullOrWhiteSpace(headerHtml)
            ? new ByteArrayContent(Encoding.UTF8.GetBytes(headerHtml))
            : null;
        if (hContent is not null)
        {
            hContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(hContent, "files", "header.html");
        }

        using var fContent = !string.IsNullOrWhiteSpace(footerHtml)
            ? new ByteArrayContent(Encoding.UTF8.GetBytes(footerHtml))
            : null;
        if (fContent is not null)
        {
            fContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(fContent, "files", "footer.html");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html") { Content = content };
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string err = await response.Content.ReadAsStringAsync(ct);
                LogChromiumRenderFailed(logger, response.StatusCode, err);
                throw new RenderException("html", "GotenbergChromium", err);
            }
            finally
            {
                response.Dispose();
            }
        }

        try
        {
            var innerStream = await response.Content.ReadAsStreamAsync(ct);
            return new ResponseDisposingStream(innerStream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task<Stream> RenderOfficeToPdfStreamAsync(Stream officeStream, string fileName, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(officeStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "files", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/libreoffice/convert") { Content = content };
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string err = await response.Content.ReadAsStringAsync(ct);
                LogLibreOfficeRenderFailed(logger, response.StatusCode, err);
                throw new RenderException(fileName, "GotenbergLibreOffice", err);
            }
            finally
            {
                response.Dispose();
            }
        }

        try
        {
            var innerStream = await response.Content.ReadAsStreamAsync(ct);
            return new ResponseDisposingStream(innerStream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Gotenberg Chromium HTML render failed ({StatusCode}): {Error}")]
    private static partial void LogChromiumRenderFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string error);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Gotenberg LibreOffice render failed ({StatusCode}): {Error}")]
    private static partial void LogLibreOfficeRenderFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string error);
}
