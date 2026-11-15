using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Constant-time API key verifier executing PostgreSQL <c>SECURITY DEFINER</c> lookup
/// (<c>fn_resolve_api_key_for_auth</c>) without bypassing row-level security on business tables.
/// </summary>
public sealed partial class ApiKeyVerifier(
    IOptions<ApiKeyOptions> options,
    TimeProvider timeProvider,
    NpgsqlDataSource? dataSource = null,
    IServiceScopeFactory? scopeFactory = null) : IApiKeyVerifier
{
    private const int MaxRawKeyLength = 256;

    [GeneratedRegex("^smk_(?:live|test)_([A-Za-z0-9]{12})_([A-Za-z0-9]{16,128})$", RegexOptions.CultureInvariant)]
    private static partial Regex StructuredKeyRegex();

    public async Task<VerifiedApiKeyResult?> VerifyAsync(string rawApiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawApiKey) || rawApiKey.Length > MaxRawKeyLength)
        {
            return null;
        }

        string trimmedKey = rawApiKey.Trim();
        var match = StructuredKeyRegex().Match(trimmedKey);
        string? parsedKeyId = match.Success ? match.Groups[1].Value : null;
        string? parsedSecret = match.Success ? match.Groups[2].Value : null;

        bool allowLegacy = options.Value.AllowLegacySha256Keys;
        string? legacySha256Hex = allowLegacy
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(trimmedKey)))
            : null;

        if (parsedKeyId is null && legacySha256Hex is null)
        {
            return null;
        }

        ApiKeyAuthRow? row = dataSource is not null
            ? await LookupViaNpgsqlAsync(dataSource, parsedKeyId, legacySha256Hex, ct)
            : await LookupViaScopeFactoryAsync(parsedKeyId, legacySha256Hex, ct);

        if (row is null || !row.IsActive)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (row.ExpiresAt.HasValue && row.ExpiresAt.Value <= now)
        {
            return null;
        }

        return TryVerifyStructuredKey(row, parsedSecret, options.Value.ApiKeyPepper)
            ?? TryVerifyLegacyKey(row, allowLegacy, legacySha256Hex);
    }

    private static VerifiedApiKeyResult? TryVerifyStructuredKey(
        ApiKeyAuthRow row,
        string? parsedSecret,
        string pepper)
    {
        if (parsedSecret is null ||
            row.SecretHashBytes is not { Length: 32 } ||
            string.IsNullOrWhiteSpace(pepper))
        {
            return null;
        }

        byte[] computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(parsedSecret));
        if (!CryptographicOperations.FixedTimeEquals(computed, row.SecretHashBytes))
        {
            return null;
        }

        return new VerifiedApiKeyResult(
            new ApiKeyId(row.Id),
            new ProjectId(row.ProjectId),
            ApiKeyScope.FromName(row.Scope),
            row.CallerApp,
            IsLegacySha256: false);
    }

    private static VerifiedApiKeyResult? TryVerifyLegacyKey(
        ApiKeyAuthRow row,
        bool allowLegacy,
        string? legacySha256Hex)
    {
        if (!allowLegacy ||
            legacySha256Hex is null ||
            row.SecretHashBytes is not null ||
            string.IsNullOrEmpty(row.LegacyKeyHash))
        {
            return null;
        }

        byte[] providedBytes = Encoding.UTF8.GetBytes(legacySha256Hex);
        byte[] storedBytes = Encoding.UTF8.GetBytes(row.LegacyKeyHash);
        if (providedBytes.Length != storedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, storedBytes))
        {
            return null;
        }

        return new VerifiedApiKeyResult(
            new ApiKeyId(row.Id),
            new ProjectId(row.ProjectId),
            ApiKeyScope.FromName(row.Scope),
            row.CallerApp,
            IsLegacySha256: true);
    }

    public async Task TouchUsageAsync(ApiKeyId apiKeyId, DateTimeOffset now, CancellationToken ct)
    {
        if (dataSource is not null)
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT public.fn_touch_api_key_last_used(@p_id, @p_now);";
            cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = apiKeyId.Value });
            cmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now });
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        if (scopeFactory is null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var setter = scope.ServiceProvider.GetService<ICurrentProjectContextSetter>();
        setter?.SetControlPlane("system:api-key-touch");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKeyId, ct);
        if (key is not null)
        {
            key.RecordUsage(now);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<ApiKeyAuthRow?> LookupViaNpgsqlAsync(
        NpgsqlDataSource source,
        string? parsedKeyId,
        string? legacySha256Hex,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, caller_app, scope, is_active,
                   expires_at, secret_hash_bytes, key_hash
            FROM public.fn_resolve_api_key_for_auth(@p_key_id, @p_legacy_hash);
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_key_id", NpgsqlDbType.Text) { Value = (object?)parsedKeyId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_legacy_hash", NpgsqlDbType.Text) { Value = (object?)legacySha256Hex ?? DBNull.Value });

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        DateTimeOffset? expiresAt = await reader.IsDBNullAsync(5, ct)
            ? null
            : await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
        byte[]? secretHashBytes = await reader.IsDBNullAsync(6, ct)
            ? null
            : await reader.GetFieldValueAsync<byte[]>(6, ct);
        string? legacyKeyHash = await reader.IsDBNullAsync(7, ct)
            ? null
            : await reader.GetFieldValueAsync<string>(7, ct);

        return new ApiKeyAuthRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            expiresAt,
            secretHashBytes,
            legacyKeyHash);
    }

    private async Task<ApiKeyAuthRow?> LookupViaScopeFactoryAsync(
        string? parsedKeyId,
        string? legacySha256Hex,
        CancellationToken ct)
    {
        if (scopeFactory is null)
        {
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var setter = scope.ServiceProvider.GetService<ICurrentProjectContextSetter>();
        setter?.SetControlPlane("system:api-key-verify");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Domain.Entities.ApiKey? key = null;
        if (parsedKeyId is not null)
        {
            key = await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyId == parsedKeyId, ct);
        }

        if (key is null && legacySha256Hex is not null)
        {
            var legacyHash = new Sha256Hash(legacySha256Hex);
            key = await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyHash == legacyHash, ct);
        }

        return key is null
            ? null
            : new ApiKeyAuthRow(
                key.Id.Value,
                key.ProjectId.Value,
                key.CallerApp,
                key.Scope.Name,
                key.IsActive,
                key.ExpiresAt,
                key.SecretHash?.ToArray(),
                key.KeyHash?.Value);
    }

    private sealed record ApiKeyAuthRow(
        Guid Id,
        Guid ProjectId,
        string CallerApp,
        string Scope,
        bool IsActive,
        DateTimeOffset? ExpiresAt,
        byte[]? SecretHashBytes,
        string? LegacyKeyHash);
}
