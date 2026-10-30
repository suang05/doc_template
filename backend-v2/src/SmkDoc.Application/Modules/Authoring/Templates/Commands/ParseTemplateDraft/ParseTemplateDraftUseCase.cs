using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCase(
    ITemplateScannerService scanner,
    ITemplateDraftCache draftCache,
    IDocxSecurityScanner securityScanner) : IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto>
{
    private readonly ITemplateScannerService _scanner = scanner;
    private readonly ITemplateDraftCache _draftCache = draftCache;
    private readonly IDocxSecurityScanner _securityScanner = securityScanner;

    public async Task<ParseDraftResultDto> ExecuteAsync(ParseTemplateDraftCommand command, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(command.FileName).ToLowerInvariant();

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await command.FileStream.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        if (ext == ".docx")
        {
            using var securityStream = new MemoryStream(fileBytes, writable: false);
            var scanResult = _securityScanner.Scan(securityStream);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }
        }

        using var scanStream = new MemoryStream(fileBytes, writable: false);
        var placeholders = await _scanner.ScanPlaceholdersAsync(scanStream, ext, ct);

        var entry = new TemplateDraftEntry(fileBytes, command.FileName, ext, placeholders);
        var draftId = await _draftCache.StoreAsync(entry, ct);

        return new ParseDraftResultDto(draftId, placeholders);
    }
}
