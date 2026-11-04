using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

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
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to create an API key.");
        }

        var pId = projectContext.ProjectId;
        _ = await projectRepo.GetByIdAsync(pId, ct)
            ?? throw new NotFoundException($"Project '{pId.Value}' not found.");

        var now = timeProvider.GetUtcNow();
        var targetScope = ApiKeyScope.FromName(command.Scope);

        // Strict 2-Key Quota & Unique Scope Enforcement
        var existingKeys = await apiKeyRepo.ListByProjectAsync(pId, ct);
        var activeKeys = existingKeys.Where(k => k.IsActive && !k.IsExpiredAt(now)).ToList();

        if (activeKeys.Count >= 2)
        {
            throw new ConflictException("Project has reached the maximum quota of 2 active API keys.");
        }

        if (activeKeys.Any(k => k.Scope == targetScope))
        {
            throw new ConflictException($"Project already possesses an active {command.Scope} API key. Revoke the existing key before issuing a new one.");
        }

        string rawSecret = $"smk_{command.CallerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawSecret));

        var key = ApiKey.Issue(
            pId,
            ApiKeyName.Create(command.Name),
            command.CallerApp,
            hash,
            ExpirationPolicy.Never,
            now: now,
            scope: targetScope);

        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResultDto(key.Id.Value, key.Name.Value, key.CallerApp, rawSecret, key.Scope.Name, key.ExpiresAt);
    }
}
