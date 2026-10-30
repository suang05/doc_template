using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Data;

public class SqlExecutorService(
    ISqlConnectionValidator? sqlValidator = null,
    IConfiguration? configuration = null) : ISqlExecutorService
{
    private const int DefaultCommandTimeoutSeconds = 15;
    private readonly ISqlConnectionValidator _sqlValidator = sqlValidator ?? new SqlSecurityGuard(configuration);

    public Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters = null) =>
        ExecuteQueryAsJsonAsync(provider, connectionString, sqlQuery, parameters, default);

    public async Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider) ||
            !string.Equals(provider.Trim(), "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Data connection provider '{provider}' is not currently supported.");
        }

        await _sqlValidator.ValidateConnectionTargetAsync(provider, connectionString, ct);
        _sqlValidator.ValidateReadOnlySql(sqlQuery);

        await using var dbConnection = new NpgsqlConnection(connectionString);
        await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var readOnlyGuardCommand = new CommandDefinition(
            "SET TRANSACTION READ ONLY;",
            transaction: transaction,
            commandTimeout: DefaultCommandTimeoutSeconds,
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
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: ct);

        var results = await dbConnection.QueryAsync(command);
        await transaction.CommitAsync(ct);

        return JsonSerializer.Serialize(results);
    }
}
