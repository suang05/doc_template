using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

public record ListFontsQuery;

public sealed class ListFontsUseCase(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IUseCase<ListFontsQuery, List<string>>
{
    public const string BuiltInDefaultFont = "Sarabun-Regular.ttf";

    private readonly IStorageService _storageService = storageService;
    private readonly ICurrentProjectContext _projectContext = projectContext;

    public async Task<List<string>> ExecuteAsync(ListFontsQuery query, CancellationToken ct = default)
    {
        if (!_projectContext.IsResolved)
        {
            throw new ForbiddenException("Listing custom fonts requires an active project context.");
        }

        string tenantPrefix = $"fonts/{_projectContext.ProjectId.Value:N}/";
        var objectKeys = await _storageService.ListObjectsAsync(StorageBuckets.Fonts, tenantPrefix, ct) ?? [];

        var fontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BuiltInDefaultFont
        };

        foreach (var key in objectKeys)
        {
            var fileName = Path.GetFileName(key);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fontNames.Add(fileName);
            }
        }

        return fontNames
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
