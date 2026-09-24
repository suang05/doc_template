using SmkDocServer.Domain.Interfaces;
using System.Net.Http.Headers;

namespace SmkDocServer.Infrastructure.Pdf;

public class GotenbergPdfConverter : IPdfConverter
{
    private readonly HttpClient _httpClient;

    public GotenbergPdfConverter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<byte[]> ConvertToPdfAsync(byte[] documentBytes, string fileName)
    {
        using var request = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(documentBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        request.Add(fileContent, "files", fileName);

        var response = await _httpClient.PostAsync("/forms/libreoffice/convert", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gotenberg PDF Conversion Failed: {error}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent)
    {
        using var request = new MultipartFormDataContent();

        var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);
        var fileContent = new ByteArrayContent(htmlBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html; charset=utf-8");
        request.Add(fileContent, "files", "index.html");

        var response = await _httpClient.PostAsync("/forms/chromium/convert/html", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gotenberg Chromium HTML-to-PDF Failed: {error}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> ConvertHtmlWithLayoutAsync(string htmlContent, string? headerHtml, string? footerHtml)
    {
        using var request = new MultipartFormDataContent();

        void AddHtmlPart(string html, string fileName)
        {
            var bytes   = System.Text.Encoding.UTF8.GetBytes(html);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html; charset=utf-8");
            request.Add(content, "files", fileName);
        }

        AddHtmlPart(htmlContent, "index.html");

        if (!string.IsNullOrWhiteSpace(headerHtml))
            AddHtmlPart(headerHtml, "header.html");

        if (!string.IsNullOrWhiteSpace(footerHtml))
            AddHtmlPart(footerHtml, "footer.html");

        var response = await _httpClient.PostAsync("/forms/chromium/convert/html", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gotenberg Chromium HTML-to-PDF Failed: {error}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }
}
