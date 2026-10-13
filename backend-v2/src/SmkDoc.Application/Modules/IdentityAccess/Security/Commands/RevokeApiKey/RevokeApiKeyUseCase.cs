using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<RevokeApiKeyCommand>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task ExecuteAsync(RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var key = await apiKeyRepo.GetByIdAsync(command.Id, command.ProjectId, ct)
            ?? throw new NotFoundException($"API Key '{command.Id}' not found.");

        key.Revoke(this.timeProvider.GetUtcNow());
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);
    }
}
