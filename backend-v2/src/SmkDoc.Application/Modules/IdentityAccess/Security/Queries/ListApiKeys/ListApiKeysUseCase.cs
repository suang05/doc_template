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

        return keys.Select(ToMaskedDto).ToList();
    }

    private static ApiKeyResultDto ToMaskedDto(Domain.Entities.ApiKey k)
    {
        var suffix = k.KeyHash.Value.Length >= 4 ? k.KeyHash.Value[^4..] : "xxxx";
        var masked = $"smk_{k.CallerApp.ToLowerInvariant()}_****{suffix}";
        return new ApiKeyResultDto(
            k.Id.Value,
            k.Name.Value,
            k.CallerApp,
            k.IsActive,
            k.LastUsedAt,
            k.CreatedAt,
            k.Scope.Name,
            masked,
            ConcurrencyToken: k.GetConcurrencyToken());
    }
}
