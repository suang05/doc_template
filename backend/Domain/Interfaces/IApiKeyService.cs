using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Domain.Interfaces;

public interface IApiKeyService
{
    /// <summary>
    /// Validates an API key against the database and returns the Project and ApiKey context.
    /// Returns null if invalid or inactive.
    /// </summary>
    Task<(Project Project, ApiKey ApiKey)?> ValidateApiKeyAsync(string apiKey);

    /// <summary>
    /// Ensures default system project and master API key exist on startup.
    /// </summary>
    Task EnsureDefaultProjectAndKeyAsync(string defaultKey);

    /// <summary>
    /// Lists all projects with their API keys.
    /// </summary>
    Task<List<Project>> GetAllProjectsAsync();

    /// <summary>
    /// Creates a new project / subsystem.
    /// </summary>
    Task<Project> CreateProjectAsync(string code, string name, string? description = null);

    /// <summary>
    /// Generates a new API Key for a project.
    /// </summary>
    Task<ApiKey> CreateApiKeyAsync(Guid projectId, string name, DateTime? expiresAt = null);

    /// <summary>
    /// Revokes / toggles active status of an API Key.
    /// </summary>
    Task<bool> RevokeApiKeyAsync(Guid apiKeyId);
}
