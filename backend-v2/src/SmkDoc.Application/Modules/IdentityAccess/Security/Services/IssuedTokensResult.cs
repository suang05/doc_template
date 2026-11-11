using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Services;

/// <summary>
/// Immutable result containing tokens and workspace context issued upon successful authentication.
/// </summary>
public sealed record IssuedTokensResult(
    string AccessToken,
    string RefreshToken,
    UserWorkspaceContextResultDto Workspace,
    IReadOnlyList<ApiKeyResultDto> ActiveApiKeys,
    int ExpiresIn = AuthConstants.AccessTokenLifetimeSeconds,
    string TokenType = AuthConstants.BearerTokenType);

