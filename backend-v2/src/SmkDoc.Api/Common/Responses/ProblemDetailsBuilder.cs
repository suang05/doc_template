using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmkDoc.Api.Common.Responses;

/// <summary>
/// Single source of truth for constructing RFC 9457 <see cref="ProblemDetails"/> responses
/// with standard IETF RFC 9110 type URIs, <c>errorCode</c>, and <c>traceId</c> extensions.
/// </summary>
public static class ProblemDetailsBuilder
{
    public static ProblemDetails Create(
        int status,
        string title,
        string detail,
        string errorCode,
        string? instance = null,
        string? traceId = null,
        object? errors = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = instance,
            Type = ResolveTypeUri(status)
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

    public static string ResolveTypeUri(int? statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            StatusCodes.Status410Gone => "https://tools.ietf.org/html/rfc9110#section-15.5.11",
            StatusCodes.Status412PreconditionFailed => "https://tools.ietf.org/html/rfc9110#section-15.5.13",
            StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc9110#section-15.5.21",
            StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc6585#section-4",
            StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
            _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        };

    public static string ResolveDefaultErrorCode(int? statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "BAD_REQUEST",
            StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            StatusCodes.Status404NotFound => "RESOURCE_NOT_FOUND",
            StatusCodes.Status409Conflict => "RESOURCE_CONFLICT",
            StatusCodes.Status410Gone => "RESOURCE_GONE",
            StatusCodes.Status412PreconditionFailed => "PRECONDITION_FAILED",
            StatusCodes.Status422UnprocessableEntity => "UNPROCESSABLE_ENTITY",
            StatusCodes.Status429TooManyRequests => "RATE_LIMIT_EXCEEDED",
            StatusCodes.Status503ServiceUnavailable => "SERVICE_UNAVAILABLE",
            _ => "INTERNAL_SERVER_ERROR"
        };
}

