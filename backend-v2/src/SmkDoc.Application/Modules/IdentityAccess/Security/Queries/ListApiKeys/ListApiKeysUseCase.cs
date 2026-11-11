using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCase(
    IApiKeyRepository apiKeyRepo,
    ICurrentProjectContext projectContext) : IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>>
{
    public async Task<List<ApiKeyResultDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct = default)
    {
        var keys = await ResolveTargetKeysAsync(query, ct);
        return keys.Select(ToMaskedDto).ToList();
    }

    private async Task<IReadOnlyList<Domain.Entities.ApiKey>> ResolveTargetKeysAsync(ListApiKeysQuery query, CancellationToken ct)
    {
        if (query.ProjectId.HasValue && query.ProjectId.Value != Guid.Empty)
        {
            var targetProjectId = new ProjectId(query.ProjectId.Value);
            EnsureListPermission(targetProjectId);
            return await apiKeyRepo.ListByProjectAsync(targetProjectId, ct);
        }

        if (projectContext.SystemRole == SystemRole.SuperAdmin && !projectContext.IsResolved)
        {
            return await apiKeyRepo.ListAllAsync(ct);
        }

        var currentProjectId = projectContext.EnsureResolved("Active project context is required to list API keys.");
        return await apiKeyRepo.ListByProjectAsync(currentProjectId, ct);
    }

    private void EnsureListPermission(ProjectId targetProjectId)
    {
        if (projectContext.SystemRole == SystemRole.SuperAdmin) return;

        var currentProjectId = projectContext.EnsureResolved("Active project context or SuperAdmin role is required to list API keys for a specific project.");
        if (currentProjectId != targetProjectId)
        {
            throw new ForbiddenException("Access denied: caller does not have permission to list API keys for another project.");
        }
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
