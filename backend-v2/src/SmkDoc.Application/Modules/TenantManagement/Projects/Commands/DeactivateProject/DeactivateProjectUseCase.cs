using SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.DeactivateProject;

public sealed class DeactivateProjectUseCase(
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<DeactivateProjectCommand, ProjectControlPlaneResultDto>
{
    public async Task<ProjectControlPlaneResultDto> ExecuteAsync(DeactivateProjectCommand command, CancellationToken ct)
    {
        var projectId = new ProjectId(command.ProjectId);
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", command.ProjectId);

        if (project.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        project.Deactivate(timeProvider.GetUtcNow());
        projectRepo.Update(project);
        await unitOfWork.CommitAsync(ct);

        return ProjectControlPlaneMapper.ToDto(project);
    }
}
