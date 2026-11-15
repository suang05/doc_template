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

    /// <summary>
    /// Executes a read-only query using a pre-resolved pinned egress IP target to eliminate DNS rebinding TOCTOU.
    /// </summary>
    Task<string> ExecuteQueryWithPinnedTargetAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int commandTimeoutSeconds,
        ResolvedEgressTarget? pinnedTarget,
        CancellationToken ct) =>
        ExecuteQueryAsJsonAsync(connectionString, sqlQuery, parameters, commandTimeoutSeconds, ct);

    /// <summary>
    /// Executes a dry-run query using a pre-resolved pinned egress IP target to eliminate DNS rebinding TOCTOU.
    /// </summary>
    Task<DryRunExecutionResult> ExecuteDryRunWithPinnedTargetAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int maxRows,
        int commandTimeoutSeconds,
        ResolvedEgressTarget? pinnedTarget,
        CancellationToken ct) =>
        ExecuteDryRunAsync(connectionString, sqlQuery, parameters, maxRows, commandTimeoutSeconds, ct);
}

