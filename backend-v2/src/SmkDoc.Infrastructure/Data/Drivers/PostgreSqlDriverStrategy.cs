using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

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
}

