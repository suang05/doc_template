using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmkDoc.Api.Common.Responses;

/// <summary>
/// Single source of truth for constructing RFC 9457 <see cref="ProblemDetails"/> responses
/// with standardized error type URIs, <c>errorCode</c>, and <c>traceId</c> extensions.
/// </summary>
public static class SmkDocProblemDetailsFactory
{
    public const string ErrorTypeBaseUri = "https://api.sammakorn.co.th/errors/";

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
            Type = BuildTypeUri(errorCode)
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

    public static string BuildTypeUri(string errorCode) =>
        $"{ErrorTypeBaseUri}{errorCode.ToLowerInvariant().Replace('_', '-')}";

    public static string ResolveDefaultErrorCode(int? statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "BAD_REQUEST",
            StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            StatusCodes.Status404NotFound => "RESOURCE_NOT_FOUND",
            StatusCodes.Status409Conflict => "RESOURCE_CONFLICT",
            StatusCodes.Status412PreconditionFailed => "PRECONDITION_FAILED",
            StatusCodes.Status422UnprocessableEntity => "UNPROCESSABLE_ENTITY",
            StatusCodes.Status429TooManyRequests => "RATE_LIMIT_EXCEEDED",
            _ => "INTERNAL_SERVER_ERROR"
        };
}

