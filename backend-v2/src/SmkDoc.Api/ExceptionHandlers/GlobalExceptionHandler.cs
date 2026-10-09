using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.ExceptionHandlers;

/// <summary>
/// Centralized ASP.NET Core exception handler (.NET 8/10) conforming to RFC 9457 Problem Details.
/// Catches unhandled exceptions across the entire HTTP pipeline (Middlewares, UseCases, Controllers).
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = ResolveExceptionDetails(exception);

        // 1. Semantic Logging (Zero string interpolation in log message templates)
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled 5xx Server Error: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning("Handled 4xx Client/Domain Error [{ErrorCode}]: {Message}", errorCode, exception.Message);
        }

        // 2. Build RFC 9457 ProblemDetails
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://api.sammakorn.co.th/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}",
            Instance = httpContext.Request.Path,
            Detail = status == StatusCodes.Status500InternalServerError && exception is not DomainException
                ? "An unexpected error occurred."
                : exception.Message
        };

        problemDetails.Extensions["errorCode"] = errorCode;

        // 3. Attach rich validation errors payload
        if (exception is SchemaValidationException schemaEx)
        {
            problemDetails.Extensions["errors"] = schemaEx.Errors.Select(e => new
            {
                path = e.Field,
                message = e.Message,
                rule = e.Rule
            }).ToArray();
        }
        else if (exception is ValidationException validationEx)
        {
            problemDetails.Extensions["errors"] = validationEx.Errors;
        }

        // 4. Output response via IProblemDetailsService with fallback
        httpContext.Response.StatusCode = status;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);
        }

        return true;
    }

    private static (int Status, string Title, string ErrorCode) ResolveExceptionDetails(Exception exception) =>
        exception switch
        {
            NotFoundException notFound           => (StatusCodes.Status404NotFound, "Not Found", notFound.ErrorCode),
            DraftExpiredException draftExpired   => (StatusCodes.Status410Gone, "Draft Expired", draftExpired.ErrorCode),
            ConflictException conflict           => (StatusCodes.Status409Conflict, "Conflict", conflict.ErrorCode),
            RenderException render               => (StatusCodes.Status500InternalServerError, "Render Failed", render.ErrorCode),
            UnauthorizedException unauthorized   => (StatusCodes.Status401Unauthorized, "Unauthorized", unauthorized.ErrorCode),
            DomainValidationException domainVal  => (StatusCodes.Status400BadRequest, "Domain Validation Error", domainVal.ErrorCode),
            BusinessRuleViolationException rule  => (StatusCodes.Status400BadRequest, "Business Rule Violation", rule.ErrorCode),
            SchemaValidationException schema     => (StatusCodes.Status400BadRequest, "Schema Validation Failed", schema.ErrorCode),
            ValidationException validation       => (StatusCodes.Status400BadRequest, "Validation Failed", validation.ErrorCode),
            DomainException domain               => (StatusCodes.Status400BadRequest, "Domain Error", domain.ErrorCode),
            UnauthorizedAccessException          => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            ArgumentException                    => (StatusCodes.Status400BadRequest, "Bad Request", "BAD_REQUEST"),
            InvalidOperationException            => (StatusCodes.Status409Conflict, "Conflict", "INVALID_OPERATION"),
            _                                    => (StatusCodes.Status500InternalServerError, "Internal Server Error", "INTERNAL_SERVER_ERROR")
        };
}
