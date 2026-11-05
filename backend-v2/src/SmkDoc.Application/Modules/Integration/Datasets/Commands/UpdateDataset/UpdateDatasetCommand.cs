namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public sealed record UpdateDatasetCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid DataConnectionId,
    string SqlQuery,
    int CacheSeconds = 0,
    string? IfMatch = null);
