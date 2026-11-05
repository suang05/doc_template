using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Services;

public interface IFontCatalogService
{
    IReadOnlyList<FontDescriptorDto> GetCuratedGoogleFonts();

    Task<FontCatalogDto> GetCatalogAsync(CancellationToken ct = default);
}

