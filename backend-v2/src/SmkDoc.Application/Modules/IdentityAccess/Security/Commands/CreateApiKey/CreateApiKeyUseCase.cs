using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>
{
    public async Task<CreateApiKeyResultDto> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct = default)
    {
        var pId = projectContext.ProjectId;
        await EnsureProjectExistsAsync(pId, ct);

        var now = timeProvider.GetUtcNow();
        var targetScope = ApiKeyScope.FromName(command.Scope);
        await EnsureWithinQuotaAndUniqueScopeAsync(pId, targetScope, command.Scope, now, ct);

        var (key, rawSecret) = IssueKey(pId, command, targetScope, now);
        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResultDto(key.Id.Value, key.Name.Value, key.CallerApp, rawSecret, key.Scope.Name, key.ExpiresAt);
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

        if (activeKeys.Count >= 2)
        {
            throw new ConflictException("Project has reached the maximum quota of 2 active API keys.");
        }

        if (activeKeys.Any(k => k.Scope == targetScope))
        {
            throw new ConflictException($"Project already possesses an active {requestedScopeName} API key. Revoke the existing key before issuing a new one.");
        }
    }

    private static (ApiKey Key, string RawSecret) IssueKey(
        ProjectId pId,
        CreateApiKeyCommand command,
        ApiKeyScope targetScope,
        DateTimeOffset now)
    {
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Migrate API key suffix token generation to cryptographic RNG / Guid.CreateVersion7()
        var rawSecret = $"smk_{command.CallerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
#pragma warning restore RS0030
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
