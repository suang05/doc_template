using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;

internal static class ProjectControlPlaneMapper
{
    internal static ProjectControlPlaneResultDto ToDto(Project project) =>
        new(
            project.Id.Value,
            project.CompanyId.Value,
            project.Name.Value,
            project.Slug.Value,
            project.IsActive,
            project.GetAllowedEgressCidrList(),
            project.CreatedAt,
            project.UpdatedAt,
            project.Version);
}
