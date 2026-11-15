using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(AppDbContext context) : IApiKeyRepository
{
    private readonly AppDbContext _context = context;

    public async Task<ApiKey?> GetByIdAsync(ApiKeyId id, ProjectId projectId, CancellationToken ct)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.ProjectId == projectId, ct);
    }

    public async Task<ApiKey?> GetForCurrentProjectAsync(ApiKeyId id, CancellationToken ct)
    {
        if (!_context.IsTenantResolved || _context.CurrentTenantId == ProjectId.Empty)
        {
            throw new TenantContextNotResolvedException();
        }

        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<ApiKey?> GetByIdWithoutProjectAsync(ApiKeyId id, CancellationToken ct)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListByProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListCurrentProjectAsync(CancellationToken ct)
    {
        if (!_context.IsTenantResolved || _context.CurrentTenantId == ProjectId.Empty)
        {
            throw new TenantContextNotResolvedException();
        }

        return await _context.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListAllAsync(CancellationToken ct)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ApiKey key, CancellationToken ct)
    {
        await _context.ApiKeys.AddAsync(key, ct);
    }

    public void Update(ApiKey key)
    {
        _context.ApiKeys.Update(key);
    }
}
