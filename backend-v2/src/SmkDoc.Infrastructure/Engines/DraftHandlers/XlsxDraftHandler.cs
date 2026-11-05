using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Engines.DraftHandlers;

public sealed class XlsxDraftHandler(ITemplateScannerService scannerService) : ITemplateDraftHandler
{
    public TemplateFormat Format => TemplateFormat.Xlsx;

    public Task ValidateSecurityAsync(Stream stream, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<List<string>> ExtractPlaceholdersAsync(Stream stream, CancellationToken ct = default)
    {
        return scannerService.ScanPlaceholdersAsync(stream, Format.Extension, ct);
    }
}

