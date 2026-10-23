using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectRepository projectRepo,
    IUserRepository userRepo) : IUseCase<ListProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListProjectsQuery query, CancellationToken ct = default)
    {
        var uId = new UserId(query.UserId);
        var user = await userRepo.GetByIdAsync(uId, ct);
        var isSuperAdmin = user?.SystemRole == SystemRole.SuperAdmin;

        var (items, totalCount) = await projectRepo.ListPagedByUserAsync(
            uId,
            isSuperAdmin,
            query.SearchTerm,
            query.Page,
            query.PageSize,
            ct);

        var dtos = items
            .Select(p => new ProjectResultDto(p.Id.Value, p.Name.Value, p.Slug.Value, p.IsActive, p.CreatedAt))
            .ToList();

        return new ProjectPagedResultDto(dtos, totalCount, query.Page, query.PageSize);
    }
}
