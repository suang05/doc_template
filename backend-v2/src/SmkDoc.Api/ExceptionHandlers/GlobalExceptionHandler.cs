using System.Text.Json;
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
public sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService,
    IConfiguration? configuration = null) : IExceptionHandler
{
    private const string UnexpectedErrorDetail = "An unexpected error occurred.";
    private const string ServiceUnavailableDetail = "A required backing service is temporarily unavailable.";
    private const string GatewayTimeoutDetail = "The upstream operation timed out.";
    private const string ConcurrencyConflictDetail =
        "The resource was modified by another transaction while your request was processing. Please reload the latest state and retry.";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogClientCanceledRequest(logger, httpContext.Request.Method, httpContext.Request.Path.Value);
            }

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = 499;
            }

            return true;
        }

        // Step 1: Map exception to HTTP status, title, and stable errorCode
        var (status, title, errorCode) = ResolveExceptionDetails(exception);
        var traceId = ProblemDetailsBuilder.ResolveTraceId(httpContext);

        // Step 2: Log semantically (4xx client warning vs 5xx server error)
        LogException(exception, status, errorCode, traceId);

        // Step 3: Build standardized RFC 9457 ProblemDetails with traceId and validation errors
        var problemDetails = BuildProblemDetails(
            httpContext,
            exception,
            status,
            title,
            errorCode,
            traceId,
            ProblemDetailsBuilder.TryParseBaseUri(configuration?["Problems:BaseUri"]));

        // Step 4: Write response to HTTP pipeline
        await WriteResponseAsync(httpContext, problemDetails, exception, cancellationToken);
        return true;
    }

    private void LogException(Exception exception, int status, string errorCode, string traceId)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledServerError(logger, exception, errorCode, traceId, exception.Message);
            return;
        }

        LogHandledClientError(logger, errorCode, traceId, exception.Message);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Request {Method} {Path} was canceled by the client.")]
    private static partial void LogClientCanceledRequest(ILogger logger, string method, string? path);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unhandled 5xx Server Error [{ErrorCode}] (TraceId: {TraceId}): {Message}")]
    private static partial void LogUnhandledServerError(ILogger logger, Exception exception, string errorCode, string traceId, string message);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Handled 4xx Client/Domain Error [{ErrorCode}] (TraceId: {TraceId}): {Message}")]
    private static partial void LogHandledClientError(ILogger logger, string errorCode, string traceId, string message);

    private static ProblemDetails BuildProblemDetails(
        HttpContext httpContext,
        Exception exception,
        int status,
        string title,
        string errorCode,
        string traceId,
        Uri? typeBaseUri) =>
        ProblemDetailsBuilder.Create(
            status: status,
            title: title,
            detail: ResolveDetailMessage(exception, status),
            errorCode: errorCode,
            instance: httpContext.Request.Path,
            traceId: traceId,
            errors: ExtractValidationErrors(exception),
            typeBaseUri: typeBaseUri);

    private static string ResolveDetailMessage(Exception exception, int status) =>
        exception switch
        {
            DbUpdateConcurrencyException => ConcurrencyConflictDetail,
            _ when status == StatusCodes.Status503ServiceUnavailable => ServiceUnavailableDetail,
            _ when status == StatusCodes.Status504GatewayTimeout => GatewayTimeoutDetail,
            _ when status >= StatusCodes.Status500InternalServerError => UnexpectedErrorDetail,
            _ => exception.Message
        };

    private static object? ExtractValidationErrors(Exception exception) =>
        exception switch
        {
            SchemaValidationException schemaEx => schemaEx.Errors
                .Select(e => new ValidationErrorEntry(
                    ValidationErrorEntry.ToJsonPointer(e.Field, camelCaseSegments: false),
                    ValidationErrorEntry.NormalizeErrorCode(e.Rule, "SCHEMA_VALIDATION"),
                    e.Message))
                .ToArray(),
            ValidationException validationEx => validationEx.ErrorEntries,
            DomainValidationException domainValEx =>
            [
                new ValidationErrorEntry("/", domainValEx.ErrorCode, domainValEx.Message)
            ],
            _ => null
        };

    private async ValueTask WriteResponseAsync(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        Exception exception,
        CancellationToken ct)
    {
        var status = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;

        if (status == StatusCodes.Status401Unauthorized &&
            !httpContext.Response.Headers.ContainsKey("WWW-Authenticate"))
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

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
            PreconditionFailedException precond => precond.ErrorCode == ErrorCodes.PreconditionRequired
                ? (StatusCodes.Status428PreconditionRequired, "Precondition Required", precond.ErrorCode)
                : (StatusCodes.Status412PreconditionFailed, "Precondition Failed", precond.ErrorCode),
            DistributedStateUnavailableException unavailable => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", unavailable.ErrorCode),
            ExternalServiceException externalUnavailable => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", externalUnavailable.ErrorCode),
            DbUpdateConcurrencyException => (StatusCodes.Status412PreconditionFailed, "Precondition Failed", ErrorCodes.PreconditionFailed),
            TimeoutException or OperationCanceledException => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", ErrorCodes.GatewayTimeout),
            BadHttpRequestException or JsonException or ArgumentException => (StatusCodes.Status400BadRequest, "Malformed Request", ErrorCodes.MalformedRequest),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", ErrorCodes.Unauthorized),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", ErrorCodes.InternalError)
        };

    private static (int Status, string Title, string ErrorCode) ResolveDomainException(DomainException exception)
    {
        var (status, title) = exception switch
        {
            ProjectContextRequiredException => (StatusCodes.Status400BadRequest, "Project Context Required"),
            ProjectContextMismatchException => (StatusCodes.Status400BadRequest, "Project Context Mismatch"),
            TenantContextNotResolvedException => (StatusCodes.Status500InternalServerError, "Tenant Context Not Resolved"),
            CrossTenantAccessDeniedException => (StatusCodes.Status403Forbidden, "Cross-Tenant Access Denied"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            DraftExpiredException => (StatusCodes.Status410Gone, "Draft Expired"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ProjectDeactivatedException => (StatusCodes.Status403Forbidden, "Project Deactivated"),
            BusinessRuleViolationException => (StatusCodes.Status409Conflict, "Business Rule Violation"),
            ApiKeyExpiredException or ApiKeyRevokedException or TokenReuseCompromiseException or UnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            SchemaValidationException => (StatusCodes.Status422UnprocessableEntity, "Schema Validation Failed"),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Validation Failed"),
            DomainValidationException => (StatusCodes.Status422UnprocessableEntity, "Domain Validation Error"),
            RenderException => (StatusCodes.Status500InternalServerError, "Render Failed"),
            DataConnectionDecryptionException => (StatusCodes.Status500InternalServerError, "Data Connection Decryption Failed"),
            DatasetExecutionException => (StatusCodes.Status500InternalServerError, "Dataset Query Failed"),
            _ => (StatusCodes.Status400BadRequest, "Domain Error")
        };

        return (status, title, exception.ErrorCode);
    }
}
