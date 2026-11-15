using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectQueryService projectQueryService,
    TimeProvider timeProvider) : IUseCase<ListProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListProjectsQuery query, CancellationToken ct)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var uId = new UserId(query.UserId);
        var now = timeProvider.GetUtcNow();

        var (items, totalCount) = await projectQueryService.ListAccessiblePagedAsync(
            uId,
            now,
            query.SearchTerm,
            page,
            pageSize,
            ct);

        return new ProjectPagedResultDto(items, totalCount, page, pageSize);
    }
}
