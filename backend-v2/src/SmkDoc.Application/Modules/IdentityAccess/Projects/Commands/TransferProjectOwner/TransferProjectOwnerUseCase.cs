using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;

/// <summary>
/// SuperAdmin formal Project Ownership Transfer / Master Credential re-assignment.
/// Enforces the Chain of Trust when an IT Lead changes or emergency break-glass occurs.
/// </summary>
public sealed class TransferProjectOwnerUseCase(
    IProjectRepository projectRepo,
    IUserRepository userRepo,
    IUserProjectRoleRepository userRoleRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<TransferProjectOwnerCommand>
{
    public async Task ExecuteAsync(TransferProjectOwnerCommand command, CancellationToken ct = default)
    {
        var projectId = new ProjectId(command.ProjectId);
        var newOwnerId = new UserId(command.NewOwnerUserId);
        var project = await EnsureValidTransferEntitiesAsync(projectId, newOwnerId, ct);

        var now = timeProvider.GetUtcNow();
        project.RecordOwnershipTransfer(now);
        projectRepo.Update(project);

        await ReassignAdminRolesAsync(projectId, newOwnerId, now, ct);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task<Project> EnsureValidTransferEntitiesAsync(ProjectId projectId, UserId newOwnerId, CancellationToken ct)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId.Value}' not found.");

        var newOwner = await userRepo.GetByIdAsync(newOwnerId, ct)
            ?? throw new NotFoundException($"User '{newOwnerId.Value}' not found.");

        if (!newOwner.IsActive)
        {
            throw new DomainValidationException("Cannot transfer project ownership to an inactive user.");
        }

        return project;
    }

    private async Task ReassignAdminRolesAsync(ProjectId projectId, UserId newOwnerId, DateTimeOffset now, CancellationToken ct)
    {
        var existingRoles = await userRoleRepo.ListByProjectAsync(projectId, ct);
        DowngradePriorAdmins(existingRoles, newOwnerId);
        await AssignNewOwnerAdminRoleAsync(existingRoles, projectId, newOwnerId, now, ct);
    }

    private void DowngradePriorAdmins(IReadOnlyList<UserProjectRole> existingRoles, UserId newOwnerId)
    {
        foreach (var role in existingRoles.Where(r => r.Role == RoleType.Admin && r.UserId != newOwnerId))
        {
            role.UpdateRole(RoleType.Developer);
            userRoleRepo.Update(role);
        }
    }

    private async Task AssignNewOwnerAdminRoleAsync(
        IReadOnlyList<UserProjectRole> existingRoles,
        ProjectId projectId,
        UserId newOwnerId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var targetRole = existingRoles.FirstOrDefault(r => r.UserId == newOwnerId);
        if (targetRole != null)
        {
            targetRole.UpdateRole(RoleType.Admin);
            userRoleRepo.Update(targetRole);
            return;
        }

        var newRole = UserProjectRole.Create(newOwnerId, projectId, RoleType.Admin, now);
        await userRoleRepo.AddAsync(newRole, ct);
    }
}

