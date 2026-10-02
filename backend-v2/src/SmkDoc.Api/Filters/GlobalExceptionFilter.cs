using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Global MVC Action Filter that converts Domain Exceptions and Framework Exceptions into RFC 7807 Problem Details responses.
/// All controllers should rely on this filter instead of using try-catch blocks.
/// Hierarchy:
///   SchemaValidationException → 400 (with RFC 7807 errors array)
///   DomainException           → Maps to domainEx.StatusCode (404, 409, 410, 500) with errorCode
///   ArgumentException         → 400 Bad Request
///   UnauthorizedAccessException → 401 Unauthorized
///   InvalidOperationException → 409 Conflict
///   Exception (fallback)      → 500 Internal Server Error
/// </summary>
public sealed class GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        // SchemaValidationException has a richer body — handle separately
        if (context.Exception is SchemaValidationException schemaEx)
        {
            HandleSchemaValidation(context, schemaEx);
            return;
        }

        // ValidationException (FluentValidation) maps to HTTP 400 with errors dictionary
        if (context.Exception is ValidationException validationEx)
        {
            HandleValidationException(context, validationEx);
            return;
        }

        int status;
        string title;

        if (context.Exception is DomainException domainEx)
        {
            (status, title) = domainEx switch
            {
                NotFoundException     => (StatusCodes.Status404NotFound, "Not Found"),
                DraftExpiredException => (StatusCodes.Status410Gone, "Draft Expired"),
                ConflictException     => (StatusCodes.Status409Conflict, "Conflict"),
                RenderException       => (StatusCodes.Status500InternalServerError, "Render Failed"),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                _                     => (StatusCodes.Status400BadRequest, "Domain Error")
            };
        }
        else
        {
            (status, title) = context.Exception switch
            {
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                ArgumentException           => (StatusCodes.Status400BadRequest,   "Bad Request"),
                InvalidOperationException   => (StatusCodes.Status409Conflict,     "Conflict"),
                _                           => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };
        }

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title  = title,
            Detail = status == StatusCodes.Status500InternalServerError && context.Exception is not DomainException
                ? "An unexpected error occurred."
                : context.Exception.Message
        };

        if (context.Exception is DomainException de)
        {
            problemDetails.Extensions["errorCode"] = de.ErrorCode;
        }

        context.Result = new ObjectResult(problemDetails) { StatusCode = status };
        context.ExceptionHandled = true;
    }

    private static void HandleSchemaValidation(ExceptionContext context, SchemaValidationException ex)
    {
        var errors = ex.Errors.Select(e => new
        {
            path    = e.Field,
            message = e.Message,
            rule    = e.Rule
        }).ToArray();

        context.Result = new ObjectResult(new
        {
            type     = "https://tools.ietf.org/html/rfc7807",
            title    = "Schema Validation Failed",
            status   = StatusCodes.Status400BadRequest,
            detail   = ex.Message,
            instance = $"/api/v1/documents/generate/{ex.TemplateSlug}",
            errorCode = ex.ErrorCode,
            errors
        }) { StatusCode = StatusCodes.Status400BadRequest };

        context.ExceptionHandled = true;
    }

    private static void HandleValidationException(ExceptionContext context, ValidationException ex)
    {
        var problemDetails = new ProblemDetails
        {
            Type   = "https://tools.ietf.org/html/rfc7807",
            Title  = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message
        };

        problemDetails.Extensions["errorCode"] = ex.ErrorCode;
        problemDetails.Extensions["errors"] = ex.Errors;

        context.Result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
        context.ExceptionHandled = true;
    }
}
