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
        // 1. Inspect Idempotency-Key header
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var rawKey) || string.IsNullOrWhiteSpace(rawKey))
        {
            if (mandatory)
            {
                var problemDetails = ProblemDetailsBuilder.Create(
                    status: StatusCodes.Status400BadRequest,
                    title: "Missing Idempotency-Key",
                    detail: "The 'Idempotency-Key' request header is required for this operation.",
                    errorCode: "MISSING_IDEMPOTENCY_KEY",
                    instance: context.HttpContext.Request.Path,
                    traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext));

                context.Result = new BadRequestObjectResult(problemDetails);
                return;
            }

            await next();
            return;
        }

        var idempotencyKey = rawKey.ToString().Trim();
        var callerId = projectContext.IsResolved
            ? projectContext.GetRequiredProjectId().Value.ToString("N")
            : (projectContext.UserId?.Value.ToString("N") ?? projectContext.CallerIdentity ?? "anonymous");
        var cacheKey = $"idempotency:{callerId}:{idempotencyKey}";
        var fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, jsonSerializerOptions);

        // 2. Atomic Acquisition & State Inspection
        var acquisitionResult = await idempotencyStore.TryAcquireOrGetAsync(
            cacheKey,
            fingerprint,
            InFlightTtl,
            context.HttpContext.RequestAborted);

        if (!acquisitionResult.IsAcquired && acquisitionResult.ExistingRecord is not null)
        {
            var existing = acquisitionResult.ExistingRecord;

            // Scenario A: Concurrent execution in-flight
            if (existing.Status == IdempotencyStatus.InFlight)
            {
                LogKeyInFlight(logger, idempotencyKey, callerId);

                var conflictDetails = ProblemDetailsBuilder.Create(
                    status: StatusCodes.Status409Conflict,
                    title: "Request In-Flight",
                    detail: "A mutation request with this Idempotency-Key is currently being processed.",
                    errorCode: "IDEMPOTENCY_IN_FLIGHT",
                    instance: context.HttpContext.Request.Path,
                    traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext));

                context.Result = new ConflictObjectResult(conflictDetails);
                return;
            }

            // Scenario B: Completed cached execution
            if (existing.Status == IdempotencyStatus.Completed)
            {
                // Verify request payload fingerprint
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

                    var mismatchResult = new ObjectResult(mismatchDetails)
                    {
                        StatusCode = StatusCodes.Status422UnprocessableEntity
                    };
                    context.Result = mismatchResult;
                    return;
                }

                // Replay cached response with IETF header
                context.HttpContext.Response.Headers[HeaderReplayed] = "true";

                if (existing.Headers is not null)
                {
                    foreach (var (headerName, headerValue) in existing.Headers)
                    {
                        context.HttpContext.Response.Headers[headerName] = headerValue;
                    }
                }

                var cachedStatus = existing.StatusCode ?? StatusCodes.Status200OK;
                if (!string.IsNullOrWhiteSpace(existing.ResponseJson))
                {
                    var contentResult = new ContentResult
                    {
                        Content = existing.ResponseJson,
                        ContentType = "application/json; charset=utf-8",
                        StatusCode = cachedStatus
                    };
                    context.Result = contentResult;
                }
                else
                {
                    var statusResult = new StatusCodeResult(cachedStatus);
                    context.Result = statusResult;
                }

                return;
            }
        }

        // 3. Execution Pipeline with Failure Rollback
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
            return;
        }

        // 4. Save Completed Successful Response
        if (executedContext.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            if (statusCode is >= 200 and < 300)
            {
                Dictionary<string, string>? headersToCache = null;
                if (context.HttpContext.Response.Headers.TryGetValue("Location", out var locationValue))
                {
                    headersToCache = new Dictionary<string, string>
                    {
                        ["Location"] = locationValue.ToString()
                    };
                }

                var serializedBody = JsonSerializer.Serialize(objectResult.Value, jsonSerializerOptions);

                await idempotencyStore.SaveCompletedAsync(
                    cacheKey,
                    fingerprint,
                    statusCode,
                    serializedBody,
                    headersToCache,
                    completedTtl,
                    context.HttpContext.RequestAborted);
            }
            else
            {
                // Non-2xx response: evict in-flight lock so client can retry
                await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
            }
        }
        else if (executedContext.Result is StatusCodeResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode;
            if (statusCode is >= 200 and < 300)
            {
                await idempotencyStore.SaveCompletedAsync(
                    cacheKey,
                    fingerprint,
                    statusCode,
                    string.Empty,
                    null,
                    completedTtl,
                    context.HttpContext.RequestAborted);
            }
            else
            {
                await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
            }
        }
        else
        {
            await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
        }
    }
}
