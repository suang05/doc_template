using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Engines.DraftHandlers;

public sealed class DocxDraftHandler(
    IDocxSecurityScanner securityScanner,
    ITemplateScannerService scannerService) : ITemplateDraftHandler
{
    public TemplateFormat Format => TemplateFormat.Docx;

    public Task ValidateSecurityAsync(Stream stream, CancellationToken ct = default)
    {
        var scanResult = securityScanner.Scan(stream);
        if (scanResult is { IsSafe: false })
        {
            throw new DomainValidationException(
                $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> ExtractPlaceholdersAsync(Stream stream, CancellationToken ct = default)
    {
        return scannerService.ScanPlaceholdersAsync(stream, Format.Extension, ct);
    }
}

