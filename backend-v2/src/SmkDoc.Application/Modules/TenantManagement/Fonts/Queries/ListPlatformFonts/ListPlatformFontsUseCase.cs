using SmkDoc.Application.Modules.TenantManagement.Fonts.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.TenantManagement.Fonts.Queries.ListPlatformFonts;

public sealed class ListPlatformFontsUseCase(
    IFontRepository fontRepository) : IUseCase<ListPlatformFontsQuery, IReadOnlyList<PlatformFontResultDto>>
{
    public async Task<IReadOnlyList<PlatformFontResultDto>> ExecuteAsync(ListPlatformFontsQuery query, CancellationToken ct)
    {
        ProjectId? projectId = query.ProjectId.HasValue && query.ProjectId.Value != Guid.Empty
            ? new ProjectId(query.ProjectId.Value)
            : null;

        var fonts = await fontRepository.ListAsync(projectId, ct);

        return fonts.Select(f => new PlatformFontResultDto(
            f.Id.Value,
            f.FamilyName,
            f.Weight,
            f.Style,
            f.Format,
            f.StorageKey,
            f.ChecksumSha256.Value,
            f.IsSystemFont,
            f.ProjectId?.Value,
            f.CreatedAt,
            f.Version)).ToList();
    }
}
