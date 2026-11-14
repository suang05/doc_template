using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(AppDbContext context) : IApiKeyRepository
{
    private readonly AppDbContext _context = context;

    public async Task<ApiKey?> GetByIdAsync(ApiKeyId id, ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.ProjectId == projectId, ct);
    }

    public async Task<ApiKey?> GetByIdWithoutProjectAsync(ApiKeyId id, CancellationToken ct = default)
    {
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Migrate cross-tenant ApiKey lookup to explicit RLS-exempt management query / Unscoped lookup
        return await _context.ApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.Id == id, ct);
#pragma warning restore RS0030
    }

    public async Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct = default)
    {
#pragma warning disable RS0030 // Pre-tenant API key authentication hash lookup runs before active ProjectId exists
        return await _context.ApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, ct);
#pragma warning restore RS0030
    }

    public async Task<IReadOnlyList<ApiKey>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListAllAsync(CancellationToken ct = default)
    {
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Migrate management key listing to explicit RLS management policy
        return await _context.ApiKeys
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
#pragma warning restore RS0030
    }

    public async Task AddAsync(ApiKey key, CancellationToken ct = default)
    {
        await _context.ApiKeys.AddAsync(key, ct);
    }

    public void Update(ApiKey key)
    {
        _context.ApiKeys.Update(key);
    }
}
