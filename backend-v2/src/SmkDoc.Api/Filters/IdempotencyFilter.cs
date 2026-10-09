using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Action filter enforcing the IETF Idempotency-Key specification on state-mutating HTTP endpoints.
/// Supports atomic in-flight locking, replay of completed responses, payload mismatch detection,
/// and automatic rollback on failure/exceptions.
/// </summary>
public sealed class IdempotencyFilter(
    IIdempotencyStore idempotencyStore,
    IExecutionContext executionContext,
    ILogger<IdempotencyFilter> logger,
    JsonSerializerOptions jsonSerializerOptions,
    TimeSpan completedTtl,
    bool mandatory) : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string HeaderReplayed = "Idempotency-Replayed";
    private static readonly TimeSpan InFlightTtl = TimeSpan.FromMinutes(2);

    public IdempotencyFilter(
        IIdempotencyStore idempotencyStore,
        IExecutionContext executionContext,
        ILogger<IdempotencyFilter> logger,
        IOptions<JsonOptions> jsonOptions)
        : this(
            idempotencyStore,
            executionContext,
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
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Missing Idempotency-Key",
                    Detail = "The 'Idempotency-Key' request header is required for this operation.",
                    Instance = context.HttpContext.Request.Path,
                    Type = "https://api.sammakorn.co.th/errors/missing-idempotency-key"
                };
                problemDetails.Extensions["errorCode"] = "MISSING_IDEMPOTENCY_KEY";

                context.Result = new BadRequestObjectResult(problemDetails);
                return;
            }

            await next();
            return;
        }

        var idempotencyKey = rawKey.ToString().Trim();
        var callerId = executionContext.CallerApp ?? executionContext.ProjectId?.ToString("N") ?? "anonymous";
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
                logger.LogWarning("Idempotency key {IdempotencyKey} is already in-flight for caller {CallerId}", idempotencyKey, callerId);

                var conflictDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Request In-Flight",
                    Detail = "A mutation request with this Idempotency-Key is currently being processed.",
                    Instance = context.HttpContext.Request.Path,
                    Type = "https://api.sammakorn.co.th/errors/idempotency-in-flight"
                };
                conflictDetails.Extensions["errorCode"] = "IDEMPOTENCY_IN_FLIGHT";

                context.Result = new ConflictObjectResult(conflictDetails);
                return;
            }

            // Scenario B: Completed cached execution
            if (existing.Status == IdempotencyStatus.Completed)
            {
                // Verify request payload fingerprint
                if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    logger.LogWarning("Idempotency key {IdempotencyKey} payload mismatch for caller {CallerId}", idempotencyKey, callerId);

                    var mismatchDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Title = "Idempotency-Key Payload Mismatch",
                        Detail = "This Idempotency-Key was previously used with a different request payload.",
                        Instance = context.HttpContext.Request.Path,
                        Type = "https://api.sammakorn.co.th/errors/idempotency-payload-mismatch"
                    };
                    mismatchDetails.Extensions["errorCode"] = "IDEMPOTENCY_PAYLOAD_MISMATCH";

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
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Operation failed for Idempotency-Key {IdempotencyKey}. Rolling back in-flight lock.", idempotencyKey);
            await idempotencyStore.RemoveAsync(cacheKey, context.HttpContext.RequestAborted);
            throw;
        }

        if (executedContext.Exception is not null)
        {
            logger.LogWarning("Operation resulted in unhandled exception for Idempotency-Key {IdempotencyKey}. Rolling back in-flight lock.", idempotencyKey);
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
