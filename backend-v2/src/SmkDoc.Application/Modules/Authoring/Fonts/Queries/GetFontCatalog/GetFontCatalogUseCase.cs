using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;

public sealed class GetFontCatalogUseCase(
    IFontCatalogPort fontCatalogPort) : IUseCase<GetFontCatalogQuery, FontCatalogDto>
{
    public Task<FontCatalogDto> ExecuteAsync(GetFontCatalogQuery query, CancellationToken ct) =>
        fontCatalogPort.GetCatalogAsync(ct);
}

