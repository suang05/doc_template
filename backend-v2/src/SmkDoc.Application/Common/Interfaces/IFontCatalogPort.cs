using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

public interface IFontCatalogPort
{
    IReadOnlyList<FontDescriptorDto> GetCuratedGoogleFonts();

    Task<FontCatalogDto> GetCatalogAsync(CancellationToken ct);

    Task<string> GetFontBase64Async(string fileName, CancellationToken ct);
}
