using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;

public record GetFontBase64Query(string FontFileName);

public sealed class GetFontBase64UseCase(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IUseCase<GetFontBase64Query, string>
{
    private readonly IStorageService _storageService = storageService;
    private readonly ICurrentProjectContext _projectContext = projectContext;

    public async Task<string> ExecuteAsync(GetFontBase64Query query, CancellationToken ct = default)
    {
        if (!_projectContext.IsResolved)
        {
            throw new ForbiddenException("Reading custom font data requires an active project context.");
        }

        string safeFileName = UploadFontUseCase.SanitizeAndValidateFileName(query.FontFileName);
        string tenantKey = StorageBuckets.TenantFontKey(_projectContext.ProjectId, safeFileName);

        string? resolvedKey = null;
        if (await _storageService.ExistsAsync(StorageBuckets.Fonts, tenantKey, ct))
        {
            resolvedKey = tenantKey;
        }
        else if (string.Equals(safeFileName, ListFontsUseCase.BuiltInDefaultFont, StringComparison.OrdinalIgnoreCase) &&
                 await _storageService.ExistsAsync(StorageBuckets.Fonts, safeFileName, ct))
        {
            resolvedKey = safeFileName;
        }

        if (resolvedKey is null)
        {
            return string.Empty;
        }

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Fonts, resolvedKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return Convert.ToBase64String(ms.ToArray());
    }
}
