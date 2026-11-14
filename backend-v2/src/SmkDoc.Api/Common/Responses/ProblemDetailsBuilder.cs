using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        Uri? typeBaseUri = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = instance,
            Type = ResolveTypeUri(errorCode, typeBaseUri).OriginalString
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

    public static Uri? TryParseBaseUri(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Uri.TryCreate(raw.Trim(), UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : null;

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "RFC 9457 problem type URI slugs are canonical lowercase kebab-case.")]
    public static Uri ResolveTypeUri(string? errorCode, Uri? baseUri = null)
    {
        var prefix = baseUri is null
            ? DefaultProblemsBaseUri
            : baseUri.OriginalString.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return new Uri($"{prefix}/internal-error", UriKind.RelativeOrAbsolute);
        }

        var slug = errorCode.Trim()
            .Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();

        return new Uri($"{prefix}/{slug}", UriKind.RelativeOrAbsolute);
    }

    public static Uri ResolveTypeUri(int? statusCode, Uri? baseUri = null) =>
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

