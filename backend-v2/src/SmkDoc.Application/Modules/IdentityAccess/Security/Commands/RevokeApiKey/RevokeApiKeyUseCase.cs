using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork) : IUseCase<RevokeApiKeyCommand>
{
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task ExecuteAsync(RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        var key = await _apiKeyRepo.GetByIdAsync(command.Id, ct)
            ?? throw new NotFoundException($"API Key '{command.Id}' not found.");

        key.Revoke();
        _apiKeyRepo.Update(key);
        await _unitOfWork.CommitAsync(ct);
    }
}
