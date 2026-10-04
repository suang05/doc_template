using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Fonts;
using SmkDoc.Application.Modules.Authoring.Fonts;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Font management — list and upload custom fonts for PDF rendering.
/// Auth: JWT Bearer (JwtPolicy).
/// </summary>
[ApiController]
[Route("api/v1/management/settings/fonts")]
[Authorize(Policy = "JwtPolicy")]
public class FontManagementController(
    ListFontsUseCase listFontsUseCase,
    UploadFontUseCase uploadFontUseCase) : ControllerBase
{
    private readonly ListFontsUseCase _listFontsUseCase = listFontsUseCase;
    private readonly UploadFontUseCase _uploadFontUseCase = uploadFontUseCase;

    /// <summary>List all uploaded custom fonts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFonts(CancellationToken ct)
    {
        var fonts = await _listFontsUseCase.ExecuteAsync(new ListFontsQuery(), ct);
        return Ok(new ApiResponse<IEnumerable<string>>(fonts));
    }

    /// <summary>Upload a new font file (.ttf, .otf). Max 10 MB.</summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFont(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var stream = file.OpenReadStream();
        var fontName = await _uploadFontUseCase.ExecuteAsync(new UploadFontCommand(stream, file.FileName, file.ContentType), ct);
        return Ok(new ApiResponse<UploadFontResponse>(new UploadFontResponse("Font uploaded successfully.", fontName)));
    }
}
