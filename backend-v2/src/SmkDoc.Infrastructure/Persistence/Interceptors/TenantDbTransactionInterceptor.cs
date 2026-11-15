using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Binds transaction-local (<c>is_local = true</c>) PostgreSQL session settings when EF Core starts a database
/// transaction, reinforcing RLS tenant isolation even across pooled or ambient connection switches.
/// </summary>
public sealed class TenantDbTransactionInterceptor(ICurrentProjectContext projectContext) : DbTransactionInterceptor
{
    private const string SetLocalConfigSql =
        "SELECT set_config('app.current_project_id', @pid, true), set_config('app.control_plane_context', @cp, true);";

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        if (connection is NpgsqlConnection npgsqlConnection && result is NpgsqlTransaction npgsqlTx)
        {
            using var cmd = npgsqlConnection.CreateCommand();
            cmd.Transaction = npgsqlTx;
            ConfigureParameters(cmd, projectContext);
            cmd.ExecuteNonQuery();
        }

        return base.TransactionStarted(connection, eventData, result);
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        if (connection is NpgsqlConnection npgsqlConnection && result is NpgsqlTransaction npgsqlTx)
        {
            await using var cmd = npgsqlConnection.CreateCommand();
            cmd.Transaction = npgsqlTx;
            ConfigureParameters(cmd, projectContext);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    private static void ConfigureParameters(NpgsqlCommand cmd, ICurrentProjectContext context)
    {
        cmd.CommandText = SetLocalConfigSql;
        var projectIdValue = context.TryGetProjectId()?.Value.ToString("D") ?? string.Empty;
        var controlPlaneValue = context.IsControlPlane ? "true" : string.Empty;

        cmd.Parameters.AddWithValue("pid", projectIdValue);
        cmd.Parameters.AddWithValue("cp", controlPlaneValue);
    }
}
