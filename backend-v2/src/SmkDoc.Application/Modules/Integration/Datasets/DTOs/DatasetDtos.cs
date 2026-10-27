namespace SmkDoc.Application.Modules.Integration.Datasets.DTOs;

/// <summary>
/// DTO representing a dataset query and cache configuration.
/// </summary>
public sealed record DatasetDto(
    Guid Id,
    string Name,
    string? Description,
    Guid DataConnectionId,
    string DataConnectionName,
    string SqlQuery,
    int CacheSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
