using System.Diagnostics.CodeAnalysis;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider,
    IQuotaReader? quotaReader = null,
    IApiKeyCredentialGenerator? credentialGenerator = null) : IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>
{
    private const int DefaultMaxActiveApiKeys = 2;

    public async Task<CreateApiKeyResultDto> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct)
    {
        var pId = projectContext.GetRequiredProjectId();
        await EnsureProjectExistsAsync(pId, ct);

        var now = timeProvider.GetUtcNow();
        var targetScope = ApiKeyScope.FromName(command.Scope);
        await EnsureWithinQuotaAndUniqueScopeAsync(pId, targetScope, command.Scope, now, ct);

        var (key, rawSecret) = IssueKey(pId, command, targetScope, now, credentialGenerator);
        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResultDto(key.Id.Value, key.Name.Value, key.CallerApp, rawSecret, key.Scope.Name, key.ExpiresAt, key.Version);
    }

    private async Task EnsureProjectExistsAsync(ProjectId pId, CancellationToken ct)
    {
        _ = await projectRepo.GetByIdAsync(pId, ct)
            ?? throw new NotFoundException($"Project '{pId.Value}' not found.");
    }

    private async Task EnsureWithinQuotaAndUniqueScopeAsync(
        ProjectId pId,
        ApiKeyScope targetScope,
        string requestedScopeName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existingKeys = await apiKeyRepo.ListByProjectAsync(pId, ct);
        var activeKeys = existingKeys.Where(k => k.IsActive && !k.IsExpiredAt(now)).ToList();

        var maxActiveKeys = quotaReader is not null
            ? (await quotaReader.GetQuotaForProjectAsync(pId, ct)).MaxActiveApiKeys
            : DefaultMaxActiveApiKeys;

        if (activeKeys.Count >= maxActiveKeys)
        {
            throw new BusinessRuleViolationException(
                $"Project has reached the maximum quota of {maxActiveKeys} active API keys.",
                ErrorCodes.QuotaExceeded);
        }

        if (activeKeys.Any(k => k.Scope == targetScope))
        {
            throw new ConflictException($"Project already possesses an active {requestedScopeName} API key. Revoke the existing key before issuing a new one.");
        }
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API key prefix uses canonical lowercase caller app identifier.")]
    private static (ApiKey Key, string RawSecret) IssueKey(
        ProjectId pId,
        CreateApiKeyCommand command,
        ApiKeyScope targetScope,
        DateTimeOffset now,
        IApiKeyCredentialGenerator? credentialGenerator)
    {
        if (credentialGenerator is not null)
        {
            var minted = credentialGenerator.MintLiveKey();
            var structuredKey = ApiKey.IssueStructured(
                pId,
                ApiKeyName.Create(command.Name),
                command.CallerApp,
                minted.KeyPrefix,
                minted.KeyId,
                minted.KeySuffix,
                minted.SecretHash.ToArray(),
                ExpirationPolicy.Never,
                now,
                targetScope);
            return (structuredKey, minted.PlainTextKey);
        }

        var rawSecret = ApiKeyGenerator.GenerateSecret($"smk_{command.CallerApp.ToLowerInvariant()}");
        var hash = new Sha256Hash(ApiKeyGenerator.ComputeHash(rawSecret));

        var key = ApiKey.Issue(
            pId,
            ApiKeyName.Create(command.Name),
            command.CallerApp,
            hash,
            ExpirationPolicy.Never,
            now: now,
            scope: targetScope);

        return (key, rawSecret);
    }
}
