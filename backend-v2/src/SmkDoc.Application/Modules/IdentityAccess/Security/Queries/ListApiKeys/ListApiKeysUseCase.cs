using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCase(
    IApiKeyRepository apiKeyRepo,
    ICurrentProjectContext projectContext) : IUseCase<ListApiKeysQuery, List<ApiKeyDto>>
{
    public async Task<List<ApiKeyDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to list API keys.");
        }

        var keys = await apiKeyRepo.ListByProjectAsync(projectContext.ProjectId, ct);
        return keys
            .Select(k => new ApiKeyDto(k.Id.Value, k.Name.Value, k.CallerApp, k.IsActive, k.LastUsedAt, k.CreatedAt))
            .ToList();
    }
}
