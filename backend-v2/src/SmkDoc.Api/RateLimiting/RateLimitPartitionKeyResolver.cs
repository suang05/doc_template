using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SmkDoc.Api.Constants;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.RateLimiting;

/// <summary>
/// Resolves deterministic, privacy-preserving rate-limit partition keys across all API replicas:
/// 1. project:{ProjectId} for global tenant fairness when project context is resolved.
/// 2. apikey:{SHA-256(token)} — hashes secret M2M keys before storage in Redis/memory.
/// 3. user:{UserId} — isolates authenticated Web Studio users behind shared corporate NATs.
/// 4. ip:{RemoteIpAddress} — bounds unauthenticated traffic by client IP.
/// </summary>
public static class RateLimitPartitionKeyResolver
{
    public static string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (HttpHeaderConstants.TryExtractApiKey(context.Request.Headers, out var rawApiKey))
        {
            var hashHex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey)));
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
