namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

public sealed class ListFontsUseCase(IFontCatalogPort fontCatalogPort) : IUseCase<ListFontsQuery, List<string>>
{
    public const string BuiltInDefaultFont = "Sarabun-Regular.ttf";

    public async Task<List<string>> ExecuteAsync(ListFontsQuery query, CancellationToken ct)
    {
        var catalog = await fontCatalogPort.GetCatalogAsync(ct);
        return catalog.AllFontNames.ToList();
    }
}
