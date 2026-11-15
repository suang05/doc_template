using System.Text.Json.Serialization;

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
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => Id;
}
