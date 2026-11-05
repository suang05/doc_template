using SmkDoc.Application.Modules.Integration.Datasets.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Strategy interface for database-specific SQL query execution and transactional read-only enforcement.
/// </summary>
public interface ISqlDriverStrategy
{
    /// <summary>
    /// Unique provider identifier (e.g. "PostgreSQL", "SqlServer", "MySQL", "Oracle").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Executes a read-only query against the database using engine-specific driver and returns JSON string.
    /// </summary>
    Task<string> ExecuteQueryAsJsonAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int commandTimeoutSeconds,
        CancellationToken ct);

    /// <summary>
    /// Executes a dry-run read-only query against the database, extracting column schema and bounded sample rows.
    /// </summary>
    Task<DryRunExecutionResult> ExecuteDryRunAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int maxRows,
        int commandTimeoutSeconds,
        CancellationToken ct);
}

