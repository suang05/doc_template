using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<RevokeApiKeyCommand>
{
    public async Task ExecuteAsync(RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        var key = await ResolveTargetKeyAsync(new ApiKeyId(command.Id), ct);
        key.Revoke(timeProvider.GetUtcNow());
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task<ApiKey> ResolveTargetKeyAsync(ApiKeyId keyId, CancellationToken ct)
    {
        ApiKey? key;
        if (projectContext.SystemRole == SystemRole.SuperAdmin && !projectContext.IsResolved)
        {
            key = await apiKeyRepo.GetByIdWithoutProjectAsync(keyId, ct);
        }
        else
        {
            key = await apiKeyRepo.GetByIdAsync(keyId, projectContext.ProjectId, ct);
        }

        return key ?? throw new NotFoundException($"API Key '{keyId.Value}' not found.");
    }
}
