using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;

/// <summary>
/// Single-responsibility Query Use Case for downloading a template file.
/// </summary>
public sealed class DownloadTemplateUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService) : IUseCase<DownloadTemplateQuery, DownloadTemplateResult>
{
    public async Task<DownloadTemplateResult> ExecuteAsync(DownloadTemplateQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        if (template.CurrentVersionId is null)
            throw new BusinessRuleViolationException($"Template '{template.Id}' has no published version.");

        var version = template.CurrentVersion
            ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version record for template '{template.Id}' not found.");

        var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        string contentType = ext switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/html; charset=utf-8"
        };

        return new DownloadTemplateResult(stream, contentType, $"{template.Slug}{ext}");
    }
}
