using SmkDoc.Domain.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Users;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Security;

public sealed class UserManagementUseCase(
    IRepository<User> userRepo,
    IRepository<UserProjectRole> roleRepo,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow)
{
    private static readonly HashSet<string> ValidRoles = Enumeration.GetAll<RoleType>().Select(r => r.Name).ToHashSet();

    public async Task<List<UserResultDto>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var roles = await roleRepo.ListAsync(r => r.ProjectId == projectId, ct);
        var userIds = roles.Select(r => r.UserId).ToHashSet();
        var users = await userRepo.ListAsync(u => userIds.Contains(u.Id), ct);

        var userMap = users.ToDictionary(u => u.Id);
        return roles
            .Where(r => userMap.ContainsKey(r.UserId))
            .Select(r =>
            {
                var u = userMap[r.UserId];
                return new UserResultDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    r.Role.ToString(),
                    u.IsActive,
                    u.CreatedAt
                );
            })
            .OrderBy(u => u.Email)
            .ToList();
    }

    public async Task<UserResultDto> InviteAsync(Guid projectId, InviteUserCommand command, CancellationToken ct = default)
        => await InviteAsync(projectId, command.Email, command.Password, command.FirstName, command.LastName, command.Role, ct);

    public async Task<UserResultDto> InviteAsync(Guid projectId, string email, string password, string firstName, string lastName, string role, CancellationToken ct = default)
    {
        if (!ValidRoles.Contains(role))
            throw new ArgumentException($"Invalid role '{role}'. Must be Admin, Developer, or Viewer.");

        var roleType = RoleType.FromDisplayName<RoleType>(role);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await userRepo.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (existing != null)
        {
            var existingRole = await roleRepo.FirstOrDefaultAsync(r => r.UserId == existing.Id && r.ProjectId == projectId, ct);
            if (existingRole != null)
                throw new InvalidOperationException($"User '{normalizedEmail}' is already a member of this project.");

            await roleRepo.AddAsync(new UserProjectRole { UserId = existing.Id, ProjectId = projectId, Role = roleType });
            await uow.CommitAsync(ct);

            return new UserResultDto(
                existing.Id,
                existing.Email,
                existing.FirstName,
                existing.LastName,
                role,
                existing.IsActive,
                existing.CreatedAt
            );
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var user = new User(normalizedEmail, passwordHasher.HashPassword(password), firstName, lastName);

        // Add both atomically — user.Id is pre-assigned (Guid.NewGuid()), no intermediate save needed
        await userRepo.AddAsync(user);
        await roleRepo.AddAsync(new UserProjectRole { UserId = user.Id, ProjectId = projectId, Role = roleType });
        await uow.CommitAsync(ct);

        return new UserResultDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            role,
            user.IsActive,
            user.CreatedAt
        );
    }

    public async Task UpdateRoleAsync(Guid projectId, Guid userId, UpdateUserRoleCommand command, CancellationToken ct = default)
        => await UpdateRoleAsync(projectId, userId, command.Role, ct);

    public async Task UpdateRoleAsync(Guid projectId, Guid userId, string role, CancellationToken ct = default)
    {
        if (!ValidRoles.Contains(role))
            throw new ArgumentException($"Invalid role '{role}'.");

        var roleEntry = await roleRepo.FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        if (roleEntry.Role == RoleType.Admin && role != "Admin")
        {
            var admins = await roleRepo.ListAsync(r => r.ProjectId == projectId && r.Role == RoleType.Admin, ct);
            if (admins.Count <= 1)
                throw new InvalidOperationException("Cannot remove the last Admin from the project.");
        }

        roleEntry.UpdateRole(RoleType.FromDisplayName<RoleType>(role));
        roleRepo.Update(roleEntry);

        await uow.CommitAsync(ct);
    }

    public async Task RemoveAsync(Guid projectId, Guid userId, Guid currentUserId, CancellationToken ct = default)
    {
        if (userId == currentUserId)
            throw new InvalidOperationException("Cannot remove yourself from the project.");

        var roleEntry = await roleRepo.FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct)
            ?? throw new NotFoundException("User is not a member of this project.");

        if (roleEntry.Role == RoleType.Admin)
        {
            var admins = await roleRepo.ListAsync(r => r.ProjectId == projectId && r.Role == RoleType.Admin, ct);
            if (admins.Count <= 1)
                throw new InvalidOperationException("Cannot remove the last Admin from the project.");
        }

        roleRepo.Remove(roleEntry);
        await uow.CommitAsync(ct);
    }

    public async Task SetActiveAsync(Guid projectId, Guid userId, bool isActive, CancellationToken ct = default)
    {
        var hasRole = await roleRepo.FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct);
        if (hasRole == null) throw new NotFoundException("User is not a member of this project.");

        var user = await userRepo.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        if (isActive)
            user.Activate();
        else
            user.Deactivate();
        userRepo.Update(user);
        await uow.CommitAsync(ct);
    }
}
