namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed record DeleteDatasetCommand(
    Guid Id,
    long ExpectedVersion = 1);
