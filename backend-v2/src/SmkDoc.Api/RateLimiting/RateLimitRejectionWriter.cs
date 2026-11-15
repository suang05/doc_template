using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.RateLimiting;

/// <summary>
/// Writes standardized RFC 9457 <c>429 Too Many Requests</c> ProblemDetails responses
/// with dynamic <c>Retry-After</c> headers and accurate policy-specific detail messages.
/// </summary>
public static class RateLimitRejectionWriter
{
    public static async ValueTask WriteAsync(
        OnRejectedContext context,
        IConfiguration configuration,
        string? policyName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        int retrySeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retrySeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString(CultureInfo.InvariantCulture);
        context.HttpContext.Response.ContentType = "application/problem+json";

        string detail = policyName switch
        {
            "auth-strict" => "Authentication rate limit exceeded. Please wait before retrying.",
            "connectivity-check" or "test-conn" => "Connectivity check rate limit exceeded. Please wait before retrying.",
            "rendering" => "Concurrent document rendering limit exceeded. Please retry after the backoff interval.",
            _ => "API rate limit exceeded for the current caller partition. Please respect Retry-After before retrying."
        };

        var problemDetails = ProblemDetailsBuilder.Create(
            status: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests",
            detail: detail,
            errorCode: ErrorCodes.RateLimited,
            instance: context.HttpContext.Request.Path,
            traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext),
            typeBaseUri: ProblemDetailsBuilder.TryParseBaseUri(configuration["Problems:BaseUri"]));

        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
    }
}
