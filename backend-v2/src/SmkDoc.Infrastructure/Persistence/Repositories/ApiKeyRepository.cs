using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(AppDbContext context) : IApiKeyRepository
{
    private readonly AppDbContext _context = context;

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, Guid projectId, CancellationToken ct = default)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.ProjectId == projectId, ct);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
    {
        return await _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
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
