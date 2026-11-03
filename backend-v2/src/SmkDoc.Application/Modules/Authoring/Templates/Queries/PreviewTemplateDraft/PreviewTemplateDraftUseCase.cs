using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

/// <summary>
/// Renders a tenant-scoped draft template directly to PDF stream with zero database or output-bucket side-effects.
/// Pure Stream I/O (Zero LOH).
/// </summary>
public sealed class PreviewTemplateDraftUseCase(
    ITemplateDraftCache draftCache,
    IEnumerable<IRenderEngine> engines,
    ICurrentProjectContext projectContext) : IUseCase<PreviewTemplateDraftQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(PreviewTemplateDraftQuery query, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Previewing a template draft requires an active project context.");
        }

        var draft = await draftCache.GetAsync(projectContext.ProjectId, query.DraftId, ct)
            ?? throw new DraftExpiredException(query.DraftId);

        var engine = Engine(draft.FileExtension);

        await using var draftStream = await draftCache.OpenStreamAsync(draft, ct);
        return await engine.RenderStreamAsync(draftStream, query.DataJson, OutputFormat.Pdf, ct);
    }

    private IRenderEngine Engine(string ext)
    {
        var type = ext switch
        {
            ".xlsx" => RenderEngineType.Excel,
            ".docx" => RenderEngineType.Docx,
            _       => RenderEngineType.Html,
        };
        return engines.FirstOrDefault(e => e.EngineType == type)
            ?? throw new RenderException("draft", type.ToString(), $"No render engine registered for '{ext}'.");
    }
}
