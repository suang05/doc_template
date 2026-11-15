using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class FontReadModel(AppDbContext context) : IFontReadModel
{
    public async Task<IReadOnlyList<FontDescriptorDto>> ListRegisteredFontsAsync(ProjectId? projectId, CancellationToken ct)
    {
        var query = context.Fonts.AsNoTracking();

        query = projectId.HasValue
            ? query.Where(f => f.IsSystemFont || f.ProjectId == null || f.ProjectId == projectId.Value)
            : query.Where(f => f.IsSystemFont || f.ProjectId == null);

        var fonts = await query
            .OrderBy(f => f.FamilyName)
            .ThenBy(f => f.Weight)
            .ThenBy(f => f.Style)
            .ToListAsync(ct);

        return fonts
            .Select(f => new FontDescriptorDto(
                FontFamily: f.FamilyName,
                DisplayName: f.IsSystemFont ? $"{f.FamilyName} (Platform)" : $"{f.FamilyName} (Custom)",
                Source: f.IsSystemFont ? FontSource.System : FontSource.MinIO,
                FileName: Path.GetFileName(f.StorageKey),
                AvailableWeights: [f.Weight]))
            .ToList();
    }
}
