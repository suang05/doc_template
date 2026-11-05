using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.DraftHandlers;

public sealed class HtmlDraftHandler(ITemplateScannerService scannerService) : ITemplateDraftHandler
{
    public TemplateFormat Format => TemplateFormat.Html;

    public Task ValidateSecurityAsync(Stream stream, CancellationToken ct = default)
    {
        // HTML draft security checks (e.g. prohibited external script injection can be added here)
        return Task.CompletedTask;
    }

    public Task<List<string>> ExtractPlaceholdersAsync(Stream stream, CancellationToken ct = default)
    {
        return scannerService.ScanPlaceholdersAsync(stream, Format.Extension, ct);
    }

    public Task<List<TemplatePlaceholder>> ExtractDetailedPlaceholdersAsync(Stream stream, CancellationToken ct = default)
    {
        return scannerService.ScanDetailedPlaceholdersAsync(stream, Format.Extension, ct);
    }
}
