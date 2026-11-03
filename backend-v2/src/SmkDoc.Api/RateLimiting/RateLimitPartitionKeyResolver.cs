using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SmkDoc.Api.RateLimiting;

/// <summary>
/// Resolves deterministic, privacy-preserving rate-limit partition keys across all API replicas:
/// 1. apikey:{SHA-256(X-API-Key)} — hashes secret M2M keys before storage in Redis/memory.
/// 2. user:{UserId} — isolates authenticated Web Studio users behind shared corporate NATs.
/// 3. ip:{RemoteIpAddress} — bounds unauthenticated traffic by client IP.
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
}
