using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;

public record GetFontBase64Query(string FontFileName);

public sealed class GetFontBase64UseCase(IStorageService storageService) : IUseCase<GetFontBase64Query, string>
{
    private readonly IStorageService _storageService = storageService;
    private const string FontsBucket = "fonts";

    public async Task<string> ExecuteAsync(GetFontBase64Query query, CancellationToken ct = default)
    {
        if (!await _storageService.ExistsAsync(FontsBucket, query.FontFileName, ct))
        {
            return string.Empty;
        }

        using var stream = await _storageService.DownloadAsync(FontsBucket, query.FontFileName, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return Convert.ToBase64String(ms.ToArray());
    }
}
