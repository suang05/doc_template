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
        IReadOnlyList<Domain.Entities.ApiKey> keys;

        if (query.ProjectId.HasValue && query.ProjectId.Value != Guid.Empty)
        {
            var targetProjectId = new ProjectId(query.ProjectId.Value);
            if (projectContext.SystemRole != SystemRole.SuperAdmin)
            {
                var currentProjectId = projectContext.EnsureResolved("Active project context or SuperAdmin role is required to list API keys for a specific project.");
                if (currentProjectId != targetProjectId)
                {
                    throw new ForbiddenException("Access denied: caller does not have permission to list API keys for another project.");
                }
            }

            keys = await apiKeyRepo.ListByProjectAsync(targetProjectId, ct);
        }
        else if (projectContext.SystemRole == SystemRole.SuperAdmin && !projectContext.IsResolved)
        {
            keys = await apiKeyRepo.ListAllAsync(ct);
        }
        else
        {
            var currentProjectId = projectContext.EnsureResolved("Active project context is required to list API keys.");
            keys = await apiKeyRepo.ListByProjectAsync(currentProjectId, ct);
        }

        return keys
            .Select(k =>
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
            })
            .ToList();
    }
}
