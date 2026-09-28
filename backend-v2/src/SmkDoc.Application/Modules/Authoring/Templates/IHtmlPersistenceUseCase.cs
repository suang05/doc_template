using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates;

/// <summary>
/// Stateful use case handling HTML template persistence, MinIO versioning,
/// legal audit trail records, and database state updates.
/// </summary>
public interface IHtmlPersistenceUseCase
{
    /// <summary>
    /// Persists a new version of an HTML template: uploads versioned archive and active files to MinIO,
    /// increments the template version in the database, and updates the active pointer atomically.
    /// </summary>
    Task<int> SaveHtmlVersionAsync(Guid templateId, SaveTemplateHtmlCommand request, CancellationToken ct = default);
}
