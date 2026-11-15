using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCase(
    IApiKeyRepository apiKeyRepo) : IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>>
{
    public async Task<List<ApiKeyResultDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct)
    {
        var keys = query.ProjectId.HasValue && query.ProjectId.Value != Guid.Empty
            ? await apiKeyRepo.ListByProjectAsync(new ProjectId(query.ProjectId.Value), ct)
            : await apiKeyRepo.ListCurrentProjectAsync(ct);

        return keys.Select(ApiKeyResultDto.FromEntity).ToList();
    }
}
