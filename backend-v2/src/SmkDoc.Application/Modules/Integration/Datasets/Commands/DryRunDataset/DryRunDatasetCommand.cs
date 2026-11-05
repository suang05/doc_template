namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;

/// <summary>
/// Command to test-run a SQL dataset query and extract column schema and sample records.
/// </summary>
public sealed record DryRunDatasetCommand(
    Guid? DataConnectionId,
    string? Provider,
    string? ConnectionString,
    string SqlQuery,
    IDictionary<string, object>? Parameters = null,
    int MaxSampleRows = 5);

