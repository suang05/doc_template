namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Result returned upon successful login authentication.
/// </summary>
public record LoginResultDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; } = 86400; // 24 hours
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
        string tokenType = "Bearer",
        int expiresIn = 86400,
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
