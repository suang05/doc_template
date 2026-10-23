using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Repository interface for managing RefreshToken lifecycle and persistence.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(Sha256Hash tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(UserId userId, DateTimeOffset now, CancellationToken ct = default);
    Task<int> PurgeExpiredTokensAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
