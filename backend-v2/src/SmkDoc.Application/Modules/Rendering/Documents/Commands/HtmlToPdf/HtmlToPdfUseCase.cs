using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless utility Use Case for converting raw HTML string to PDF bytes or stream.
/// Zero side-effects: No DB writes, no MinIO uploads.
/// </summary>
public sealed class HtmlToPdfUseCase(IPdfRenderer pdfRenderer) : IUseCase<HtmlToPdfCommand, byte[]>
{
    private readonly IPdfRenderer _pdfRenderer = pdfRenderer;

    public async Task<byte[]> ExecuteAsync(HtmlToPdfCommand request, CancellationToken ct = default)
    {
        return await _pdfRenderer.RenderHtmlToPdfAsync(
            html: request.Html,
            headerHtml: request.HeaderHtml,
            footerHtml: request.FooterHtml,
            ct: ct);
    }

    public async Task<Stream> ExecuteStreamAsync(HtmlToPdfCommand request, CancellationToken ct = default)
    {
        return await _pdfRenderer.RenderHtmlToPdfStreamAsync(
            html: request.Html,
            headerHtml: request.HeaderHtml,
            footerHtml: request.FooterHtml,
            ct: ct);
    }
}
