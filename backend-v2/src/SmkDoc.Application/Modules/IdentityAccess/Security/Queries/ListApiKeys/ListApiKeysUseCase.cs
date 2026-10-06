using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCase(
    IApiKeyRepository apiKeyRepo) : IUseCase<ListApiKeysQuery, List<ApiKeyDto>>
{
    public async Task<List<ApiKeyDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct = default)
    {
        if (query.ProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var keys = await apiKeyRepo.ListByProjectAsync(query.ProjectId, ct);
        return keys
            .Select(k => new ApiKeyDto(k.Id, k.Name.Value, k.CallerApp, k.IsActive, k.LastUsedAt, k.CreatedAt))
            .ToList();
    }
}
