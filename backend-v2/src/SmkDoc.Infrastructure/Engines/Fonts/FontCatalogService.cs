using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Services;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Engines.Fonts;

public sealed class FontCatalogService(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IFontCatalogService
{
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

    public async Task<FontCatalogDto> GetCatalogAsync(CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Listing font catalog requires an active project context.");
        }

        string tenantPrefix = $"fonts/{projectContext.ProjectId.Value:N}/";
        var customFontKeys = await storageService.ListObjectsAsync(StorageBuckets.Fonts, tenantPrefix, ct) ?? [];

        var customFonts = new List<FontDescriptorDto>();
        var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add Google Fonts
        foreach (var gf in CuratedGoogleFontsList)
        {
            allNames.Add(gf.FontFamily);
        }

        // Add Default system font
        allNames.Add("Sarabun-Regular.ttf");

        // Add Custom MinIO Fonts
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
}
