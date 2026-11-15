using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Durable atomic PostgreSQL idempotency store backed by partial-conflict UPSERT
/// over <c>idempotency_keys</c> with transaction-local RLS binding.
/// </summary>
public sealed class PostgresIdempotencyStore(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider) : IIdempotencyStore
{
    private static readonly Guid GlobalTenantScopeId = Guid.Empty;

    public async Task<IdempotencyAcquisitionResult> TryAcquireOrGetAsync(
        IdempotencyAcquisitionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var lockedUntil = now.Add(request.InFlightTtl);
        var expiresAt = now.Add(request.CompletedTtl);
        string cacheKey = BuildCacheKey(request.ProjectId, request.CredentialId, request.Scope, request.IdempotencyKey);

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await BindTransactionTenantScopeAsync(conn, tx, request.ProjectId, ct);

        await using (var upsertCmd = conn.CreateCommand())
        {
            upsertCmd.Transaction = tx;
            upsertCmd.CommandText = """
                INSERT INTO public.idempotency_keys (
                    id, project_id, credential_id, scope, idempotency_key,
                    request_fingerprint, status, locked_until, created_at, expires_at
                )
                VALUES (
                    @p_id, @p_project_id, @p_credential_id, @p_scope, @p_key,
                    @p_fingerprint, 'in_progress', @p_locked_until, @p_now, @p_expires_at
                )
                ON CONFLICT (tenant_scope_id, credential_id, scope, idempotency_key)
                DO UPDATE SET
                    id = EXCLUDED.id,
                    request_fingerprint = EXCLUDED.request_fingerprint,
                    status = 'in_progress',
                    status_code = NULL,
                    response_json = NULL,
                    response_headers_json = NULL,
                    result_resource_id = NULL,
                    locked_until = EXCLUDED.locked_until,
                    created_at = EXCLUDED.created_at,
                    expires_at = EXCLUDED.expires_at
                WHERE public.idempotency_keys.expires_at <= @p_now
                   OR (public.idempotency_keys.status = 'in_progress' AND public.idempotency_keys.locked_until <= @p_now)
                RETURNING id;
                """;
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = Guid.CreateVersion7() });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_project_id", NpgsqlDbType.Uuid)
            {
                Value = request.ProjectId.HasValue ? request.ProjectId.Value : DBNull.Value
            });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_credential_id", NpgsqlDbType.Text) { Value = request.CredentialId });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_scope", NpgsqlDbType.Text) { Value = request.Scope });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_key", NpgsqlDbType.Text) { Value = request.IdempotencyKey });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_fingerprint", NpgsqlDbType.Text) { Value = request.RequestFingerprint });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_locked_until", NpgsqlDbType.TimestampTz) { Value = lockedUntil });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now });
            upsertCmd.Parameters.Add(new NpgsqlParameter("p_expires_at", NpgsqlDbType.TimestampTz) { Value = expiresAt });

            var acquiredId = await upsertCmd.ExecuteScalarAsync(ct);
            if (acquiredId is Guid)
            {
                await tx.CommitAsync(ct);
                return IdempotencyAcquisitionResult.Acquired();
            }
        }

        await using var selectCmd = conn.CreateCommand();
        selectCmd.Transaction = tx;
        selectCmd.CommandText = """
            SELECT request_fingerprint, status, status_code, response_json,
                   response_headers_json, created_at, result_resource_id, locked_until
            FROM public.idempotency_keys
            WHERE tenant_scope_id = @p_scope_id
              AND credential_id = @p_credential_id
              AND scope = @p_scope
              AND idempotency_key = @p_key
            LIMIT 1;
            """;
        selectCmd.Parameters.Add(new NpgsqlParameter("p_scope_id", NpgsqlDbType.Uuid) { Value = request.ProjectId ?? GlobalTenantScopeId });
        selectCmd.Parameters.Add(new NpgsqlParameter("p_credential_id", NpgsqlDbType.Text) { Value = request.CredentialId });
        selectCmd.Parameters.Add(new NpgsqlParameter("p_scope", NpgsqlDbType.Text) { Value = request.Scope });
        selectCmd.Parameters.Add(new NpgsqlParameter("p_key", NpgsqlDbType.Text) { Value = request.IdempotencyKey });

        await using var reader = await selectCmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            await tx.CommitAsync(ct);
            throw new InvalidOperationException("Idempotency key conflict occurred, but existing record could not be read.");
        }

        var existingRecord = MapRecord(reader, cacheKey);
        await reader.CloseAsync();
        await tx.CommitAsync(ct);

        return IdempotencyAcquisitionResult.Conflict(existingRecord);
    }

    public async Task SaveCompletedAsync(IdempotencyCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(request.CompletedTtl);
        string? headersJson = request.Headers is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Headers)
            : null;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await BindTransactionTenantScopeAsync(conn, tx, request.ProjectId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO public.idempotency_keys (
                id, project_id, credential_id, scope, idempotency_key,
                request_fingerprint, status, status_code, response_json,
                response_headers_json, result_resource_id, locked_until, created_at, expires_at
            )
            VALUES (
                @p_id, @p_project_id, @p_credential_id, @p_scope, @p_key,
                @p_fingerprint, 'completed', @p_status_code, CAST(@p_response_json AS jsonb),
                CAST(@p_headers_json AS jsonb), @p_result_id, @p_now, @p_now, @p_expires_at
            )
            ON CONFLICT (tenant_scope_id, credential_id, scope, idempotency_key)
            DO UPDATE SET
                request_fingerprint = EXCLUDED.request_fingerprint,
                status = 'completed',
                status_code = EXCLUDED.status_code,
                response_json = EXCLUDED.response_json,
                response_headers_json = EXCLUDED.response_headers_json,
                result_resource_id = EXCLUDED.result_resource_id,
                locked_until = EXCLUDED.locked_until,
                expires_at = EXCLUDED.expires_at;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = Guid.CreateVersion7() });
        cmd.Parameters.Add(new NpgsqlParameter("p_project_id", NpgsqlDbType.Uuid)
        {
            Value = request.ProjectId.HasValue ? request.ProjectId.Value : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("p_credential_id", NpgsqlDbType.Text) { Value = request.CredentialId });
        cmd.Parameters.Add(new NpgsqlParameter("p_scope", NpgsqlDbType.Text) { Value = request.Scope });
        cmd.Parameters.Add(new NpgsqlParameter("p_key", NpgsqlDbType.Text) { Value = request.IdempotencyKey });
        cmd.Parameters.Add(new NpgsqlParameter("p_fingerprint", NpgsqlDbType.Text) { Value = request.RequestFingerprint });
        cmd.Parameters.Add(new NpgsqlParameter("p_status_code", NpgsqlDbType.Integer) { Value = request.StatusCode });
        cmd.Parameters.Add(new NpgsqlParameter("p_response_json", NpgsqlDbType.Text) { Value = (object?)request.ResponseJson ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_headers_json", NpgsqlDbType.Text) { Value = (object?)headersJson ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_result_id", NpgsqlDbType.Uuid)
        {
            Value = request.ResultResourceId.HasValue ? request.ResultResourceId.Value : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now });
        cmd.Parameters.Add(new NpgsqlParameter("p_expires_at", NpgsqlDbType.TimestampTz) { Value = expiresAt });

        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RemoveAsync(
        Guid? projectId,
        string credentialId,
        string scope,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await BindTransactionTenantScopeAsync(conn, tx, projectId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM public.idempotency_keys
            WHERE tenant_scope_id = @p_scope_id
              AND credential_id = @p_credential_id
              AND scope = @p_scope
              AND idempotency_key = @p_key
              AND status = 'in_progress';
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_scope_id", NpgsqlDbType.Uuid) { Value = projectId ?? GlobalTenantScopeId });
        cmd.Parameters.Add(new NpgsqlParameter("p_credential_id", NpgsqlDbType.Text) { Value = credentialId });
        cmd.Parameters.Add(new NpgsqlParameter("p_scope", NpgsqlDbType.Text) { Value = scope });
        cmd.Parameters.Add(new NpgsqlParameter("p_key", NpgsqlDbType.Text) { Value = idempotencyKey });

        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task BindTransactionTenantScopeAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid? projectId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "SELECT set_config('app.current_project_id', @p_pid, true), set_config('app.control_plane_context', @p_cp, true);";
        cmd.Parameters.AddWithValue("p_pid", projectId.HasValue ? projectId.Value.ToString("D") : string.Empty);
        cmd.Parameters.AddWithValue("p_cp", projectId.HasValue ? string.Empty : "true");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IdempotencyRecord MapRecord(NpgsqlDataReader reader, string cacheKey)
    {
        string fingerprint = reader.GetString(0);
        string statusText = reader.GetString(1);
        int? statusCode = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        string? responseJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        string? headersJson = reader.IsDBNull(4) ? null : reader.GetString(4);
        DateTimeOffset createdAt = reader.GetFieldValue<DateTimeOffset>(5);
        Guid? resultResourceId = reader.IsDBNull(6) ? null : reader.GetGuid(6);
        DateTimeOffset? lockedUntil = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7);

        var status = statusText switch
        {
            "completed" => IdempotencyStatus.Completed,
            "failed" => IdempotencyStatus.Failed,
            _ => IdempotencyStatus.InFlight
        };

        IReadOnlyDictionary<string, string>? headers = string.IsNullOrWhiteSpace(headersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);

        return new IdempotencyRecord(
            cacheKey,
            fingerprint,
            status,
            statusCode,
            responseJson,
            headers,
            createdAt,
            resultResourceId,
            lockedUntil);
    }

    private static string BuildCacheKey(Guid? projectId, string credentialId, string scope, string idempotencyKey) =>
        $"{(projectId.HasValue ? projectId.Value.ToString("N") : "global")}:{credentialId}:{scope}:{idempotencyKey}";
}
