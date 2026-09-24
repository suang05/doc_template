using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Handles template version history: listing versions and rolling back to a previous version.
/// </summary>
public interface ITemplateVersioningService
{
    /// <summary>Returns all recorded versions for a given template.</summary>
    Task<IEnumerable<TemplateVersion>> GetVersionsAsync(string fileName, Guid? projectId = null);

    /// <summary>
    /// Rolls back a template to the specified version number.
    /// Archives the current version before restoring the target.
    /// </summary>
    Task<TemplateMetadata> RollbackAsync(string fileName, int targetVersion, Guid? projectId = null);
}
