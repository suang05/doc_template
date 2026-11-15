using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

public sealed class ListControlPlaneProjectsUseCase(
    IProjectQueryService projectQueryService) : IUseCase<ListControlPlaneProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListControlPlaneProjectsQuery query, CancellationToken ct)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await projectQueryService.ListAllControlPlanePagedAsync(
            query.SearchTerm,
            page,
            pageSize,
            ct);

        return new ProjectPagedResultDto(items, totalCount, page, pageSize);
    }
}
