using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings;

public sealed class TemplateDatasetUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork)
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<TemplateDatasetDto>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(templateId, ct);
        if (template == null) return [];

        var rows = template.TemplateDatasets;

        // Enrich with dataset name
        var datasetIds = rows.Select(r => r.DatasetId).Distinct().ToList();
        var datasets   = await _datasetRepo.GetByIdsAsync(datasetIds, ct);
        var dsMap      = datasets.ToDictionary(d => d.Id, d => d.Name);

        return rows
            .OrderBy(r => r.SortOrder)
            .Select(r => new TemplateDatasetDto(
                r.Id,
                r.TemplateId,
                r.DatasetId,
                dsMap.GetValueOrDefault(r.DatasetId, "(unknown)"),
                r.Alias,
                r.SortOrder))
            .ToList();
    }

    public async Task SaveAsync(Guid templateId, List<SaveTemplateDatasetItemDto> items, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        var datasets = items.Select(item =>
            new TemplateDataset(templateId, item.DatasetId, item.Alias.Trim().ToLowerInvariant(), item.SortOrder)
        ).ToList();

        template.ReplaceDatasets(datasets);
        await _unitOfWork.CommitAsync(ct);
    }
}
