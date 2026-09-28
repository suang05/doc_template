using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Templates.Queries.ScanTemplatePlaceholders;

public sealed record ScanTemplatePlaceholdersQuery(Guid Id);

/// <summary>
/// Single-responsibility Query Use Case for scanning placeholders from a published template file.
/// </summary>
public sealed class ScanTemplatePlaceholdersUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    ITemplateScannerService scanner) : IUseCase<ScanTemplatePlaceholdersQuery, List<string>>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly ITemplateScannerService _scanner = scanner;

    public async Task<List<string>> ExecuteAsync(ScanTemplatePlaceholdersQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{template.Id}' has no published version.");

        var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version record for template '{template.Id}' not found.");

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        return await _scanner.ScanPlaceholdersAsync(stream, ext, ct);
    }
}
