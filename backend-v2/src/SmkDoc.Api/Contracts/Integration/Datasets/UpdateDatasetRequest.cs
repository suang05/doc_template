using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Integration.Datasets;

public record UpdateDatasetRequest(
    string Name,
    string? Description,
    [property: JsonRequired] Guid DataConnectionId,
    string SqlQuery,
    int? CacheSeconds = null
);
