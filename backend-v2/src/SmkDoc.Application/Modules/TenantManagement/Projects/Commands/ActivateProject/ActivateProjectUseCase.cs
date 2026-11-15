using SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.ActivateProject;

public sealed class ActivateProjectUseCase(
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<ActivateProjectCommand, ProjectControlPlaneResultDto>
{
    public async Task<ProjectControlPlaneResultDto> ExecuteAsync(ActivateProjectCommand command, CancellationToken ct)
    {
        var projectId = new ProjectId(command.ProjectId);
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", command.ProjectId);

        if (project.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        project.Activate(timeProvider.GetUtcNow());
        projectRepo.Update(project);
        await unitOfWork.CommitAsync(ct);

        return ProjectControlPlaneMapper.ToDto(project);
    }
}
