using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;

/// <summary>
/// Single-responsibility Query Use Case for scanning placeholders from a published template file.
/// </summary>
public sealed class ScanTemplatePlaceholdersUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    ITemplateScannerService scanner) : IUseCase<ScanTemplatePlaceholdersQuery, List<string>>
{
    public async Task<List<string>> ExecuteAsync(ScanTemplatePlaceholdersQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        if (template.CurrentVersionId is null)
            throw new BusinessRuleViolationException($"Template '{template.Id}' has no published version.");

        var version = template.CurrentVersion
            ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version record for template '{template.Id}' not found.");

        using var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        return await scanner.ScanPlaceholdersAsync(stream, ext, ct);
    }
}
