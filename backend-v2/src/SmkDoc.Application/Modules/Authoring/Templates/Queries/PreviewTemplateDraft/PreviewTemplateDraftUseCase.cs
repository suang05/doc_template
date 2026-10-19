using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

public sealed class PreviewTemplateDraftUseCase(
    ITemplateDraftCache draftCache,
    IEnumerable<IRenderEngine> engines) : IUseCase<PreviewTemplateDraftQuery, byte[]>
{
    private readonly ITemplateDraftCache _draftCache = draftCache;
    private readonly IEnumerable<IRenderEngine> _engines = engines;

    public async Task<Stream> ExecuteStreamAsync(PreviewTemplateDraftQuery query, CancellationToken ct = default)
    {
        var draft = await _draftCache.GetAsync(query.DraftId, ct)
            ?? throw new DraftExpiredException(query.DraftId);

        var engine = Engine(draft.FileExtension);

        using var ms = new MemoryStream(draft.FileBytes);
        return await engine.RenderStreamAsync(ms, query.DataJson, OutputFormat.Pdf, ct);
    }

    public async Task<byte[]> ExecuteAsync(PreviewTemplateDraftQuery query, CancellationToken ct = default)
    {
        await using var stream = await ExecuteStreamAsync(query, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private IRenderEngine Engine(string ext)
    {
        var type = ext switch
        {
            ".xlsx" => RenderEngineType.Excel,
            ".docx" => RenderEngineType.Docx,
            _       => RenderEngineType.Html,
        };
        return _engines.FirstOrDefault(e => e.EngineType == type)
            ?? throw new RenderException("draft", type.ToString(), $"No render engine registered for '{ext}'.");
    }
}
