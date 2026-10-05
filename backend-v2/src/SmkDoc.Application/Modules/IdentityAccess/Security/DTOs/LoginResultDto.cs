using System;
using System.Collections.Generic;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

/// <summary>
/// Command for authenticating a user with email and password.
/// </summary>
public record LoginCommand
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }

    public LoginCommand() {}
    public LoginCommand(string email, string password, Guid? projectId = null)
    {
        Email = email;
        Password = password;
        ProjectId = projectId;
    }
}

/// <summary>
/// User profile data embedded in login tokens and responses.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string SystemRole
);

/// <summary>
/// Project access information for an authenticated user.
/// </summary>
public record AccessibleProjectDto(
    Guid Id,
    string Name,
    string Slug,
    string Role
);

/// <summary>
/// Result returned upon successful login authentication.
/// </summary>
public record LoginResultDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string Token => AccessToken; // Backward compatibility
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; } = 86400; // 24 hours
    public UserProfileDto User { get; init; } = default!;
    public IReadOnlyList<AccessibleProjectDto> AccessibleProjects { get; init; } = [];
    public Guid? DefaultProjectId { get; init; }

    public LoginResultDto() {}
    public LoginResultDto(
        string accessToken,
        UserProfileDto user,
        IReadOnlyList<AccessibleProjectDto> accessibleProjects,
        Guid? defaultProjectId,
        string tokenType = "Bearer",
        int expiresIn = 86400)
    {
        AccessToken = accessToken;
        User = user;
        AccessibleProjects = accessibleProjects;
        DefaultProjectId = defaultProjectId;
        TokenType = tokenType;
        ExpiresIn = expiresIn;
    }
}

/// <summary>
/// DTO representing an API Key record.
/// </summary>
public record ApiKeyDto(
    Guid Id,
    string Name,
    string CallerApp,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Result returned immediately after creating an API key, containing the one-time plain-text key.
/// </summary>
public record CreateApiKeyResultDto(
    Guid Id,
    string Name,
    string CallerApp,
    string PlainTextKey
);

/// <summary>
/// DTO returned after validating an API key. Replaces passing the Domain Entity to Presentation Layer.
/// </summary>
public record ValidatedApiKeyDto(
    Guid Id,
    string CallerApp,
    Guid ProjectId
);
