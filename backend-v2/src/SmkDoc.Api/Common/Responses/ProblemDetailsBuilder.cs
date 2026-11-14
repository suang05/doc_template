using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Common.Responses;

/// <summary>
/// Single source of truth for constructing RFC 9457 <see cref="ProblemDetails"/> responses
/// with machine-readable type URIs, <c>errorCode</c>, and W3C <c>traceId</c> extensions.
/// </summary>
public static class ProblemDetailsBuilder
{
    private const string DefaultProblemsBaseUri = "/problems";

    public static ProblemDetails Create(
        int status,
        string title,
        string detail,
        string errorCode,
        string? instance = null,
        string? traceId = null,
        object? errors = null,
        string? typeBaseUri = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = instance,
            Type = ResolveTypeUri(errorCode, typeBaseUri)
        };

        problemDetails.Extensions["errorCode"] = errorCode;

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        return problemDetails;
    }

    public static string ResolveTraceId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var activityTraceId = Activity.Current?.TraceId;
        if (activityTraceId.HasValue && activityTraceId.Value != default)
        {
            return activityTraceId.Value.ToHexString();
        }

        return context.TraceIdentifier;
    }

    public static string ResolveTypeUri(string? errorCode, string? baseUri = null)
    {
        var prefix = string.IsNullOrWhiteSpace(baseUri)
            ? DefaultProblemsBaseUri
            : baseUri.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return $"{prefix}/internal-error";
        }

        var slug = errorCode.Trim()
            .Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"{prefix}/{slug}";
    }

    public static string ResolveTypeUri(int? statusCode, string? baseUri = null) =>
        ResolveTypeUri(ResolveDefaultErrorCode(statusCode), baseUri);

    public static string ResolveDefaultErrorCode(int? statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.MalformedRequest,
            StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            StatusCodes.Status409Conflict => ErrorCodes.ResourceConflict,
            StatusCodes.Status410Gone => ErrorCodes.DraftExpired,
            StatusCodes.Status412PreconditionFailed => ErrorCodes.PreconditionFailed,
            StatusCodes.Status422UnprocessableEntity => ErrorCodes.ValidationFailed,
            StatusCodes.Status428PreconditionRequired => ErrorCodes.PreconditionRequired,
            StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
            StatusCodes.Status503ServiceUnavailable => ErrorCodes.ExternalServiceUnavailable,
            StatusCodes.Status504GatewayTimeout => ErrorCodes.GatewayTimeout,
            _ => ErrorCodes.InternalError
        };
}

