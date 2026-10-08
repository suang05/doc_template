using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCase(
    IProjectRepository projectRepo,
    IUserRepository userRepo) : IUseCase<ListProjectsQuery, ProjectPagedResultDto>
{
    public async Task<ProjectPagedResultDto> ExecuteAsync(ListProjectsQuery query, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(query.UserId, ct);
        var isSuperAdmin = user?.SystemRole == SystemRole.SuperAdmin;

        var (items, totalCount) = await projectRepo.ListPagedByUserAsync(
            query.UserId,
            isSuperAdmin,
            query.SearchTerm,
            query.Page,
            query.PageSize,
            ct);

        var dtos = items
            .Select(p => new ProjectResultDto(p.Id, p.Name, p.Slug, p.IsActive, p.CreatedAt))
            .ToList();

        return new ProjectPagedResultDto(dtos, totalCount, query.Page, query.PageSize);
    }
}
