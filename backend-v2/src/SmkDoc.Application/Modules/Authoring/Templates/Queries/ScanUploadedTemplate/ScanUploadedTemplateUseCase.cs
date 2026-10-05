using SmkDoc.Application.Common;
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
    ITemplateScannerService scanner) : IUseCase<ScanUploadedTemplateQuery, List<string>>
{
    public async Task<List<string>> ExecuteAsync(ScanUploadedTemplateQuery query, CancellationToken ct = default)
    {
        if (query.Stream == null || query.Stream.Length == 0)
        {
            throw new DomainValidationException("Template file stream cannot be empty.");
        }

        var ext = query.Extension.ToLowerInvariant();
        return await scanner.ScanPlaceholdersAsync(query.Stream, ext, ct);
    }
}
