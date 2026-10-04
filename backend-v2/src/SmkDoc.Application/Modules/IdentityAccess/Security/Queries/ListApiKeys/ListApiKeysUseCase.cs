using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCase(
    IApiKeyRepository apiKeyRepo) : IUseCase<ListApiKeysQuery, List<ApiKeyDto>>
{
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;

    public async Task<List<ApiKeyDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct = default)
    {
        if (query.ProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var keys = await _apiKeyRepo.ListByProjectAsync(query.ProjectId, ct);
        return keys
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.CallerApp, k.IsActive, k.LastUsedAt, k.CreatedAt))
            .ToList();
    }
}
