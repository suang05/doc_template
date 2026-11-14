using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Template"/> aggregate roots.
/// </summary>
public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct);
    Task<Template?> GetByIdWithDetailsAsync(TemplateId id, CancellationToken ct);
    Task<Template?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<Template?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct);
    Task<TemplateVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct);
    Task<IReadOnlyList<TemplateVersion>> ListVersionsAsync(TemplateId templateId, CancellationToken ct);
    Task AddAsync(Template template, CancellationToken ct);
    void Update(Template template);
    void Remove(Template template);
}
