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
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var uId = new UserId(query.UserId);
        var user = await userRepo.GetByIdAsync(uId, ct);
        var isSuperAdmin = user?.SystemRole == SystemRole.SuperAdmin;

        var (items, totalCount) = await projectQueryService.ListPagedByUserAsync(
            uId,
            isSuperAdmin,
            query.SearchTerm,
            page,
            pageSize,
            ct);

        return new ProjectPagedResultDto(items, totalCount, page, pageSize);
    }
}
