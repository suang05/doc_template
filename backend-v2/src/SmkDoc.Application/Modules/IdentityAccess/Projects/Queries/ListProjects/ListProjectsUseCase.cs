using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectQueryService projectQueryService,
    TimeProvider timeProvider) : IUseCase<ListProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListProjectsQuery query, CancellationToken ct)
    {
        var uId = new UserId(query.UserId);
        var now = timeProvider.GetUtcNow();

        var (items, totalCount) = await projectQueryService.ListAccessiblePagedAsync(
            uId,
            now,
            query.SearchTerm,
            query.Page,
            query.Limit,
            query.IncludeTotal,
            ct);

        return new ProjectPagedResultDto(items, totalCount, query.Page, query.Limit);
    }
}
