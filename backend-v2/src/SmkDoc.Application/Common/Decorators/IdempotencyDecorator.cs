using System.Security.Cryptography;
using System.Text.Json;

namespace SmkDoc.Application.Common.Decorators;

internal static class IdempotencyDecoratorSupport
{
    internal static readonly TimeSpan InFlightTtl = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan CompletedTtl = TimeSpan.FromHours(24);
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static (Guid? ProjectId, string CredentialId, string Scope, string Key, string Fingerprint) BuildKeyContext<TRequest>(
        TRequest request,
        IIdempotentCommand command,
        ICurrentProjectContext projectContext)
    {
        Guid? projectId = projectContext.TryGetProjectId()?.Value;
        string credentialId = string.IsNullOrWhiteSpace(projectContext.CallerIdentity)
            ? "system"
            : projectContext.CallerIdentity.Trim();
        string scope = string.IsNullOrWhiteSpace(command.IdempotencyScope)
            ? typeof(TRequest).Name
            : command.IdempotencyScope.Trim();
        string key = command.IdempotencyKey!.Trim();

        string fingerprint;
        if (!string.IsNullOrWhiteSpace(command.RequestFingerprint))
        {
            fingerprint = command.RequestFingerprint.Trim();
        }
        else
        {
            byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions);
            byte[] hash = SHA256.HashData(utf8Bytes);
            fingerprint = Convert.ToHexStringLower(hash);
        }

        return (projectId, credentialId, scope, key, fingerprint);
    }

    internal static void ValidateExistingRecord(IdempotencyRecord? existing, string fingerprint)
    {
        if (existing is null)
        {
            throw new ConflictException(
                "An idempotent operation with this key is already recorded.",
                ErrorCodes.IdempotencyInProgress);
        }

        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                "Idempotency-Key was previously used with a different request payload.",
                ErrorCodes.IdempotencyKeyReused);
        }

        if (existing.Status == IdempotencyStatus.InFlight)
        {
            throw new ConflictException(
                "A mutation request with this Idempotency-Key is currently in progress.",
                ErrorCodes.IdempotencyInProgress);
        }

        if (existing.Status != IdempotencyStatus.Completed)
        {
            throw new DomainValidationException(
                "Idempotency-Key cannot be replayed because the prior attempt failed.",
                ErrorCodes.IdempotencyKeyReused);
        }
    }
}

/// <summary>
/// Application-layer idempotency decorator positioned between <see cref="Behaviors.LoggingUseCaseDecorator{TRequest, TResponse}"/>
/// and <see cref="Behaviors.ValidatingUseCaseDecorator{TRequest, TResponse}"/>. Defers to HTTP
/// <see cref="IIdempotencyExecutionScope.IsFilterManaged"/> when already managed by the API filter.
/// </summary>
public sealed class IdempotencyDecorator<TRequest, TResponse>(
    IUseCase<TRequest, TResponse> inner,
    IIdempotencyStore idempotencyStore,
    IIdempotencyExecutionScope executionScope,
    ICurrentProjectContext projectContext) : IUseCase<TRequest, TResponse>
{
    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(projectContext);

        if (executionScope.IsFilterManaged ||
            request is not IIdempotentCommand command ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            return await inner.ExecuteAsync(request, ct).ConfigureAwait(false);
        }

        var (projectId, credentialId, scope, key, fingerprint) =
            IdempotencyDecoratorSupport.BuildKeyContext(request, command, projectContext);

        var acquisition = await idempotencyStore.TryAcquireOrGetAsync(
            new IdempotencyAcquisitionRequest(
                projectId,
                credentialId,
                scope,
                key,
                fingerprint,
                IdempotencyDecoratorSupport.InFlightTtl,
                IdempotencyDecoratorSupport.CompletedTtl),
            ct).ConfigureAwait(false);

        if (!acquisition.IsAcquired)
        {
            IdempotencyDecoratorSupport.ValidateExistingRecord(acquisition.ExistingRecord, fingerprint);

            if (string.IsNullOrWhiteSpace(acquisition.ExistingRecord!.ResponseJson))
            {
                throw new InvalidOperationException("Completed idempotency record is missing stored response JSON.");
            }

            var replayed = JsonSerializer.Deserialize<TResponse>(
                acquisition.ExistingRecord.ResponseJson,
                IdempotencyDecoratorSupport.SerializerOptions);

            return replayed
                ?? throw new InvalidOperationException("Failed to deserialize replayed idempotency response.");
        }

        bool completed = false;
        try
        {
            var response = await inner.ExecuteAsync(request, ct).ConfigureAwait(false);
            string responseJson = JsonSerializer.Serialize(response, IdempotencyDecoratorSupport.SerializerOptions);
            Guid? resourceId = response is IHasResourceId hasId ? hasId.ResourceId : null;

            await idempotencyStore.SaveCompletedAsync(
                new IdempotencyCompletionRequest(
                    projectId,
                    credentialId,
                    scope,
                    key,
                    fingerprint,
                    StatusCode: 200,
                    ResponseJson: responseJson,
                    Headers: null,
                    CompletedTtl: IdempotencyDecoratorSupport.CompletedTtl,
                    ResultResourceId: resourceId),
                ct).ConfigureAwait(false);

            completed = true;
            return response;
        }
        finally
        {
            if (!completed)
            {
                await idempotencyStore.RemoveAsync(
                    projectId,
                    credentialId,
                    scope,
                    key,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Void-response idempotency decorator overload for <see cref="IUseCase{TRequest}"/> commands
/// implementing <see cref="IIdempotentCommand"/>.
/// </summary>
public sealed class IdempotencyDecorator<TRequest>(
    IUseCase<TRequest> inner,
    IIdempotencyStore idempotencyStore,
    IIdempotencyExecutionScope executionScope,
    ICurrentProjectContext projectContext) : IUseCase<TRequest>
{
    public async Task ExecuteAsync(TRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(projectContext);

        if (executionScope.IsFilterManaged ||
            request is not IIdempotentCommand command ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            await inner.ExecuteAsync(request, ct).ConfigureAwait(false);
            return;
        }

        var (projectId, credentialId, scope, key, fingerprint) =
            IdempotencyDecoratorSupport.BuildKeyContext(request, command, projectContext);

        var acquisition = await idempotencyStore.TryAcquireOrGetAsync(
            new IdempotencyAcquisitionRequest(
                projectId,
                credentialId,
                scope,
                key,
                fingerprint,
                IdempotencyDecoratorSupport.InFlightTtl,
                IdempotencyDecoratorSupport.CompletedTtl),
            ct).ConfigureAwait(false);

        if (!acquisition.IsAcquired)
        {
            IdempotencyDecoratorSupport.ValidateExistingRecord(acquisition.ExistingRecord, fingerprint);
            return;
        }

        bool completed = false;
        try
        {
            await inner.ExecuteAsync(request, ct).ConfigureAwait(false);

            await idempotencyStore.SaveCompletedAsync(
                new IdempotencyCompletionRequest(
                    projectId,
                    credentialId,
                    scope,
                    key,
                    fingerprint,
                    StatusCode: 204,
                    ResponseJson: "{}",
                    Headers: null,
                    CompletedTtl: IdempotencyDecoratorSupport.CompletedTtl,
                    ResultResourceId: null),
                ct).ConfigureAwait(false);

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                await idempotencyStore.RemoveAsync(
                    projectId,
                    credentialId,
                    scope,
                    key,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
