using System.Threading;
using System.Threading.Tasks;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Stateless utility Use Case for converting raw HTML string to PDF bytes.
/// Zero side-effects: No DB writes, no MinIO uploads.
/// </summary>
public sealed class HtmlToPdfUseCase(IPdfRenderer pdfRenderer)
{
    public async Task<byte[]> ExecuteAsync(HtmlToPdfCommand request, CancellationToken ct)
    {
        return await pdfRenderer.RenderHtmlToPdfAsync(
            html: request.Html,
            headerHtml: request.HeaderHtml,
            footerHtml: request.FooterHtml,
            ct: ct);
    }
}
