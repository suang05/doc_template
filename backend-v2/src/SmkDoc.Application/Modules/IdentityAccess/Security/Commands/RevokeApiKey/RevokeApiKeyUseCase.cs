using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider? timeProvider = null) : IUseCase<RevokeApiKeyCommand>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task ExecuteAsync(RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to revoke an API key.");
        }

        var key = await apiKeyRepo.GetByIdAsync(new ApiKeyId(command.Id), projectContext.ProjectId, ct)
            ?? throw new NotFoundException($"API Key '{command.Id}' not found.");

        key.Revoke(this.timeProvider.GetUtcNow());
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);
    }
}
