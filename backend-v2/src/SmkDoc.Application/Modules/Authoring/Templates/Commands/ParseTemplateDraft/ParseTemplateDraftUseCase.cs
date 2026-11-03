using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCase(
    ITemplateScannerService scanner,
    ITemplateDraftCache draftCache,
    IDocxSecurityScanner securityScanner,
    ICurrentProjectContext projectContext) : IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto>
{
    private const long MaxDraftFileSizeBytes = 20L * 1024 * 1024;

    public async Task<ParseDraftResultDto> ExecuteAsync(ParseTemplateDraftCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Uploading a template draft requires an active project context.");
        }

        var ext = Path.GetExtension(command.FileName).ToLowerInvariant();

        await using var spooled = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);
        if (spooled.Length > MaxDraftFileSizeBytes)
        {
            throw new DomainValidationException(
                $"Draft file size ({spooled.Length / (1024 * 1024)} MB) exceeds the 20 MB limit.");
        }

        if (ext == ".docx")
        {
            spooled.Position = 0;
            var scanResult = securityScanner.Scan(spooled);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }
        }

        spooled.Position = 0;
        var placeholders = await scanner.ScanPlaceholdersAsync(spooled, ext, ct);

        spooled.Position = 0;
        var draftId = await draftCache.StoreAsync(
            projectContext.ProjectId,
            spooled,
            command.FileName,
            ext,
            placeholders,
            ct);

        return new ParseDraftResultDto(draftId, placeholders);
    }
}
