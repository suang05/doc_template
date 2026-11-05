using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Services;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;

public sealed class GetFontCatalogUseCase(
    IFontCatalogService fontCatalogService) : IUseCase<GetFontCatalogQuery, FontCatalogDto>
{
    public Task<FontCatalogDto> ExecuteAsync(GetFontCatalogQuery query, CancellationToken ct = default)
    {
        return fontCatalogService.GetCatalogAsync(ct);
    }
}

