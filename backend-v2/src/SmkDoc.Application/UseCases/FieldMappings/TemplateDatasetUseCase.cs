using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.FieldMappings;

public class TemplateDatasetUseCase
{
    private readonly IRepository<TemplateDataset> _tdRepo;
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<Dataset> _datasetRepo;
    private readonly IUnitOfWork _unitOfWork;

    public TemplateDatasetUseCase(
        IRepository<TemplateDataset> tdRepo,
        IRepository<Template> templateRepo,
        IRepository<Dataset> datasetRepo,
        IUnitOfWork unitOfWork)
    {
        _tdRepo      = tdRepo;
        _templateRepo = templateRepo;
        _datasetRepo  = datasetRepo;
        _unitOfWork   = unitOfWork;
    }

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

    public async Task SaveAsync(Guid templateId, List<SaveTemplateDatasetItem> items, CancellationToken ct = default)
    {
        _ = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new KeyNotFoundException($"Template '{templateId}' not found.");

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
            await _tdRepo.AddAsync(new TemplateDataset
            {
                Id         = Guid.NewGuid(),
                TemplateId = templateId,
                DatasetId  = item.DatasetId,
                Alias      = item.Alias.Trim().ToLowerInvariant(),
                SortOrder  = item.SortOrder,
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
