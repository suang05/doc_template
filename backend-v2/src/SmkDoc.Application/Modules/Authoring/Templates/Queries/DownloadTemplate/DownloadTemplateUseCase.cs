using SmkDoc.Application.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;

/// <summary>
/// Single-responsibility Query Use Case for downloading a template file.
/// </summary>
public sealed class DownloadTemplateUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService) : IUseCase<DownloadTemplateQuery, DownloadTemplateResult>
{
    public async Task<DownloadTemplateResult> ExecuteAsync(DownloadTemplateQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        var version = template.GetPublishedVersion();

        var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey);
        (string contentType, string normalizedExt) = ext.ToUpperInvariant() switch
        {
            ".DOCX" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"),
            ".XLSX" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
            ".HTML" => ("text/html; charset=utf-8", ".html"),
            _ => ("text/html; charset=utf-8", ext)
        };

        return new DownloadTemplateResult(stream, contentType, $"{template.Slug}{normalizedExt}");
    }
}
