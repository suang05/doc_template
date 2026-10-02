using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class UserProjectRoleRepository(AppDbContext context) : IUserProjectRoleRepository
{
    private readonly AppDbContext _context = context;

    public async Task<List<UserProjectRole>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.UserProjectRoles
            .Where(r => r.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task<List<UserProjectRole>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserProjectRoles
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<UserProjectRole?> GetAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _context.UserProjectRoles
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == userId, ct);
    }

    public async Task<int> CountAdminsAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.UserProjectRoles
            .CountAsync(r => r.ProjectId == projectId && r.Role == RoleType.Admin, ct);
    }

    public async Task AddAsync(UserProjectRole role, CancellationToken ct = default)
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
