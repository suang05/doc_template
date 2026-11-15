using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Action filter enforcing the IETF Idempotency-Key specification on state-mutating HTTP endpoints.
/// Uses durable tenant-scoped acquisition, coordinates with <see cref="IIdempotencyExecutionScope"/>,
/// emits <c>Retry-After</c> on concurrent in-flight conflicts, and rolls back in-flight locks on failure.
/// </summary>
public sealed partial class IdempotencyFilter(
    IIdempotencyStore idempotencyStore,
    IIdempotencyExecutionScope executionScope,
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

    [ActivatorUtilitiesConstructor]
    public IdempotencyFilter(
        IIdempotencyStore idempotencyStore,
        IIdempotencyExecutionScope executionScope,
        ICurrentProjectContext projectContext,
        ILogger<IdempotencyFilter> logger,
        IOptions<JsonOptions> jsonOptions)
        : this(
            idempotencyStore,
            executionScope,
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

        string idempotencyKey = rawKey.ToString().Trim();
        Guid? projectId = projectContext.TryGetProjectId()?.Value;
        string credentialId = ResolveCredentialId();
        string scope = ResolveScope(context);
        string fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, jsonSerializerOptions);

        var acquisitionResult = await idempotencyStore.TryAcquireOrGetAsync(
            new IdempotencyAcquisitionRequest(
                projectId,
                credentialId,
                scope,
                idempotencyKey,
                fingerprint,
                InFlightTtl,
                completedTtl),
            context.HttpContext.RequestAborted);

        if (!acquisitionResult.IsAcquired)
        {
            HandleConflictOrReplay(context, acquisitionResult.ExistingRecord, idempotencyKey, credentialId, fingerprint);
            return;
        }

        executionScope.MarkFilterManaged();

        var executedContext = await ExecuteWithRollbackAsync(
            next,
            projectId,
            credentialId,
            scope,
            idempotencyKey);

        if (executedContext is not null)
        {
            await PersistOrEvictResponseAsync(
                context,
                executedContext,
                projectId,
                credentialId,
                scope,
                idempotencyKey,
                fingerprint);
        }
    }

    private string ResolveCredentialId() =>
        projectContext.ApiKeyId?.Value.ToString("N")
        ?? projectContext.UserId?.Value.ToString("N")
        ?? (string.IsNullOrWhiteSpace(projectContext.CallerIdentity) ? "anonymous" : projectContext.CallerIdentity.Trim());

    private static string ResolveScope(ActionExecutingContext context)
    {
        string method = context.HttpContext.Request.Method.ToUpperInvariant();
        string path = context.HttpContext.Request.Path.Value ?? string.Empty;
        return $"{method}:{path}";
    }

    private void HandleConflictOrReplay(
        ActionExecutingContext context,
        IdempotencyRecord? existing,
        string idempotencyKey,
        string credentialId,
        string fingerprint)
    {
        if (existing is null || existing.Status == IdempotencyStatus.InFlight)
        {
            LogKeyInFlight(logger, idempotencyKey, credentialId);
            context.HttpContext.Response.Headers.RetryAfter = "2";
            context.Result = new ConflictObjectResult(ProblemDetailsBuilder.Create(
                status: StatusCodes.Status409Conflict,
                title: "Request In-Flight",
                detail: "A mutation request with this Idempotency-Key is currently being processed.",
                errorCode: Domain.Exceptions.ErrorCodes.IdempotencyInProgress,
                instance: context.HttpContext.Request.Path,
                traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext)));
            return;
        }

        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            LogKeyPayloadMismatch(logger, idempotencyKey, credentialId);
            var mismatchDetails = ProblemDetailsBuilder.Create(
                status: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency-Key Payload Mismatch",
                detail: "This Idempotency-Key was previously used with a different request payload.",
                errorCode: Domain.Exceptions.ErrorCodes.IdempotencyKeyReused,
                instance: context.HttpContext.Request.Path,
                traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext));

            context.Result = new ObjectResult(mismatchDetails)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
            return;
        }

        if (existing.Status != IdempotencyStatus.Completed)
        {
            context.HttpContext.Response.Headers.RetryAfter = "2";
            context.Result = new ConflictObjectResult(ProblemDetailsBuilder.Create(
                status: StatusCodes.Status409Conflict,
                title: "Idempotency Conflict",
                detail: "This Idempotency-Key is associated with an incomplete prior request.",
                errorCode: Domain.Exceptions.ErrorCodes.IdempotencyInProgress,
                instance: context.HttpContext.Request.Path,
                traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext)));
            return;
        }

        ReplayCachedResponse(context, existing);
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
        ActionExecutionDelegate next,
        Guid? projectId,
        string credentialId,
        string scope,
        string idempotencyKey)
    {
        ActionExecutedContext executedContext;
        try
        {
            executedContext = await next();
        }
        catch (Exception)
        {
            await idempotencyStore.RemoveAsync(projectId, credentialId, scope, idempotencyKey, CancellationToken.None);
            throw;
        }

        if (executedContext.Exception is not null)
        {
            LogUnhandledRollback(logger, idempotencyKey);
            await idempotencyStore.RemoveAsync(projectId, credentialId, scope, idempotencyKey, CancellationToken.None);
            return null;
        }

        return executedContext;
    }

    private async Task PersistOrEvictResponseAsync(
        ActionExecutingContext context,
        ActionExecutedContext executedContext,
        Guid? projectId,
        string credentialId,
        string scope,
        string idempotencyKey,
        string fingerprint)
    {
        if (executedContext.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            await SaveOrEvictSuccessStatusAsync(
                context.HttpContext,
                projectId,
                credentialId,
                scope,
                idempotencyKey,
                fingerprint,
                statusCode,
                () => (
                    JsonSerializer.Serialize(objectResult.Value, jsonSerializerOptions),
                    ExtractReplayHeaders(context.HttpContext.Response.Headers),
                    ExtractResourceId(objectResult.Value)));
            return;
        }

        if (executedContext.Result is StatusCodeResult statusCodeResult)
        {
            await SaveOrEvictSuccessStatusAsync(
                context.HttpContext,
                projectId,
                credentialId,
                scope,
                idempotencyKey,
                fingerprint,
                statusCodeResult.StatusCode,
                static () => (string.Empty, null, null));
            return;
        }

        await idempotencyStore.RemoveAsync(projectId, credentialId, scope, idempotencyKey, CancellationToken.None);
    }

    private async Task SaveOrEvictSuccessStatusAsync(
        HttpContext httpContext,
        Guid? projectId,
        string credentialId,
        string scope,
        string idempotencyKey,
        string fingerprint,
        int statusCode,
        Func<(string Body, Dictionary<string, string>? Headers, Guid? ResourceId)> payloadFactory)
    {
        if (statusCode is >= 200 and < 300)
        {
            var (body, headers, resourceId) = payloadFactory();
            await idempotencyStore.SaveCompletedAsync(
                new IdempotencyCompletionRequest(
                    projectId,
                    credentialId,
                    scope,
                    idempotencyKey,
                    fingerprint,
                    statusCode,
                    body,
                    headers,
                    completedTtl,
                    resourceId),
                httpContext.RequestAborted);
            return;
        }

        await idempotencyStore.RemoveAsync(projectId, credentialId, scope, idempotencyKey, CancellationToken.None);
    }

    private static Guid? ExtractResourceId(object? value)
    {
        var payload = value is IApiResponse envelope ? envelope.Data : value;
        return payload is IHasResourceId resource ? resource.ResourceId : null;
    }

    private static Dictionary<string, string>? ExtractReplayHeaders(IHeaderDictionary responseHeaders)
    {
        Dictionary<string, string>? headers = null;

        if (responseHeaders.TryGetValue("Location", out var locationValue))
        {
            headers = new Dictionary<string, string>
            {
                ["Location"] = locationValue.ToString()
            };
        }

        if (responseHeaders.TryGetValue(OpenApiNames.ETagHeader, out var etagValue))
        {
            headers ??= [];
            headers[OpenApiNames.ETagHeader] = etagValue.ToString();
        }

        return headers;
    }
}
