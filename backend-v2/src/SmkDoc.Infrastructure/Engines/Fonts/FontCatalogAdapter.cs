using System.Buffers;
using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

namespace SmkDoc.Infrastructure.Engines.Fonts;

public sealed class FontCatalogAdapter(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IFontCatalogPort
{
    private const int PooledByteChunkSize = 24_576; // 24 KB, exact multiple of 3 for contiguous Base64 framing
    private const int PooledCharChunkSize = 32_768; // 24_576 * 4 / 3 chars (64 KB < LOH threshold)

    private static readonly FontDescriptorDto[] CuratedGoogleFontsList =
    [
        new("Sarabun", "Sarabun (สารบรรณ)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Sarabun:wght@300;400;600;700&display=swap",
            AvailableWeights: ["300", "400", "600", "700"]),

        new("Prompt", "Prompt (พร้อมท์)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;700&display=swap",
            AvailableWeights: ["300", "400", "500", "700"]),

        new("Kanit", "Kanit (คณิต)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Kanit:wght@300;400;500;700&display=swap",
            AvailableWeights: ["300", "400", "500", "700"]),

        new("Mitr", "Mitr (มิตร)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Mitr:wght@300;400;500&display=swap",
            AvailableWeights: ["300", "400", "500"]),

        new("Bai Jamjuree", "Bai Jamjuree (จามจุรี)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Bai+Jamjuree:wght@300;400;600&display=swap",
            AvailableWeights: ["300", "400", "600"]),

        new("Chakra Petch", "Chakra Petch (จักรเพชร)", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@300;400;600&display=swap",
            AvailableWeights: ["300", "400", "600"]),

        new("Inter", "Inter", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap",
            AvailableWeights: ["400", "500", "600", "700"]),

        new("Roboto", "Roboto", FontSource.GoogleFonts,
            GoogleFontUrl: "https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap",
            AvailableWeights: ["400", "500", "700"])
    ];

    public IReadOnlyList<FontDescriptorDto> GetCuratedGoogleFonts() => CuratedGoogleFontsList;

    public async Task<FontCatalogDto> GetCatalogAsync(CancellationToken ct)
    {
        var projectId = projectContext.GetRequiredProjectId();
        string tenantPrefix = $"fonts/{projectId.Value:N}/";
        var customFontKeys = await storageService.ListObjectsAsync(StorageBuckets.Fonts, tenantPrefix, ct) ?? [];

        var customFonts = new List<FontDescriptorDto>();
        var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var gf in CuratedGoogleFontsList)
        {
            allNames.Add(gf.FontFamily);
        }

        allNames.Add("Sarabun-Regular.ttf");

        foreach (var key in customFontKeys)
        {
            var fileName = Path.GetFileName(key);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var family = Path.GetFileNameWithoutExtension(fileName);
            allNames.Add(fileName);

            customFonts.Add(new FontDescriptorDto(
                FontFamily: family,
                DisplayName: $"{family} (Custom)",
                Source: FontSource.MinIO,
                FileName: fileName));
        }

        var sortedNames = allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        return new FontCatalogDto(
            GoogleFonts: CuratedGoogleFontsList,
            CustomFonts: customFonts.AsReadOnly(),
            AllFontNames: sortedNames);
    }

    public async Task<string> GetFontBase64Async(string fileName, CancellationToken ct)
    {
        var projectId = projectContext.GetRequiredProjectId();

        string safeFileName = UploadFontUseCase.SanitizeAndValidateFileName(fileName);
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

        await using var stream = await storageService.DownloadAsync(StorageBuckets.Fonts, resolvedKey, ct);
        return await EncodeStreamToBase64Async(stream, ct);
    }

    private static async Task<string> EncodeStreamToBase64Async(Stream stream, CancellationToken ct)
    {
        byte[] byteBuffer = ArrayPool<byte>.Shared.Rent(PooledByteChunkSize);
        char[] charBuffer = ArrayPool<char>.Shared.Rent(PooledCharChunkSize);
        try
        {
            var builder = new StringBuilder();
            int buffered = 0;

            while (true)
            {
                int read = await stream.ReadAsync(
                    byteBuffer.AsMemory(buffered, PooledByteChunkSize - buffered),
                    ct);
                buffered += read;

                if (read == 0)
                {
                    if (buffered > 0)
                    {
                        Convert.TryToBase64Chars(byteBuffer.AsSpan(0, buffered), charBuffer, out int charsWritten);
                        builder.Append(charBuffer.AsSpan(0, charsWritten));
                    }

                    break;
                }

                int fullGroups = buffered - (buffered % 3);
                if (fullGroups > 0)
                {
                    Convert.TryToBase64Chars(byteBuffer.AsSpan(0, fullGroups), charBuffer, out int charsWritten);
                    builder.Append(charBuffer.AsSpan(0, charsWritten));

                    int remainder = buffered - fullGroups;
                    if (remainder > 0)
                    {
                        byteBuffer.AsSpan(fullGroups, remainder).CopyTo(byteBuffer);
                    }

                    buffered = remainder;
                }
            }

            return builder.ToString();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
            ArrayPool<char>.Shared.Return(charBuffer);
        }
    }
}
