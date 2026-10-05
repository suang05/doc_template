namespace SmkDoc.Api.Contracts.Integration.Datasets;

public record UpdateDatasetRequest(
    string Name,
    string? Description,
    Guid DataConnectionId,
    string SqlQuery,
    int CacheSeconds = 0
);
