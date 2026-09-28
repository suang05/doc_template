using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="TemplateDataset"/> entities.
/// </summary>
public interface ITemplateDatasetRepository
{
    Task<List<TemplateDataset>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task AddAsync(TemplateDataset templateDataset, CancellationToken ct = default);
    void Remove(TemplateDataset templateDataset);
    void RemoveRange(IEnumerable<TemplateDataset> templateDatasets);
}
