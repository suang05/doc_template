using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;

public sealed class GetProjectByIdUseCase(IProjectRepository projectRepo) : IUseCase<GetProjectByIdQuery, ProjectResultDto>
{
    private readonly IProjectRepository _projectRepo = projectRepo;

    public async Task<ProjectResultDto> ExecuteAsync(GetProjectByIdQuery query, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(query.ProjectId, ct)
            ?? throw new NotFoundException("Project", query.ProjectId);

        return new ProjectResultDto(project.Id, project.Name, project.Slug, project.IsActive, project.CreatedAt);
    }
}
