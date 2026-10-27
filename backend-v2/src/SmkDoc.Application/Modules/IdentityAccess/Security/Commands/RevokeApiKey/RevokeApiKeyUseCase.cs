using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<RevokeApiKeyCommand>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ICurrentProjectContext? projectContext = projectContext;

    public async Task ExecuteAsync(RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        if (projectContext?.IsResolved == true && command.ProjectId != projectContext.ProjectId.Value)
        {
            throw new ForbiddenException($"Cross-tenant API key revocation rejected: Command ProjectId '{command.ProjectId}' does not match active context '{projectContext.ProjectId.Value}'.");
        }

        var key = await apiKeyRepo.GetByIdAsync(new ApiKeyId(command.Id), new ProjectId(command.ProjectId), ct)
            ?? throw new NotFoundException($"API Key '{command.Id}' not found.");

        key.Revoke(this.timeProvider.GetUtcNow());
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);
    }
}
