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
        ApiKey? key;
        var keyId = new ApiKeyId(command.Id);

        if (projectContext.IsResolved)
        {
            key = await apiKeyRepo.GetByIdAsync(keyId, projectContext.ProjectId, ct);
        }
        else if (projectContext.SystemRole == SystemRole.SuperAdmin)
        {
            key = await apiKeyRepo.GetByIdWithoutProjectAsync(keyId, ct);
        }
        else
        {
            throw new ForbiddenException("Active project context is required to revoke an API key.");
        }

        if (key == null)
        {
            throw new NotFoundException($"API Key '{command.Id}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(command.IfMatch) && command.IfMatch != "*")
        {
            var currentToken = (key.UpdatedAt ?? key.CreatedAt).ToUnixTimeMilliseconds().ToString();
            var sanitizedMatch = command.IfMatch.Trim('\"', 'W', '/', ' ');
            if (!string.Equals(sanitizedMatch, currentToken, StringComparison.OrdinalIgnoreCase))
            {
                throw new PreconditionFailedException(
                    $"Precondition failed for API key '{command.Id}'. ETag '{sanitizedMatch}' does not match current state.");
            }
        }

        key.Revoke(timeProvider.GetUtcNow());
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);
    }
}
