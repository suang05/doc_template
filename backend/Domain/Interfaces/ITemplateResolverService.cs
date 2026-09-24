using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Resolves template metadata and file paths for document generation.
/// Handles multi-tenant scoping (project-specific vs. GLOBAL templates).
/// Falls back to MinIO download if a template is missing from local disk.
/// </summary>
public interface ITemplateResolverService
{
    /// <summary>
    /// Returns all templates visible to the given project (project-scoped + GLOBAL).
    /// </summary>
    Task<IEnumerable<TemplateMetadata>> ListTemplatesAsync(Guid? projectId = null);

    /// <summary>
    /// Returns the full local file path for a template, downloading from MinIO if necessary.
    /// Returns null if the template does not exist.
    /// </summary>
    Task<string?> ResolveTemplatePathAsync(string fileName, Guid? projectId = null);

    /// <summary>Returns the TemplateMetadata record for the given template name and project.</summary>
    Task<TemplateMetadata?> GetMetadataAsync(string fileName, Guid? projectId = null);
}
