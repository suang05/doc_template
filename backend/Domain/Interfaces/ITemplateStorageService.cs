using Microsoft.AspNetCore.Http;
using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Handles all MinIO and local disk file operations for templates.
/// Responsible for: upload, download, delete, and sync between disk and MinIO.
/// </summary>
public interface ITemplateStorageService
{
    /// <summary>
    /// Saves an uploaded template file to local disk and MinIO.
    /// Archives the previous version before overwriting (if exists).
    /// </summary>
    Task<TemplateMetadata> SaveTemplateAsync(IFormFile file, Guid? projectId = null, bool isGlobal = false);

    /// <summary>
    /// Deletes a template from local disk, MinIO, and the database.
    /// </summary>
    Task DeleteTemplateAsync(string fileName, Guid? projectId = null);

    /// <summary>
    /// Ensures the template file exists locally; downloads from MinIO if missing.
    /// Returns the full local file path.
    /// </summary>
    Task<string> EnsureLocalFileAsync(string storagePath, string localPath);

    /// <summary>
    /// Saves a Tiptap HTML template (string content) to local disk, MinIO, and PostgreSQL.
    /// Sets EngineType = Tiptap automatically.
    /// </summary>
    Task<TemplateMetadata> SaveHtmlTemplateAsync(string fileName, string htmlContent, Guid? projectId = null, bool isGlobal = false);

    /// <summary>
    /// Saves a ReportBro JSON layout (string content) to local disk, MinIO, and PostgreSQL.
    /// Sets EngineType = ReportBro automatically.
    /// </summary>
    Task<TemplateMetadata> SaveJsonTemplateAsync(string fileName, string jsonContent, Guid? projectId = null, bool isGlobal = false);

    /// <summary>
    /// Syncs all templates found in local Templates/ directory into MinIO and PostgreSQL.
    /// Used for startup reconciliation.
    /// </summary>
    Task SyncLocalTemplatesAsync();
}
