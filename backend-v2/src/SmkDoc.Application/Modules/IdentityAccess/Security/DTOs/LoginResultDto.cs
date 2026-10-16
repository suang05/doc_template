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

    public LoginResultDto() { }

    public LoginResultDto(
        string accessToken,
        UserProfileDto user,
        IReadOnlyList<AccessibleProjectDto> accessibleProjects,
        Guid? defaultProjectId,
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
        TokenType = tokenType;
        ExpiresIn = expiresIn;
    }
}
