using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.FieldMappings;

public sealed class TemplateDatasetUseCase(
    IRepository<TemplateDataset> tdRepo,
    IRepository<Template> templateRepo,
    IRepository<Dataset> datasetRepo,
    IUnitOfWork unitOfWork)
{
    private readonly IRepository<TemplateDataset> _tdRepo = tdRepo;
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<Dataset> _datasetRepo = datasetRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<TemplateDatasetDto>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        var rows = await _tdRepo.ListAsync(td => td.TemplateId == templateId, ct);

        // Enrich with dataset name
        var datasetIds = rows.Select(r => r.DatasetId).Distinct().ToList();
        var datasets   = await _datasetRepo.ListAsync(d => datasetIds.Contains(d.Id), ct);
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
        _ = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        // Validate unique aliases
        var aliases = items.Select(i => i.Alias.Trim().ToLowerInvariant()).ToList();
        if (aliases.Count != aliases.Distinct().Count())
            throw new InvalidOperationException("Dataset aliases must be unique within a template.");

        // Replace all existing assignments
        var existing = await _tdRepo.ListAsync(td => td.TemplateId == templateId, ct);
        foreach (var td in existing)
            _tdRepo.Remove(td);

        foreach (var item in items)
        {
            await _tdRepo.AddAsync(new TemplateDataset(templateId, item.DatasetId, item.Alias.Trim().ToLowerInvariant(), item.SortOrder), ct);
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
