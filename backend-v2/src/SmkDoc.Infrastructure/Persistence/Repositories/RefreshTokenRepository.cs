using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(Sha256Hash tokenHash, CancellationToken ct = default)
    {
        return await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await context.RefreshTokens.AddAsync(token, ct);
    }

    public async Task RevokeAllByUserIdAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.Revoke(now);
        }
    }

    public async Task<int> PurgeExpiredTokensAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await context.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);
    }
}
