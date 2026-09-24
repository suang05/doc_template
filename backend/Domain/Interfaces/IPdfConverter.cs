namespace SmkDocServer.Domain.Interfaces;

public interface IPdfConverter
{
    Task<byte[]> ConvertToPdfAsync(byte[] documentBytes, string fileName);

    /// <summary>
    /// Converts raw HTML content to PDF via Gotenberg Chromium route.
    /// Used by HtmlTemplateProcessor (Sprint 5).
    /// </summary>
    Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent);

    /// <summary>
    /// Converts raw HTML with optional header/footer HTML via Gotenberg Chromium.
    /// Used by HtmlToPdf endpoint (zero side-effects).
    /// </summary>
    Task<byte[]> ConvertHtmlWithLayoutAsync(string htmlContent, string? headerHtml, string? footerHtml);
}

