using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Services;

/// <summary>
/// Domain application generator responsible for issuing, validating, and rotating session credentials
/// (JWT Access Tokens and persistent Refresh Tokens) under strict security invariant guardrails.
/// </summary>
public interface ITokenSessionGenerator
{
    /// <summary>
    /// Issues a new JWT access token and companion refresh token for an authenticated user session.
    /// Resolves active workspace context and tenant role deterministically.
    /// </summary>
    Task<IssuedTokensResult> IssueTokensAsync(
        User user,
        Guid? preferredProjectId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Rotates a refresh token pair: validates hash and lifetime, revokes the current token,
    /// issues replacement credentials, and triggers automated compromised session revocation if token reuse is detected.
    /// </summary>
    Task<RotatedTokensResult> RotateTokensAsync(
        string rawRefreshToken,
        Guid? preferredProjectId = null,
        CancellationToken ct = default);
}
