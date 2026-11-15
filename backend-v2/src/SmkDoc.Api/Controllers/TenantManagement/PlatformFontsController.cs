using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.TenantManagement.Fonts.Commands.RegisterPlatformFont;
using SmkDoc.Application.Modules.TenantManagement.Fonts.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Fonts.Queries.ListPlatformFonts;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.TenantManagement;

/// <summary>
/// SuperAdmin Control Plane catalog and registration for platform-wide system fonts.
/// </summary>
[ApiController]
[Route("api/v1/management/fonts")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PlatformFontsController(
    IUseCase<ListPlatformFontsQuery, IReadOnlyList<PlatformFontResultDto>> listFontsUseCase,
    IUseCase<RegisterPlatformFontCommand, PlatformFontResultDto> registerFontUseCase) : ControllerBase
{
    /// <summary>List system-wide fonts (and optionally tenant-scoped fonts when projectId is specified).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlatformFontResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFonts([FromQuery] Guid? projectId = null, CancellationToken ct = default)
    {
        var query = new ListPlatformFontsQuery(projectId);
        var result = await listFontsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IReadOnlyList<PlatformFontResultDto>>(result));
    }

    /// <summary>Register a platform-wide system font in control-plane storage. Max 8 MB.</summary>
    [HttpPost]
    [Idempotent]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<PlatformFontResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterFont(
        IFormFile file,
        [FromForm] string? familyName = null,
        [FromForm] string weight = "normal",
        [FromForm] string style = "normal",
        CancellationToken ct = default)
    {
        if (file is not { Length: > 0 })
        {
            throw new DomainValidationException("Font file is required and must not be empty.");
        }

        await using var stream = file.OpenReadStream();
        var command = new RegisterPlatformFontCommand(stream, file.FileName, file.ContentType, familyName, weight, style);
        var result = await registerFontUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(ListFonts), new { projectId = result.ProjectId }, new ApiResponse<PlatformFontResultDto>(result));
    }
}
