using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateHtml;

public sealed class PreviewTemplateHtmlUseCase(
    IEnumerable<IRenderEngine> renderEngines) : IUseCase<PreviewTemplateHtmlQuery, Stream>
{
    private readonly IRenderEngine _htmlEngine = renderEngines.First(e => e.EngineType == RenderEngineType.Html);

    public async Task<Stream> ExecuteAsync(PreviewTemplateHtmlQuery query, CancellationToken ct = default)
    {
        string safeData = string.IsNullOrWhiteSpace(query.SampleDataJson) ? "{}" : query.SampleDataJson;
        byte[] htmlBytes = Encoding.UTF8.GetBytes(query.HtmlContent ?? string.Empty);

        using var templateStream = new MemoryStream(htmlBytes);
        return await _htmlEngine.RenderStreamAsync(templateStream, safeData, OutputFormat.Pdf, ct);
    }
}
