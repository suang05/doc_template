using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Repository interface for managing RefreshToken lifecycle and persistence.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(Sha256Hash tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);
    Task<int> PurgeExpiredTokensAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
