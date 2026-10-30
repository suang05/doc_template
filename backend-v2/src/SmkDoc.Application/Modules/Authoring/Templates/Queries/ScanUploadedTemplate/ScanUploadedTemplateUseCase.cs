using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;

/// <summary>
/// Query parameters for scanning placeholders from an uploaded template stream.
/// </summary>
public sealed record ScanUploadedTemplateQuery(Stream Stream, string Extension);

/// <summary>
/// Stateless Use Case for scanning placeholders from an uploaded file stream.
/// Zero side-effects (no DB writes, no MinIO uploads).
/// </summary>
public sealed class ScanUploadedTemplateUseCase(
    ITemplateScannerService scanner,
    IDocxSecurityScanner? securityScanner = null) : IUseCase<ScanUploadedTemplateQuery, List<string>>
{
    public async Task<List<string>> ExecuteAsync(ScanUploadedTemplateQuery query, CancellationToken ct = default)
    {
        if (query.Stream == null || (query.Stream.CanSeek && query.Stream.Length == 0))
        {
            throw new DomainValidationException("Template file stream cannot be empty.");
        }

        var ext = query.Extension.ToLowerInvariant();
        if (ext == ".docx" && securityScanner != null)
        {
            using var ms = new MemoryStream();
            await query.Stream.CopyToAsync(ms, ct);
            if (ms.Length == 0)
            {
                throw new DomainValidationException("Template file stream cannot be empty.");
            }

            ms.Position = 0;
            var scanResult = securityScanner.Scan(ms);
            if (scanResult is { IsSafe: false })
            {
                throw new DomainValidationException(
                    $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }

            ms.Position = 0;
            return await scanner.ScanPlaceholdersAsync(ms, ext, ct);
        }

        return await scanner.ScanPlaceholdersAsync(query.Stream, ext, ct);
    }
}
