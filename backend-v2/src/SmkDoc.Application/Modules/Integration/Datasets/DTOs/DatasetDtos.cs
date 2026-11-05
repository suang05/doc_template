// SSoT backward compatibility alias for external contracts
global using DatasetDto = SmkDoc.Application.Modules.Integration.Datasets.DTOs.DatasetResultDto;

namespace SmkDoc.Application.Modules.Integration.Datasets.DTOs;

/// <summary>
/// Result DTO representing a dataset query and cache configuration.
/// </summary>
public sealed record DatasetResultDto(
    Guid Id,
    string Name,
    string? Description,
    Guid DataConnectionId,
    string DataConnectionName,
    string SqlQuery,
    int CacheSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyToken = null);
