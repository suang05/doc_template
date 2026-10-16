using SmkDoc.Domain.Constants;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result returned upon successful login authentication.
/// </summary>
public record LoginResultDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string TokenType { get; init; } = SecurityConstants.BearerTokenType;
    public int ExpiresIn { get; init; } = SecurityConstants.AccessTokenLifetimeSeconds;
    public UserProfileDto User { get; init; } = default!;
    public IReadOnlyList<AccessibleProjectDto> AccessibleProjects { get; init; } = [];
    public IReadOnlyList<ApiKeyDto> ActiveApiKeys { get; init; } = [];
    public Guid? DefaultProjectId { get; init; }
    public Guid? ActiveProjectId { get; init; }
    public string? ActiveRole { get; init; }

    public LoginResultDto() { }

    public LoginResultDto(
        string accessToken,
        UserProfileDto user,
        IReadOnlyList<AccessibleProjectDto> accessibleProjects,
        Guid? defaultProjectId,
        Guid? activeProjectId = null,
        string? activeRole = null,
        string tokenType = SecurityConstants.BearerTokenType,
        int expiresIn = SecurityConstants.AccessTokenLifetimeSeconds,
        string? refreshToken = null,
        IReadOnlyList<ApiKeyDto>? activeApiKeys = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        User = user;
        AccessibleProjects = accessibleProjects;
        ActiveApiKeys = activeApiKeys ?? [];
        DefaultProjectId = defaultProjectId;
        ActiveProjectId = activeProjectId ?? defaultProjectId;
        ActiveRole = activeRole;
        TokenType = tokenType;
        ExpiresIn = expiresIn;
    }
}
