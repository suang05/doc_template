using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectQueryService projectQueryService,
    IUserRepository userRepo) : IUseCase<ListProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListProjectsQuery query, CancellationToken ct = default)
    {
        var uId = new UserId(query.UserId);
        var user = await userRepo.GetByIdAsync(uId, ct);
        var isSuperAdmin = user?.SystemRole == SystemRole.SuperAdmin;

        var (items, totalCount) = await projectQueryService.ListPagedByUserAsync(
            uId,
            isSuperAdmin,
            query.SearchTerm,
            query.Page,
            query.PageSize,
            ct);

        return new ProjectPagedResultDto(items, totalCount, query.Page, query.PageSize);
    }
}
