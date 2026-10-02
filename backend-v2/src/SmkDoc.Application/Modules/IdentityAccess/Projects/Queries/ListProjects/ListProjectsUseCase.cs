using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectRepository projectRepo,
    IUserProjectRoleRepository userRoleRepo) : IUseCase<ListProjectsQuery, IEnumerable<ProjectResultDto>>
{
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IUserProjectRoleRepository _userRoleRepo = userRoleRepo;

    public async Task<IEnumerable<ProjectResultDto>> ExecuteAsync(ListProjectsQuery query, CancellationToken ct = default)
    {
        var roles = await _userRoleRepo.ListByUserAsync(query.UserId, ct);
        var projectIds = roles.Select(r => r.ProjectId).ToList();

        if (projectIds.Count == 0)
        {
            return Enumerable.Empty<ProjectResultDto>();
        }

        var projects = await _projectRepo.ListByIdsAsync(projectIds, ct);
        return projects
            .Where(p => p.IsActive)
            .Select(p => new ProjectResultDto(p.Id, p.Name, p.Slug, p.IsActive, p.CreatedAt));
    }
}
