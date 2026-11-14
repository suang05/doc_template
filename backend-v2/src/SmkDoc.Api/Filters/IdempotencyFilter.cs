using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Action filter enforcing the IETF Idempotency-Key specification on state-mutating HTTP endpoints.
/// Supports atomic in-flight locking, replay of completed responses, payload mismatch detection,
/// and automatic rollback on failure/exceptions.
/// </summary>
public sealed partial class IdempotencyFilter(
    IIdempotencyStore idempotencyStore,
    ICurrentProjectContext projectContext,
    ILogger<IdempotencyFilter> logger,
    JsonSerializerOptions jsonSerializerOptions,
    TimeSpan completedTtl,
    bool mandatory) : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string HeaderReplayed = "Idempotency-Replayed";
    private static readonly TimeSpan InFlightTtl = TimeSpan.FromMinutes(2);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Idempotency key {IdempotencyKey} is already in-flight for caller {CallerId}")]
    private static partial void LogKeyInFlight(ILogger logger, string idempotencyKey, string callerId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Idempotency key {IdempotencyKey} payload mismatch for caller {CallerId}")]
    private static partial void LogKeyPayloadMismatch(ILogger logger, string idempotencyKey, string callerId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Operation resulted in unhandled exception for Idempotency-Key {IdempotencyKey}. Rolling back in-flight lock.")]
    private static partial void LogUnhandledRollback(ILogger logger, string idempotencyKey);

    public IdempotencyFilter(
        IIdempotencyStore idempotencyStore,
        ICurrentProjectContext projectContext,
        ILogger<IdempotencyFilter> logger,
        IOptions<JsonOptions> jsonOptions)
        : this(
            idempotencyStore,
            projectContext,
            logger,
            jsonOptions.Value.JsonSerializerOptions,
            TimeSpan.FromHours(24),
            false)
    {
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var rawKey) || string.IsNullOrWhiteSpace(rawKey))
        {
            if (mandatory)
            {
                context.Result = new BadRequestObjectResult(ProblemDetailsBuilder.Create(
                    status: StatusCodes.Status400BadRequest,
                    title: "Missing Idempotency-Key",
                    detail: "The 'Idempotency-Key' request header is required for this operation.",
                    errorCode: "MISSING_IDEMPOTENCY_KEY",
                    instance: context.HttpContext.Request.Path,
                    traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext)));
                return;
            }

            await next();
            return;
        }

        var idempotencyKey = rawKey.ToString().Trim();
        var callerId = ResolveCallerId();
        var cacheKey = $"idempotency:{callerId}:{idempotencyKey}";
        var fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, jsonSerializerOptions);

        var acquisitionResult = await idempotencyStore.TryAcquireOrGetAsync(
            cacheKey,
            fingerprint,
            InFlightTtl,
            context.HttpContext.RequestAborted);

        if (!acquisitionResult.IsAcquired &&
            acquisitionResult.ExistingRecord is not null &&
            TryHandleExistingRecord(context, acquisitionResult.ExistingRecord, idempotencyKey, callerId, fingerprint))
        {
            return;
        }

        var executedContext = await ExecuteWithRollbackAsync(context, next, cacheKey, idempotencyKey);
        if (executedContext is not null)
        {
            await PersistOrEvictResponseAsync(context, executedContext, cacheKey, fingerprint);
        }
    }

    private string ResolveCallerId() =>
        projectContext.IsResolved
            ? projectContext.GetRequiredProjectId().Value.ToString("N")
            : (projectContext.UserId?.Value.ToString("N") ?? projectContext.CallerIdentity ?? "anonymous");

    private bool TryHandleExistingRecord(
        ActionExecutingContext context,
        IdempotencyRecord existing,
        string idempotencyKey,
        string callerId,
        string fingerprint)
    {
        if (existing.Status == IdempotencyStatus.InFlight)
        {
            LogKeyInFlight(logger, idempotencyKey, callerId);
            context.Result = new ConflictObjectResult(ProblemDetailsBuilder.Create(
                status: StatusCodes.Status409Conflict,
                title: "Request In-Flight",
                detail: "A mutation request with this Idempotency-Key is currently being processed.",
                errorCode: "IDEMPOTENCY_IN_FLIGHT",
                instance: context.HttpContext.Request.Path,
                traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext)));
            return true;
        }

        if (existing.Status != IdempotencyStatus.Completed)
        {
            return false;
        }

        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            LogKeyPayloadMismatch(logger, idempotencyKey, callerId);
            var mismatchDetails = ProblemDetailsBuilder.Create(
                status: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency-Key Payload Mismatch",
                detail: "This Idempotency-Key was previously used with a different request payload.",
                errorCode: "IDEMPOTENCY_PAYLOAD_MISMATCH",
                instance: context.HttpContext.Request.Path,
                traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext));

            context.Result = new ObjectResult(mismatchDetails)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
            return true;
        }

        ReplayCachedResponse(context, existing);
        return true;
    }

    private static void ReplayCachedResponse(ActionExecutingContext context, IdempotencyRecord existing)
    {
        context.HttpContext.Response.Headers[HeaderReplayed] = "true";

        if (existing.Headers is not null)
        {
            foreach (var (headerName, headerValue) in existing.Headers)
            {
                context.HttpContext.Response.Headers[headerName] = headerValue;
            }
        }

        var cachedStatus = existing.StatusCode ?? StatusCodes.Status200OK;
        context.Result = !string.IsNullOrWhiteSpace(existing.ResponseJson)
            ? new ContentResult
            {
                Content = existing.ResponseJson,
                ContentType = "application/json; charset=utf-8",
                StatusCode = cachedStatus
            }
            : new StatusCodeResult(cachedStatus);
    }

    private async Task<ActionExecutedContext?> ExecuteWithRollbackAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        string cacheKey,
        string idempotencyKey)
    {
        ActionExecutedContext executedContext;
        try
        {
            executedContext = await next();
        }
        catch (Exception)
        {
            await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
            throw;
        }

        if (executedContext.Exception is not null)
        {
            LogUnhandledRollback(logger, idempotencyKey);
            await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
            return null;
        }

        return executedContext;
    }

    private async Task PersistOrEvictResponseAsync(
        ActionExecutingContext context,
        ActionExecutedContext executedContext,
        string cacheKey,
        string fingerprint)
    {
        if (executedContext.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            await SaveOrEvictSuccessStatusAsync(
                context.HttpContext,
                cacheKey,
                fingerprint,
                statusCode,
                () => (
                    JsonSerializer.Serialize(objectResult.Value, jsonSerializerOptions),
                    ExtractLocationHeaders(context.HttpContext.Response.Headers)));
            return;
        }

        if (executedContext.Result is StatusCodeResult statusCodeResult)
        {
            await SaveOrEvictSuccessStatusAsync(
                context.HttpContext,
                cacheKey,
                fingerprint,
                statusCodeResult.StatusCode,
                static () => (string.Empty, null));
            return;
        }

        await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
    }

    private async Task SaveOrEvictSuccessStatusAsync(
        HttpContext httpContext,
        string cacheKey,
        string fingerprint,
        int statusCode,
        Func<(string Body, Dictionary<string, string>? Headers)> payloadFactory)
    {
        if (statusCode is >= 200 and < 300)
        {
            var (body, headers) = payloadFactory();
            await idempotencyStore.SaveCompletedAsync(
                cacheKey,
                fingerprint,
                statusCode,
                body,
                headers,
                completedTtl,
                httpContext.RequestAborted);
            return;
        }

        await idempotencyStore.RemoveAsync(cacheKey, httpContext.RequestAborted);
    }

    private static Dictionary<string, string>? ExtractLocationHeaders(IHeaderDictionary responseHeaders) =>
        responseHeaders.TryGetValue("Location", out var locationValue)
            ? new Dictionary<string, string> { ["Location"] = locationValue.ToString() }
            : null;
}
