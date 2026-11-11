using SmkDoc.Application.Common.Constants;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Services;

/// <summary>
/// Immutable result containing renewed token pair and the authenticated user identity upon rotation.
/// </summary>
public sealed record RotatedTokensResult(
    UserId UserId,
    string AccessToken,
    string NewRefreshToken,
    int ExpiresIn = AuthConstants.AccessTokenLifetimeSeconds,
    string TokenType = AuthConstants.BearerTokenType);

