using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Sets and resets PostgreSQL session GUCs (<c>app.current_project_id</c> and <c>app.control_plane_context</c>)
/// whenever Npgsql connections open or close against the pool, guaranteeing fail-closed PostgreSQL RLS
/// with zero cross-request connection pool contamination.
/// </summary>
public sealed class TenantDbConnectionInterceptor(ICurrentProjectContext projectContext) : DbConnectionInterceptor
{
    private const string SetSessionConfigSql =
        "SELECT set_config('app.current_project_id', @pid, false), set_config('app.control_plane_context', @cp, false);";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        using var cmd = npgsqlConnection.CreateCommand();
        ConfigureParameters(cmd, projectContext);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        await using var cmd = npgsqlConnection.CreateCommand();
        ConfigureParameters(cmd, projectContext);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        if (connection is NpgsqlConnection { State: ConnectionState.Open } npgsqlConnection)
        {
            try
            {
                using var cmd = npgsqlConnection.CreateCommand();
                ConfigureResetParameters(cmd);
                cmd.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // Ignore broken sockets on close; Npgsql discards faulted physical connections.
            }
        }

        return base.ConnectionClosing(connection, eventData, result);
    }

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection is NpgsqlConnection { State: ConnectionState.Open } npgsqlConnection)
        {
            try
            {
                await using var cmd = npgsqlConnection.CreateCommand();
                ConfigureResetParameters(cmd);
                await cmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (NpgsqlException)
            {
                // Ignore broken sockets on close; Npgsql discards faulted physical connections.
            }
        }

        return await base.ConnectionClosingAsync(connection, eventData, result);
    }

    private static void ConfigureParameters(NpgsqlCommand cmd, ICurrentProjectContext context)
    {
        cmd.CommandText = SetSessionConfigSql;
        var projectIdValue = context.TryGetProjectId()?.Value.ToString("D") ?? string.Empty;
        var controlPlaneValue = context.IsControlPlane ? "true" : string.Empty;

        cmd.Parameters.AddWithValue("pid", projectIdValue);
        cmd.Parameters.AddWithValue("cp", controlPlaneValue);
    }

    private static void ConfigureResetParameters(NpgsqlCommand cmd)
    {
        cmd.CommandText = SetSessionConfigSql;
        cmd.Parameters.AddWithValue("pid", string.Empty);
        cmd.Parameters.AddWithValue("cp", string.Empty);
    }
}
