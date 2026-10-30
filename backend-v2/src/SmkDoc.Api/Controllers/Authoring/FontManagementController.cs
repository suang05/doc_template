using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Authoring.Fonts;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Font management — list and upload custom tenant-scoped fonts for PDF rendering.
/// Auth: JWT Bearer via default authentication scheme + ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/management/settings/fonts")]
[Authorize]
public class FontManagementController(
    ListFontsUseCase listFontsUseCase,
    UploadFontUseCase uploadFontUseCase) : ControllerBase
{
    /// <summary>List all available fonts for the active project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFonts(CancellationToken ct)
    {
        var query = new ListFontsQuery();
        var fonts = await listFontsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<string>>(fonts));
    }

    /// <summary>Upload a new font file (.ttf, .otf, .woff, .woff2). Max 10 MB. Author or Admin role required.</summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.AuthorOrAdmin)]
    [Idempotent]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFontResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadFont(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("Font file is required and must not be empty.");

        await using var stream = file.OpenReadStream();
        var command = new UploadFontCommand(stream, file.FileName, file.ContentType);
        var fontName = await uploadFontUseCase.ExecuteAsync(command, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<UploadFontResponse>(new UploadFontResponse("Font uploaded successfully.", fontName)));
    }
}
