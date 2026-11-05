namespace SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

public enum FontSource
{
    GoogleFonts = 1,
    MinIO = 2,
    System = 3
}

public sealed record FontDescriptorDto(
    string FontFamily,
    string DisplayName,
    FontSource Source,
    string? FileName = null,
    string? GoogleFontUrl = null,
    IReadOnlyList<string>? AvailableWeights = null
);

public sealed record FontCatalogDto(
    IReadOnlyList<FontDescriptorDto> GoogleFonts,
    IReadOnlyList<FontDescriptorDto> CustomFonts,
    IReadOnlyList<string> AllFontNames
);

