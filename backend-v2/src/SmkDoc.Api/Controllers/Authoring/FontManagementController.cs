using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Fonts;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;

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
    /// <summary>List all uploaded custom fonts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFonts(CancellationToken ct)
    {
        var query = new ListFontsQuery();
        var fonts = await listFontsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<string>>(fonts));
    }

    /// <summary>Upload a new font file (.ttf, .otf). Max 10 MB.</summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFontResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFont(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("Font file is required and must not be empty.");

        using var stream = file.OpenReadStream();
        var command = new UploadFontCommand(stream, file.FileName, file.ContentType);
        var fontName = await uploadFontUseCase.ExecuteAsync(command, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<UploadFontResponse>(new UploadFontResponse("Font uploaded successfully.", fontName)));
    }
}
