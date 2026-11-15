using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.RateLimiting;

/// <summary>
/// Resolves deterministic, privacy-preserving rate-limit partition keys across all API replicas:
/// 1. project:{ProjectId} for global tenant fairness when project context is resolved.
/// 2. apikey:{SHA-256(X-API-Key)} — hashes secret M2M keys before storage in Redis/memory.
/// 3. user:{UserId} — isolates authenticated Web Studio users behind shared corporate NATs.
/// 4. ip:{RemoteIpAddress} — bounds unauthenticated traffic by client IP.
/// </summary>
public static class RateLimitPartitionKeyResolver
{
    public static string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rawApiKey = context.Request.Headers["X-API-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(rawApiKey))
        {
            var normalizedKey = rawApiKey.Trim();
            var hashHex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey)));
            return $"apikey:{hashHex}";
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return $"user:{userId.Trim()}";
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp)
            ? "ip:unknown"
            : $"ip:{remoteIp}";
    }

    public static string ResolveGlobalPartition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var projectContext = context.RequestServices.GetService<ICurrentProjectContext>();
        if (projectContext?.TryGetProjectId() is { } projectId && !projectId.IsEmpty)
        {
            return $"project:{projectId.Value:N}:{Resolve(context)}";
        }

        return Resolve(context);
    }
}
