using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Template"/> aggregate roots.
/// </summary>
public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct = default);
    Task<Template?> GetByIdWithDetailsAsync(TemplateId id, CancellationToken ct = default);
    Task<Template?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Template?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Template>> ListAsync(CancellationToken ct = default);
    Task<TemplateVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default);
    Task<TemplateVersion?> GetCurrentVersionAsync(TemplateId templateId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVersion>> ListVersionsAsync(TemplateId templateId, CancellationToken ct = default);
    Task AddAsync(Template template, CancellationToken ct = default);
    Task AddVersionAsync(TemplateVersion version, CancellationToken ct = default);
    void Update(Template template);
    void Remove(Template template);
}
