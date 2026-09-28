using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;

public sealed record ListTemplateVersionsQuery(Guid TemplateId);

/// <summary>
/// Single-responsibility Query Use Case for listing versions of a template.
/// Injects only IRepository<TemplateVersion> (Zero unused dependencies).
/// </summary>
public sealed class ListTemplateVersionsUseCase(
    IRepository<TemplateVersion> versionRepo) : IUseCase<ListTemplateVersionsQuery, List<TemplateVersionDto>>
{
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;

    public async Task<List<TemplateVersionDto>> ExecuteAsync(ListTemplateVersionsQuery query, CancellationToken ct = default)
    {
        var versions = await _versionRepo.ListAsync(v => v.TemplateId == query.TemplateId, ct);

        return versions
            .Select(v => new TemplateVersionDto(
                v.Id,
                v.TemplateId,
                v.Version,
                v.StorageKey,
                v.Status.Name,
                v.FileFormat?.Name,
                v.DataSchema,
                v.SamplePayload,
                v.MappingsSnapshot,
                v.CommitMessage,
                v.CreatedBy,
                v.CreatedAt,
                v.UpdatedAt))
            .OrderByDescending(v => v.Version)
            .ToList();
    }
}
