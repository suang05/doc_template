using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;

public sealed class GetFontBase64UseCase(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IUseCase<GetFontBase64Query, string>
{
    public async Task<string> ExecuteAsync(GetFontBase64Query query, CancellationToken ct = default)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.ProjectId;

        string safeFileName = UploadFontUseCase.SanitizeAndValidateFileName(query.FontFileName);
        string tenantKey = StorageBuckets.TenantFontKey(projectId, safeFileName);

        string? resolvedKey = null;
        if (await storageService.ExistsAsync(StorageBuckets.Fonts, tenantKey, ct))
        {
            resolvedKey = tenantKey;
        }
        else if (string.Equals(safeFileName, ListFontsUseCase.BuiltInDefaultFont, StringComparison.OrdinalIgnoreCase) &&
                 await storageService.ExistsAsync(StorageBuckets.Fonts, safeFileName, ct))
        {
            resolvedKey = safeFileName;
        }

        if (resolvedKey is null)
        {
            return string.Empty;
        }

        using var stream = await storageService.DownloadAsync(StorageBuckets.Fonts, resolvedKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Replace MemoryStream.ToArray() with pooled HybridSpoolStream/RecyclableMemoryStream
        return Convert.ToBase64String(ms.ToArray());
#pragma warning restore RS0030
    }
}
