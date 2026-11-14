using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class UserProjectRoleRepository(AppDbContext context) : IUserProjectRoleRepository
{
    private readonly AppDbContext _context = context;

    private ProjectId RequireCurrentTenantId() =>
        _context.IsTenantResolved && _context.CurrentTenantId != ProjectId.Empty
            ? _context.CurrentTenantId
            : throw new TenantContextNotResolvedException();

    public Task<IReadOnlyList<UserProjectRole>> ListForCurrentProjectAsync(CancellationToken ct) =>
        ListByProjectAsync(RequireCurrentTenantId(), ct);

    public Task<UserProjectRole?> GetForCurrentProjectAsync(UserId userId, CancellationToken ct) =>
        GetAsync(RequireCurrentTenantId(), userId, ct);

    public Task<int> CountAdminsForCurrentProjectAsync(CancellationToken ct) =>
        CountAdminsAsync(RequireCurrentTenantId(), ct);

    public async Task<IReadOnlyList<UserProjectRole>> ListByProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        return await _context.UserProjectRoles
            .Where(r => r.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserProjectRole>> ListByUserAsync(UserId userId, CancellationToken ct)
    {
        return await _context.UserProjectRoles
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<UserProjectRole?> GetAsync(ProjectId projectId, UserId userId, CancellationToken ct)
    {
        return await _context.UserProjectRoles
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == userId, ct);
    }

    public async Task<int> CountAdminsAsync(ProjectId projectId, CancellationToken ct)
    {
        return await _context.UserProjectRoles
            .CountAsync(r => r.ProjectId == projectId && r.Role == RoleType.Admin, ct);
    }

    public async Task AddAsync(UserProjectRole role, CancellationToken ct)
    {
        await _context.UserProjectRoles.AddAsync(role, ct);
    }

    public void Update(UserProjectRole role)
    {
        _context.UserProjectRoles.Update(role);
    }

    public void Remove(UserProjectRole role)
    {
        _context.UserProjectRoles.Remove(role);
    }
}
