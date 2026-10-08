using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

public sealed class ValidateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<ValidateApiKeyQuery, ValidatedApiKeyDto?>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ValidatedApiKeyDto?> ExecuteAsync(ValidateApiKeyQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.PlainTextKey)) return null;

        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(query.PlainTextKey));
        var key = await apiKeyRepo.GetByKeyHashAsync(hash, ct);
        var now = _timeProvider.GetUtcNow();
        if (key != null && key.IsUsableAt(now))
        {
            key.RecordUsage(now);
            apiKeyRepo.Update(key);
            await unitOfWork.CommitAsync(ct);
            return new ValidatedApiKeyDto(key.Id, key.CallerApp, key.ProjectId, key.Scope.Name);
        }
        return null;
    }
}
