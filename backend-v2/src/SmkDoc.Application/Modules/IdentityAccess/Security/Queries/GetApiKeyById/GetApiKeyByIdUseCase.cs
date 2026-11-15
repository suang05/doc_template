using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetApiKeyById;

public sealed class GetApiKeyByIdUseCase(
    IApiKeyRepository apiKeyRepo) : IUseCase<GetApiKeyByIdQuery, ApiKeyResultDto>
{
    public async Task<ApiKeyResultDto> ExecuteAsync(GetApiKeyByIdQuery query, CancellationToken ct)
    {
        var keyId = new ApiKeyId(query.KeyId);
        var key = query.ProjectId is { } explicitProjectId
            ? await apiKeyRepo.GetByIdAsync(keyId, new ProjectId(explicitProjectId), ct)
            : await apiKeyRepo.GetForCurrentProjectAsync(keyId, ct);

        if (key is null)
        {
            throw new NotFoundException("ApiKey", query.KeyId);
        }

        return ApiKeyResultDto.FromEntity(key);
    }
}
