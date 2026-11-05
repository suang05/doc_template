namespace SmkDoc.Api.Contracts.Integration.Datasets;

/// <summary>
/// Request payload to test-run a SQL dataset query and discover column schemas.
/// </summary>
public sealed record DryRunDatasetRequest(
    Guid? DataConnectionId,
    string? Provider,
    string? ConnectionString,
    string SqlQuery,
    IDictionary<string, object>? Parameters = null,
    int MaxSampleRows = 5);

