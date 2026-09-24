using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Data;

public class SqlExecutorService : ISqlExecutorService
{
    public async Task<string> ExecuteQueryAsJsonAsync(string provider, string connectionString, string sqlQuery, IDictionary<string, object>? parameters = null)
    {
        if (provider.ToLowerInvariant() != "postgresql")
        {
            throw new NotSupportedException($"Data connection provider '{provider}' is not currently supported.");
        }

        using IDbConnection dbConnection = new NpgsqlConnection(connectionString);
        
        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var results = await dbConnection.QueryAsync(sqlQuery, dapperParams);
        return JsonSerializer.Serialize(results);
    }
}
