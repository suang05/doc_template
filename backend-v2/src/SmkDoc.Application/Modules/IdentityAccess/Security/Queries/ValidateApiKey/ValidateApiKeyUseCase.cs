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
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ValidatedApiKeyDto?> ExecuteAsync(ValidateApiKeyQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.PlainTextKey)) return null;

        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(query.PlainTextKey));
        var key = await _apiKeyRepo.GetByKeyHashAsync(hash, ct);
        var now = _timeProvider.GetUtcNow();
        if (key != null && key.IsUsableAt(now))
        {
            key.RecordUsage(now);
            _apiKeyRepo.Update(key);
            await _unitOfWork.CommitAsync(ct);
            return new ValidatedApiKeyDto(key.Id, key.CallerApp, key.ProjectId);
        }
        return null;
    }
}
