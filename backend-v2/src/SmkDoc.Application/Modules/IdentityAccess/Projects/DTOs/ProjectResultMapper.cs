using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

/// <summary>
/// Maps <see cref="Project"/> aggregates to <see cref="ProjectResultDto"/>.
/// </summary>
public static class ProjectResultMapper
{
    public static ProjectResultDto ToResultDto(
        this Project project,
        string? readApiKey = null,
        string? writeApiKey = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectResultDto(
            project.Id.Value,
            project.Name.Value,
            project.Slug.Value,
            project.IsActive,
            project.CreatedAt,
            readApiKey,
            writeApiKey,
            project.Version);
    }
}
