using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(AppDbContext context) : IApiKeyRepository
{
    private readonly AppDbContext _context = context;

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var keyId = new ApiKeyId(id);
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId, ct);
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, Guid projectId, CancellationToken ct = default)
    {
        var keyId = new ApiKeyId(id);
        var pId = new ProjectId(projectId);
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == pId, ct);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(Sha256Hash keyHash, CancellationToken ct = default)
    {
        return await _context.ApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var pId = new ProjectId(projectId);
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == pId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByNameAsync(Guid projectId, ApiKeyName name, CancellationToken ct = default)
    {
        var pId = new ProjectId(projectId);
        return await _context.ApiKeys.AnyAsync(k => k.ProjectId == pId && k.Name == name, ct);
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
