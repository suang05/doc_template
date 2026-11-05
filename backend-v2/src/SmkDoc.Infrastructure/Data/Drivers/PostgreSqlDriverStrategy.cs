using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;

namespace SmkDoc.Infrastructure.Data.Drivers;

/// <summary>
/// PostgreSQL database driver strategy utilizing Npgsql with transaction-level read-only guard.
/// </summary>
public sealed class PostgreSqlDriverStrategy : ISqlDriverStrategy
{
    public string ProviderName => "PostgreSQL";

    public async Task<string> ExecuteQueryAsJsonAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int commandTimeoutSeconds,
        CancellationToken ct)
    {
        await using var dbConnection = new NpgsqlConnection(connectionString);
        await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var readOnlyGuardCommand = new CommandDefinition(
            "SET TRANSACTION READ ONLY;",
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);
        await dbConnection.ExecuteAsync(readOnlyGuardCommand);

        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var command = new CommandDefinition(
            sqlQuery,
            dapperParams,
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);

        var results = await dbConnection.QueryAsync(command);
        await transaction.CommitAsync(ct);

        return JsonSerializer.Serialize(results);
    }

    public async Task<DryRunExecutionResult> ExecuteDryRunAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int maxRows,
        int commandTimeoutSeconds,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var dbConnection = new NpgsqlConnection(connectionString);
        await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var readOnlyGuardCommand = new CommandDefinition(
            "SET TRANSACTION READ ONLY;",
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);
        await dbConnection.ExecuteAsync(readOnlyGuardCommand);

        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var command = new CommandDefinition(
            sqlQuery,
            dapperParams,
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);

        await using var reader = await dbConnection.ExecuteReaderAsync(command);

        var columnSchema = await reader.GetColumnSchemaAsync(ct);
        var columns = columnSchema.Select(c => new DatasetColumnSchemaDto(
            c.ColumnName,
            c.DataTypeName ?? c.DataType?.Name ?? "string",
            c.AllowDBNull ?? true)).ToList();

        var rows = new List<IDictionary<string, object?>>();
        int boundedLimit = Math.Clamp(maxRows, 1, 50);

        while (await reader.ReadAsync(ct) && rows.Count < boundedLimit)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.GetValue(i);
                row[reader.GetName(i)] = val is DBNull ? null : val;
            }
            rows.Add(row);
        }

        await transaction.CommitAsync(ct);
        stopwatch.Stop();

        return new DryRunExecutionResult(columns, rows, stopwatch.ElapsedMilliseconds);
    }
}

