using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Services;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

public sealed class ListFontsUseCase(IFontCatalogService fontCatalogService) : IUseCase<ListFontsQuery, List<string>>
{
    public const string BuiltInDefaultFont = "Sarabun-Regular.ttf";

    public async Task<List<string>> ExecuteAsync(ListFontsQuery query, CancellationToken ct = default)
    {
        var catalog = await fontCatalogService.GetCatalogAsync(ct);
        return catalog.AllFontNames.ToList();
    }
}
