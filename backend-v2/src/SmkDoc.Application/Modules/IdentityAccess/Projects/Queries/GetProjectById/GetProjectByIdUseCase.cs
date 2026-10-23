using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;

public sealed class GetProjectByIdUseCase(IProjectRepository projectRepo) : IUseCase<GetProjectByIdQuery, ProjectResultDto>
{
    private readonly IProjectRepository _projectRepo = projectRepo;

    public async Task<ProjectResultDto> ExecuteAsync(GetProjectByIdQuery query, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(new ProjectId(query.ProjectId), ct)
            ?? throw new NotFoundException("Project", query.ProjectId);

        return new ProjectResultDto(project.Id.Value, project.Name.Value, project.Slug.Value, project.IsActive, project.CreatedAt);
    }
}
