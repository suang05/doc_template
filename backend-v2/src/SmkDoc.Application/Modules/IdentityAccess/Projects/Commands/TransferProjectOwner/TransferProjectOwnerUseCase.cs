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
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException($"Project '{command.ProjectId}' not found.");

        var newOwnerId = new UserId(command.NewOwnerUserId);
        var newOwner = await userRepo.GetByIdAsync(newOwnerId, ct)
            ?? throw new NotFoundException($"User '{command.NewOwnerUserId}' not found.");

        if (!newOwner.IsActive)
        {
            throw new DomainValidationException("Cannot transfer project ownership to an inactive user.");
        }

        var existingRoles = await userRoleRepo.ListByProjectAsync(projectId, ct);
        var now = timeProvider.GetUtcNow();

        project.RecordOwnershipTransfer(now);
        projectRepo.Update(project);

        // Downgrade any prior Project Admin to Developer to maintain clear single ownership
        foreach (var role in existingRoles.Where(r => r.Role == RoleType.Admin && r.UserId != newOwnerId))
        {
            role.UpdateRole(RoleType.Developer);
            userRoleRepo.Update(role);
        }

        var targetRole = existingRoles.FirstOrDefault(r => r.UserId == newOwnerId);
        if (targetRole != null)
        {
            targetRole.UpdateRole(RoleType.Admin);
            userRoleRepo.Update(targetRole);
        }
        else
        {
            var newRole = UserProjectRole.Create(newOwnerId, projectId, RoleType.Admin, now);
            await userRoleRepo.AddAsync(newRole, ct);
        }

        await unitOfWork.CommitAsync(ct);
    }
}

