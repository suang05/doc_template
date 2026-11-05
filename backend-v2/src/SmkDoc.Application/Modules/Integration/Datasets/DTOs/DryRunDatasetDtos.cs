namespace SmkDoc.Application.Modules.Integration.Datasets.DTOs;

/// <summary>
/// Schema metadata for a single column discovered from query execution.
/// </summary>
public sealed record DatasetColumnSchemaDto(
    string Name,
    string DataType,
    bool IsNullable = true);

/// <summary>
/// Result returned from dry-running a SQL dataset query, containing discovered column schema and sample rows.
/// </summary>
public sealed record DryRunDatasetResultDto(
    bool Success,
    IReadOnlyList<DatasetColumnSchemaDto> Columns,
    IReadOnlyList<IDictionary<string, object?>> SampleRows,
    int SampleCount,
    long ExecutionTimeMs,
    string? Error = null);

/// <summary>
/// Low-level execution result returned by database driver strategies.
/// </summary>
public sealed record DryRunExecutionResult(
    IReadOnlyList<DatasetColumnSchemaDto> Columns,
    IReadOnlyList<IDictionary<string, object?>> SampleRows,
    long ExecutionTimeMs);

