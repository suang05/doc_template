using SmkDoc.Domain.Constants;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result returned upon token refresh containing a newly issued token pair.
/// </summary>
public record TokenResultDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn = SecurityConstants.AccessTokenLifetimeSeconds,
    string TokenType = SecurityConstants.BearerTokenType
);
