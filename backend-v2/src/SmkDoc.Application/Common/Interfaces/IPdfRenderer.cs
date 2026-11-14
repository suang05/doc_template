namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for PDF conversion engines (Gotenberg Chromium &amp; LibreOffice).
/// Exposes pure Zero-LOH response-owning stream contracts only.
/// </summary>
public interface IPdfRenderer
{
    Task<Stream> RenderHtmlToPdfStreamAsync(string html, string? headerHtml, string? footerHtml, CancellationToken ct);
    Task<Stream> RenderOfficeToPdfStreamAsync(Stream officeStream, string fileName, CancellationToken ct);
}
