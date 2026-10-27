using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless utility Use Case for converting raw HTML string to PDF stream.
/// Pure Stream output (Zero LOH).
/// Zero side-effects: No DB writes, no MinIO uploads.
/// </summary>
public sealed class HtmlToPdfUseCase(IPdfRenderer pdfRenderer) : IUseCase<HtmlToPdfCommand, Stream>
{
    public async Task<Stream> ExecuteAsync(HtmlToPdfCommand request, CancellationToken ct = default)
    {
        return await pdfRenderer.RenderHtmlToPdfStreamAsync(
            html: request.Html,
            headerHtml: request.HeaderHtml,
            footerHtml: request.FooterHtml,
            ct: ct);
    }
}
