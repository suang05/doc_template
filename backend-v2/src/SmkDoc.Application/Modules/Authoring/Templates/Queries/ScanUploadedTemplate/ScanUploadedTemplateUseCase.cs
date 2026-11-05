using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;

/// <summary>
/// Stateless Use Case for scanning placeholders from an uploaded file stream.
/// Zero side-effects (no DB writes, no MinIO uploads).
/// </summary>
public sealed class ScanUploadedTemplateUseCase(
    ITemplateScannerService scanner,
    IDocxSecurityScanner? securityScanner = null) : IUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto>
{
    public async Task<ScanFieldsResultDto> ExecuteAsync(ScanUploadedTemplateQuery query, CancellationToken ct = default)
    {
        if (query.Stream == null || (query.Stream.CanSeek && query.Stream.Length == 0))
        {
            throw new DomainValidationException("Template file stream cannot be empty.");
        }

        var ext = query.Extension.ToLowerInvariant();
        if (ext == ".docx" && securityScanner != null)
        {
            await using var spool = await HybridSpoolStream.SpoolFromAsync(query.Stream, ct: ct);
            if (spool.Length == 0)
            {
                throw new DomainValidationException("Template file stream cannot be empty.");
            }

            var scanResult = securityScanner.Scan(spool);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }

            spool.Position = 0;
            var docxDetailed = await scanner.ScanDetailedPlaceholdersAsync(spool, ext, ct);
            var docxPlaceholders = docxDetailed.Select(p => p.Name).Distinct().ToList();
            return new ScanFieldsResultDto(docxPlaceholders, docxDetailed);
        }

        var detailed = await scanner.ScanDetailedPlaceholdersAsync(query.Stream, ext, ct);
        var placeholders = detailed.Select(p => p.Name).Distinct().ToList();
        return new ScanFieldsResultDto(placeholders, detailed);
    }
}
