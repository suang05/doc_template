using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Persistence.Queries;

/// <summary>
/// Perimeter identity/tenancy resolver backed by short-TTL <see cref="IMemoryCache"/> entries,
/// RLS-safe PostgreSQL <c>SECURITY DEFINER</c> functions, and domain-event / Redis PubSub invalidation.
/// </summary>
public sealed partial class TenantResolutionResolver(
    IMemoryCache memoryCache,
    ILogger<TenantResolutionResolver> logger,
    NpgsqlDataSource? dataSource = null,
    IServiceScopeFactory? scopeFactory = null,
    IConnectionMultiplexer? redis = null) : ITenantResolutionResolver
{
    private static readonly TimeSpan ProjectCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MembershipCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UserActiveCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BreakGlassPositiveMaxTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BreakGlassNegativeTtl = TimeSpan.FromSeconds(5);
    private static readonly RedisChannel InvalidationChannel = RedisChannel.Literal("smkdoc:tenant-cache:invalidate");

    private int _subscribed;

    public async Task<ResolvedProjectState?> FindProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        EnsureSubscribed();
        string cacheKey = ProjectKey(projectId);
        if (memoryCache.TryGetValue(cacheKey, out CachedProjectWrapper? cached))
        {
            return cached?.Value;
        }

        ResolvedProjectState? resolved = dataSource is not null
            ? await QueryProjectFromNpgsqlAsync(dataSource, projectId, ct)
            : await QueryProjectFromEfAsync(projectId, ct);

        memoryCache.Set(cacheKey, new CachedProjectWrapper(resolved), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ProjectCacheTtl
        });

        return resolved;
    }

    public async Task<ResolvedProjectMembership?> FindMembershipAsync(ProjectId projectId, UserId userId, CancellationToken ct)
    {
        EnsureSubscribed();
        string cacheKey = MembershipKey(projectId, userId);
        if (memoryCache.TryGetValue(cacheKey, out CachedMembershipWrapper? cached))
        {
            return cached?.Value;
        }

        ResolvedProjectMembership? resolved = dataSource is not null
            ? await QueryMembershipFromNpgsqlAsync(dataSource, projectId, userId, ct)
            : await QueryMembershipFromEfAsync(projectId, userId, ct);

        memoryCache.Set(cacheKey, new CachedMembershipWrapper(resolved), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = MembershipCacheTtl
        });

        return resolved;
    }

    public async Task<bool> IsUserActiveAsync(UserId userId, CancellationToken ct)
    {
        EnsureSubscribed();
        string cacheKey = UserActiveKey(userId);
        if (memoryCache.TryGetValue(cacheKey, out bool cachedActive))
        {
            return cachedActive;
        }

        bool isActive = dataSource is not null
            ? await QueryUserActiveFromNpgsqlAsync(dataSource, userId, ct)
            : await QueryUserActiveFromEfAsync(userId, ct);

        memoryCache.Set(cacheKey, isActive, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = UserActiveCacheTtl
        });

        return isActive;
    }

    public async Task<bool> HasActiveBreakGlassGrantAsync(
        UserId superAdminUserId,
        ProjectId projectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        EnsureSubscribed();
        string cacheKey = BreakGlassKey(superAdminUserId, projectId);
        if (memoryCache.TryGetValue(cacheKey, out CachedBreakGlassGrant? cached))
        {
            return cached is not null && cached.IsActive && (!cached.ExpiresAt.HasValue || cached.ExpiresAt.Value > now);
        }

        DateTimeOffset? expiresAt = dataSource is not null
            ? await QueryActiveBreakGlassExpiryFromNpgsqlAsync(dataSource, superAdminUserId, projectId, now, ct)
            : await QueryActiveBreakGlassExpiryFromEfAsync(superAdminUserId, projectId, now, ct);

        bool active = expiresAt.HasValue && expiresAt.Value > now;
        var ttl = active && expiresAt.HasValue
            ? MinDuration(BreakGlassPositiveMaxTtl, expiresAt.Value - now)
            : BreakGlassNegativeTtl;

        if (ttl > TimeSpan.Zero)
        {
            memoryCache.Set(cacheKey, new CachedBreakGlassGrant(active, expiresAt), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });
        }

        return active;
    }

    public Task InvalidateProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        memoryCache.Remove(ProjectKey(projectId));
        return PublishInvalidationAsync($"p:{projectId.Value:D}", ct);
    }

    public Task InvalidateUserAsync(UserId userId, CancellationToken ct)
    {
        memoryCache.Remove(UserActiveKey(userId));
        return PublishInvalidationAsync($"u:{userId.Value:D}", ct);
    }

    public Task InvalidateBreakGlassAsync(UserId superAdminUserId, ProjectId projectId, CancellationToken ct)
    {
        memoryCache.Remove(BreakGlassKey(superAdminUserId, projectId));
        return PublishInvalidationAsync($"bg:{superAdminUserId.Value:D}:{projectId.Value:D}", ct);
    }

    private void EnsureSubscribed()
    {
        if (redis is not { IsConnected: true })
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _subscribed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var subscriber = redis.GetSubscriber();
            subscriber.Subscribe(InvalidationChannel, (_, value) => ApplyRemoteInvalidation(value.ToString()));
        }
        catch (RedisException ex)
        {
            LogRedisInvalidationFailure(logger, ex);
            Volatile.Write(ref _subscribed, 0);
        }
    }

    private void ApplyRemoteInvalidation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var parts = message.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0] == "p" && Guid.TryParse(parts[1], out var projectId))
        {
            memoryCache.Remove(ProjectKey(new ProjectId(projectId)));
        }
        else if (parts.Length == 2 && parts[0] == "u" && Guid.TryParse(parts[1], out var userId))
        {
            memoryCache.Remove(UserActiveKey(new UserId(userId)));
        }
        else if (parts.Length == 3 &&
                 parts[0] == "bg" &&
                 Guid.TryParse(parts[1], out var adminId) &&
                 Guid.TryParse(parts[2], out var targetProjectId))
        {
            memoryCache.Remove(BreakGlassKey(new UserId(adminId), new ProjectId(targetProjectId)));
        }
    }

    private async Task PublishInvalidationAsync(string payload, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (redis is not { IsConnected: true })
        {
            return;
        }

        try
        {
            await redis.GetSubscriber().PublishAsync(InvalidationChannel, payload, CommandFlags.FireAndForget);
        }
        catch (RedisException ex)
        {
            LogRedisInvalidationFailure(logger, ex);
        }
    }

    private static async Task<ResolvedProjectState?> QueryProjectFromNpgsqlAsync(
        NpgsqlDataSource source,
        ProjectId projectId,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, is_active FROM public.projects WHERE id = @p_id LIMIT 1;";
        cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = projectId.Value });

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ResolvedProjectState(reader.GetGuid(0), reader.GetBoolean(1));
    }

    private static async Task<ResolvedProjectMembership?> QueryMembershipFromNpgsqlAsync(
        NpgsqlDataSource source,
        ProjectId projectId,
        UserId userId,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT project_id, user_id, role FROM public.fn_resolve_user_project_role_for_auth(@p_project_id, @p_user_id);";
        cmd.Parameters.Add(new NpgsqlParameter("p_project_id", NpgsqlDbType.Uuid) { Value = projectId.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_user_id", NpgsqlDbType.Uuid) { Value = userId.Value });

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ResolvedProjectMembership(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2));
    }

    private static async Task<bool> QueryUserActiveFromNpgsqlAsync(
        NpgsqlDataSource source,
        UserId userId,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT is_active FROM public.users WHERE id = @p_user_id LIMIT 1;";
        cmd.Parameters.Add(new NpgsqlParameter("p_user_id", NpgsqlDbType.Uuid) { Value = userId.Value });

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task<DateTimeOffset?> QueryActiveBreakGlassExpiryFromNpgsqlAsync(
        NpgsqlDataSource source,
        UserId superAdminUserId,
        ProjectId projectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT MAX(expires_at)
            FROM public.break_glass_grants
            WHERE super_admin_user_id = @p_admin_id
              AND target_project_id = @p_project_id
              AND revoked_at IS NULL
              AND granted_at <= @p_now
              AND expires_at > @p_now;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_admin_id", NpgsqlDbType.Uuid) { Value = superAdminUserId.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_project_id", NpgsqlDbType.Uuid) { Value = projectId.Value });
        cmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now });

        var result = await cmd.ExecuteScalarAsync(ct);
        return result switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => null
        };
    }

    private async Task<ResolvedProjectState?> QueryProjectFromEfAsync(ProjectId projectId, CancellationToken ct)
    {
        if (scopeFactory is null)
        {
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.IsActive })
            .FirstOrDefaultAsync(ct);

        return project is null ? null : new ResolvedProjectState(project.Id.Value, project.IsActive);
    }

    private async Task<ResolvedProjectMembership?> QueryMembershipFromEfAsync(ProjectId projectId, UserId userId, CancellationToken ct)
    {
        if (scopeFactory is null)
        {
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.UserProjectRoles
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.UserId == userId)
            .Select(r => new { r.ProjectId, r.UserId, RoleName = r.Role.Name })
            .FirstOrDefaultAsync(ct);

        return membership is null ? null : new ResolvedProjectMembership(membership.ProjectId.Value, membership.UserId.Value, membership.RoleName);
    }

    private async Task<bool> QueryUserActiveFromEfAsync(UserId userId, CancellationToken ct)
    {
        if (scopeFactory is null)
        {
            return false;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, ct);
    }

    private async Task<DateTimeOffset?> QueryActiveBreakGlassExpiryFromEfAsync(
        UserId superAdminUserId,
        ProjectId projectId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (scopeFactory is null)
        {
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BreakGlassGrants
            .AsNoTracking()
            .Where(g => g.SuperAdminUserId == superAdminUserId &&
                        g.TargetProjectId == projectId &&
                        g.RevokedAt == null &&
                        g.GrantedAt <= now &&
                        g.ExpiresAt > now)
            .MaxAsync(g => (DateTimeOffset?)g.ExpiresAt, ct);
    }

    private static string ProjectKey(ProjectId projectId) => $"tenant:proj:{projectId.Value:N}";
    private static string MembershipKey(ProjectId projectId, UserId userId) => $"tenant:member:{projectId.Value:N}:{userId.Value:N}";
    private static string UserActiveKey(UserId userId) => $"tenant:user:{userId.Value:N}";
    private static string BreakGlassKey(UserId superAdminUserId, ProjectId projectId) => $"tenant:bg:{superAdminUserId.Value:N}:{projectId.Value:N}";

    private static TimeSpan MinDuration(TimeSpan a, TimeSpan b) => a <= b ? a : b;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis tenant cache invalidation channel encountered a transient error; falling back to local cache TTL.")]
    private static partial void LogRedisInvalidationFailure(ILogger logger, Exception ex);

    private sealed record CachedProjectWrapper(ResolvedProjectState? Value);
    private sealed record CachedMembershipWrapper(ResolvedProjectMembership? Value);
    private sealed record CachedBreakGlassGrant(bool IsActive, DateTimeOffset? ExpiresAt);
}
