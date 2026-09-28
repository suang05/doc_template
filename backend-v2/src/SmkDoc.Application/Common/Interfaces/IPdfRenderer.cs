namespace SmkDoc.Application.Common.Interfaces;

public interface IPdfRenderer
{
    Task<byte[]> RenderHtmlToPdfAsync(string html, string? headerHtml = null, string? footerHtml = null, CancellationToken ct = default);
    Task<byte[]> RenderOfficeToPdfAsync(Stream officeStream, string fileName, CancellationToken ct = default);

    Task<Stream> RenderHtmlToPdfStreamAsync(string html, string? headerHtml = null, string? footerHtml = null, CancellationToken ct = default);
    Task<Stream> RenderOfficeToPdfStreamAsync(Stream officeStream, string fileName, CancellationToken ct = default);
}
