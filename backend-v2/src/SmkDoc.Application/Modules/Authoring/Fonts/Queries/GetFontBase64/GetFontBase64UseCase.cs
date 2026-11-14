namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;

public sealed class GetFontBase64UseCase(
    IFontCatalogPort fontCatalogPort) : IUseCase<GetFontBase64Query, string>
{
    public Task<string> ExecuteAsync(GetFontBase64Query query, CancellationToken ct) =>
        fontCatalogPort.GetFontBase64Async(query.FontFileName, ct);
}
