namespace SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;

/// <summary>
/// DTO representing project details.
/// </summary>
public sealed record ProjectResultDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? ReadApiKey = null,
    string? WriteApiKey = null,
    string? ConcurrencyToken = null
) : IHasConcurrencyToken;

/// <summary>
/// Paged query result for projects.
/// </summary>
public sealed record ProjectPagedResultDto(
    IReadOnlyList<ProjectResultDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
