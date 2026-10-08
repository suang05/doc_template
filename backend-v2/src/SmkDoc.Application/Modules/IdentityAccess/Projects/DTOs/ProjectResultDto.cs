namespace SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

/// <summary>
/// DTO representing project details.
/// </summary>
public record ProjectResultDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? ReadApiKey = null,
    string? WriteApiKey = null
);

/// <summary>
/// Paged query result for projects.
/// </summary>
public record ProjectPagedResultDto(
    IReadOnlyList<ProjectResultDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);