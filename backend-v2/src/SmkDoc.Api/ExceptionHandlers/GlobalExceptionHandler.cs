using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.ExceptionHandlers;

/// <summary>
/// Centralized ASP.NET Core exception handler (.NET 10) conforming to RFC 9457 Problem Details.
/// Catches unhandled exceptions across the entire HTTP pipeline (Middlewares, UseCases, Controllers).
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string UnexpectedErrorDetail = "An unexpected error occurred.";
    private const string ConcurrencyConflictDetail =
        "The resource was modified by another transaction while your request was processing. Please reload the latest state and retry.";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Step 1: Map exception to HTTP status, title, and stable errorCode
        var (status, title, errorCode) = ResolveExceptionDetails(exception);

        // Step 2: Log semantically (4xx client warning vs 5xx server error)
        LogException(exception, status, errorCode, httpContext.TraceIdentifier);

        // Step 3: Build standardized RFC 9457 ProblemDetails with traceId and validation errors
        var problemDetails = BuildProblemDetails(httpContext, exception, status, title, errorCode);

        // Step 4: Write response to HTTP pipeline
        await WriteResponseAsync(httpContext, problemDetails, exception, cancellationToken);
        return true;
    }

    private void LogException(Exception exception, int status, string errorCode, string traceId)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled 5xx Server Error [{ErrorCode}] (TraceId: {TraceId}): {Message}", errorCode, traceId, exception.Message);
            return;
        }

        logger.LogWarning("Handled 4xx Client/Domain Error [{ErrorCode}] (TraceId: {TraceId}): {Message}", errorCode, traceId, exception.Message);
    }

    private static ProblemDetails BuildProblemDetails(
        HttpContext httpContext,
        Exception exception,
        int status,
        string title,
        string errorCode) =>
        ProblemDetailsBuilder.Create(
            status: status,
            title: title,
            detail: ResolveDetailMessage(exception, status),
            errorCode: errorCode,
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier,
            errors: ExtractValidationErrors(exception));

    private static string ResolveDetailMessage(Exception exception, int status) =>
        exception switch
        {
            DbUpdateConcurrencyException => ConcurrencyConflictDetail,
            _ when status == StatusCodes.Status500InternalServerError && exception is not DomainException => UnexpectedErrorDetail,
            _ => exception.Message
        };

    private static object? ExtractValidationErrors(Exception exception) =>
        exception switch
        {
            SchemaValidationException schemaEx => schemaEx.Errors
                .Select(e => new { path = e.Field, message = e.Message, rule = e.Rule })
                .ToArray(),
            ValidationException validationEx => validationEx.Errors,
            _ => null
        };

    private async ValueTask WriteResponseAsync(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        Exception exception,
        CancellationToken ct)
    {
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: ct);
        }
    }

    private static (int Status, string Title, string ErrorCode) ResolveExceptionDetails(Exception exception) =>
        exception switch
        {
            DomainException domainEx => ResolveDomainException(domainEx),
            PreconditionFailedException precond => (StatusCodes.Status412PreconditionFailed, "Precondition Failed", precond.ErrorCode),
            DistributedStateUnavailableException unavailable => (StatusCodes.Status503ServiceUnavailable, "Distributed State Unavailable", unavailable.ErrorCode),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency Conflict", "CONCURRENCY_CONFLICT"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "BAD_REQUEST"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "INTERNAL_SERVER_ERROR")
        };

    private static (int Status, string Title, string ErrorCode) ResolveDomainException(DomainException exception)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            DraftExpiredException => (StatusCodes.Status410Gone, "Draft Expired"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ProjectDeactivatedException => (StatusCodes.Status403Forbidden, "Project Deactivated"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            SchemaValidationException => (StatusCodes.Status422UnprocessableEntity, "Schema Validation Failed"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed"),
            DomainValidationException => (StatusCodes.Status400BadRequest, "Domain Validation Error"),
            BusinessRuleViolationException => (StatusCodes.Status400BadRequest, "Business Rule Violation"),
            RenderException => (StatusCodes.Status500InternalServerError, "Render Failed"),
            DataConnectionDecryptionException => (StatusCodes.Status500InternalServerError, "Data Connection Decryption Failed"),
            DatasetExecutionException => (StatusCodes.Status500InternalServerError, "Dataset Query Failed"),
            _ => (StatusCodes.Status400BadRequest, "Domain Error")
        };

        return (status, title, exception.ErrorCode);
    }
}
