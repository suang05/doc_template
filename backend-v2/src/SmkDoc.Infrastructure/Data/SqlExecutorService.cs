using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Data;

public class SqlExecutorService : ISqlExecutorService
{
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
        if (provider.ToLowerInvariant() != "postgresql")
        {
            throw new NotSupportedException($"Data connection provider '{provider}' is not currently supported.");
        }

        await using var dbConnection = new NpgsqlConnection(connectionString);

        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var command = new CommandDefinition(sqlQuery, dapperParams, cancellationToken: ct);
        var results = await dbConnection.QueryAsync(command);
        return JsonSerializer.Serialize(results);
    }
}
