namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;

public sealed record CreateDatasetCommand(
    string Name,
    string? Description,
    Guid DataConnectionId,
    string SqlQuery,
    int CacheSeconds = 0);
