using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCase(
    ITemplateScannerService scannerService,
    IDocxSecurityScanner docxSecurityScanner,
    ITemplateDraftCache draftCache,
    ICurrentProjectContext projectContext) : IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto>
{
    private const long MaxDraftFileSizeBytes = 20L * 1024 * 1024;

    public async Task<ParseDraftResultDto> ExecuteAsync(ParseTemplateDraftCommand command, CancellationToken ct)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.GetRequiredProjectId();

        var ext = Path.GetExtension(command.FileName);
        if (!TemplateFormat.TryFromExtension(ext, out _))
        {
            throw new DomainValidationException(
                $"Unsupported template file extension: '{ext}'. Allowed formats: .html, .docx, .xlsx");
        }

        await using var spooled = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);
        if (spooled.Length > MaxDraftFileSizeBytes)
        {
            throw new DomainValidationException(
                $"Draft file size ({spooled.Length / (1024 * 1024)} MB) exceeds the 20 MB limit.");
        }

        if (string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            spooled.Position = 0;
            var scanResult = docxSecurityScanner.Scan(spooled);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }
        }

        spooled.Position = 0;
        var detailedPlaceholders = await scannerService.ScanDetailedPlaceholdersAsync(spooled, ext, ct);
        var placeholders = detailedPlaceholders.Select(p => p.Name).Distinct().ToList();

        spooled.Position = 0;
        var draftId = await draftCache.StoreAsync(
            projectId,
            spooled,
            command.FileName,
            ext,
            placeholders,
            ct);

        return new ParseDraftResultDto(draftId, placeholders, detailedPlaceholders);
    }
}
