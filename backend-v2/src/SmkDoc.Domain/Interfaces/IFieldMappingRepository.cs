using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="FieldMapping"/> entities.
/// </summary>
public interface IFieldMappingRepository
{
    Task<List<FieldMapping>> GetByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task AddAsync(FieldMapping mapping, CancellationToken ct = default);
    void Remove(FieldMapping mapping);
    void RemoveRange(IEnumerable<FieldMapping> mappings);
}
