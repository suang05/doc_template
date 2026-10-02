using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Api.Models;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Field Scanning — discover placeholders from stored or uploaded templates.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
[Route("api/templates")]
public class TemplateScanController(
    ITemplateScannerService scanner,
    ScanTemplatePlaceholdersUseCase scanUseCase) : ControllerBase
{
    /// <summary>
    /// Stateless scan — upload any file and get its placeholders without creating a template.
    /// Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScanFieldsStateless(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = file.OpenReadStream();
        var placeholders = await scanner.ScanPlaceholdersAsync(stream, ext, ct);
        return Ok(new ApiResponse<ScanFieldsResponse>(new ScanFieldsResponse(placeholders)));
    }

    /// <summary>Scan placeholders from a stored template file in MinIO.</summary>
    [HttpGet("{id:guid}/scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ScanFieldsById([FromRoute] Guid id, CancellationToken ct)
    {
        var placeholders = await scanUseCase.ExecuteAsync(new ScanTemplatePlaceholdersQuery(id), ct);
        return Ok(new ApiResponse<ScanFieldsResponse>(new ScanFieldsResponse(placeholders)));
    }
}
