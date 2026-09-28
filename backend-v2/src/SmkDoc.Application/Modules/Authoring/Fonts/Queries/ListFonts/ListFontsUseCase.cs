using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

public record ListFontsQuery;

public sealed class ListFontsUseCase(IStorageService storageService) : IUseCase<ListFontsQuery, List<string>>
{
    private readonly IStorageService _storageService = storageService;

    public Task<List<string>> ExecuteAsync(ListFontsQuery query, CancellationToken ct = default)
    {
        return Task.FromResult(new List<string> { "Sarabun-Regular.ttf" });
    }
}
