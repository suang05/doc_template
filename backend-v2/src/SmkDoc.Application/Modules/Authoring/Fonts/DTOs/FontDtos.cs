using System.Diagnostics.CodeAnalysis;

namespace SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Wire/API font source enum deliberately excludes a zero default value.")]
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
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    string? GoogleFontUrl = null,
    IReadOnlyList<string>? AvailableWeights = null
)
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    public string? GoogleFontUrl { get; init; } = GoogleFontUrl;
}

public sealed record FontCatalogDto(
    IReadOnlyList<FontDescriptorDto> GoogleFonts,
    IReadOnlyList<FontDescriptorDto> CustomFonts,
    IReadOnlyList<string> AllFontNames
);

