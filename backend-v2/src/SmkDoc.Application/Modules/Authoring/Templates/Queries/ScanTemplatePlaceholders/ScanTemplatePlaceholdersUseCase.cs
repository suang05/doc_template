using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;

/// <summary>
/// Single-responsibility Query Use Case for scanning placeholders from a published template file.
/// </summary>
public sealed class ScanTemplatePlaceholdersUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    ITemplateScannerService scanner) : IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto>
{
    public async Task<ScanFieldsResultDto> ExecuteAsync(ScanTemplatePlaceholdersQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        var version = template.GetPublishedVersion();

        using var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey);
        var detailed = await scanner.ScanDetailedPlaceholdersAsync(stream, ext, ct);
        var placeholders = detailed.Select(p => p.Name).Distinct().ToList();
        return new ScanFieldsResultDto(placeholders, detailed);
    }
}
