using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Template"/> aggregate roots.
/// </summary>
public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Template?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid projectId, CancellationToken ct = default);
    Task<List<Template>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Template template, CancellationToken ct = default);
    void Update(Template template);
    void Remove(Template template);
}
