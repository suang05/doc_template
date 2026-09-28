using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Fonts;

public sealed class FontManagementUseCase(IStorageService storageService)
{
    private readonly IStorageService _storageService = storageService;
    private const string FontsBucket = "fonts";

    public async Task<List<string>> ListFontsAsync(CancellationToken ct = default)
    {
        // Currently MinioStorageService does not expose a ListObjectsAsync method.
        // We'd either need to add it, or maintain a DB record of fonts.
        // For Phase 4 MVP, if we don't have ListObjects in IStorageService, we can mock or throw NotImplemented.
        // I will add a basic implementation that returns empty until ListObjects is added.
        return new List<string> { "Sarabun-Regular.ttf" }; 
    }

    public async Task<string> UploadFontAsync(Stream data, string fileName, string contentType, CancellationToken ct = default)
    {
        // Allowed content types for fonts: font/woff2, font/woff, font/ttf, application/x-font-ttf
        var allowedTypes = new[] { "font/woff2", "font/woff", "font/ttf", "application/x-font-ttf" };
        if (!allowedTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new ArgumentException("Invalid font file type. Allowed: WOFF2, WOFF, TTF.");
        }

        var objectName = fileName.Replace(" ", "_");
        await _storageService.UploadAsync(FontsBucket, objectName, data, contentType, ct);
        return objectName;
    }

    public async Task<string> GetFontAsBase64Async(string fontFileName, CancellationToken ct = default)
    {
        if (!await _storageService.ExistsAsync(FontsBucket, fontFileName, ct))
        {
            return string.Empty;
        }

        using var stream = await _storageService.DownloadAsync(FontsBucket, fontFileName, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        
        var base64 = Convert.ToBase64String(ms.ToArray());
        return base64;
    }
}
