using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

public sealed class ListControlPlaneProjectsUseCase(
    IProjectQueryService projectQueryService) : IUseCase<ListControlPlaneProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListControlPlaneProjectsQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await projectQueryService.ListAllControlPlanePagedAsync(
            query.SearchTerm,
            query.Page,
            query.Limit,
            query.IncludeTotal,
            ct);

        return new ProjectPagedResultDto(items, totalCount, query.Page, query.Limit);
    }
}
