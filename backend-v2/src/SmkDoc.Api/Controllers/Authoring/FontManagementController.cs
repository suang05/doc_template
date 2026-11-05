using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Font management — dual-source font catalog (Google Fonts + MinIO custom fonts) and upload.
/// </summary>
[ApiController]
[Route("api/v1/fonts")]
[Route("api/v1/management/settings/fonts")]
[Authorize]
public sealed class FontManagementController(
    IUseCase<ListFontsQuery, List<string>> listFontsUseCase,
    IUseCase<GetFontCatalogQuery, FontCatalogDto> getFontCatalogUseCase,
    IUseCase<UploadFontCommand, UploadFontResultDto> uploadFontUseCase) : ControllerBase
{
    /// <summary>List all available fonts for the active project (legacy flat list).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFonts(CancellationToken ct)
    {
        var query = new ListFontsQuery();
        var result = await listFontsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<string>>(result));
    }

    /// <summary>Get dual-source font catalog (Google Fonts + MinIO Custom Fonts).</summary>
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(ApiResponse<FontCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
    {
        var query = new GetFontCatalogQuery();
        var result = await getFontCatalogUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<FontCatalogDto>(result));
    }

    /// <summary>Upload a new font file (.ttf, .otf, .woff, .woff2). Max 10 MB. Author or Admin role required.</summary>
    [HttpPost]
    [Authorize(Policy = SmkDocPolicies.CanManageTemplates)]
    [Idempotent]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFontResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadFont(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("Font file is required and must not be empty.");

        await using var stream = file.OpenReadStream();
        var command = new UploadFontCommand(stream, file.FileName, file.ContentType);
        var result = await uploadFontUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(ListFonts), new ApiResponse<UploadFontResultDto>(result));
    }
}
