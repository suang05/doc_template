using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

public sealed class ValidateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork) : IUseCase<ValidateApiKeyQuery, ValidatedApiKeyDto?>
{
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ValidatedApiKeyDto?> ExecuteAsync(ValidateApiKeyQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.PlainTextKey)) return null;

        string hash = ApiKeyHelper.ComputeHash(query.PlainTextKey);
        var key = await _apiKeyRepo.GetByKeyHashAsync(hash, ct);
        if (key != null)
        {
            key.RecordUsage();
            _apiKeyRepo.Update(key);
            await _unitOfWork.CommitAsync(ct);
            return new ValidatedApiKeyDto(key.Id, key.CallerApp, key.ProjectId);
        }
        return null;
    }
}
