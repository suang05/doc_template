namespace SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

/// <summary>
/// DTO representing project details.
/// </summary>
public record ProjectResultDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt
);