using System.Net;
using SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.UpdateProjectEgressCidrs;

public sealed class UpdateProjectEgressCidrsUseCase(
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<UpdateProjectEgressCidrsCommand, ProjectControlPlaneResultDto>
{
    public async Task<ProjectControlPlaneResultDto> ExecuteAsync(UpdateProjectEgressCidrsCommand command, CancellationToken ct)
    {
        var projectId = new ProjectId(command.ProjectId);
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException("Project", command.ProjectId);

        if (project.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        var normalized = command.AllowedCidrs
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(NormalizeEntry)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        project.SetAllowedEgressCidrs(normalized.Length == 0 ? null : string.Join(',', normalized), timeProvider.GetUtcNow());
        projectRepo.Update(project);
        await unitOfWork.CommitAsync(ct);

        return ProjectControlPlaneMapper.ToDto(project);
    }

    private static string NormalizeEntry(string entry)
    {
        var trimmed = entry.Trim();
        if (IPNetwork.TryParse(trimmed, out var network))
        {
            return network.ToString();
        }

        if (IPAddress.TryParse(trimmed, out var ip))
        {
            int prefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            return new IPNetwork(ip, prefix).ToString();
        }

        throw new ValidationException("AllowedCidrs", $"Invalid IP or CIDR entry '{entry}'.");
    }
}
