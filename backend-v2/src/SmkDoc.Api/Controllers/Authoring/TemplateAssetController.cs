using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template asset and validation operations: file download, placeholder extraction, and payload dry-run validation.
/// Auth: Channel A (X-API-Key) or Channel B (Portal Bearer JWT) via ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateAssetController(
    IUseCase<DownloadTemplateQuery, DownloadTemplateResult> downloadTemplateUseCase,
    IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto> scanStoredUseCase,
    IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> validatePayloadUseCase) : ControllerBase
{
    /// <summary>Download the current published template file.</summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new DownloadTemplateQuery(id);
        var result = await downloadTemplateUseCase.ExecuteAsync(query, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>Scan placeholders from a stored template file in MinIO.</summary>
    [HttpGet("{id:guid}/scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanFieldsById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new ScanTemplatePlaceholdersQuery(id);
        var result = await scanStoredUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResultDto>(result));
    }

    /// <summary>
    /// Dry-run schema validation for an incoming payload against a published template's schema contract.
    /// Multi-tenant scoped. Zero side-effects (no DB write, no MinIO upload, no rendering).
    /// </summary>
    [HttpPost("{slug}/validate-payload")]
    [ProducesResponseType(typeof(ApiResponse<ValidateTemplatePayloadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidatePayload(
        [FromRoute] string slug,
        [FromBody] JsonElement data,
        CancellationToken ct)
    {
        var query = new ValidateTemplatePayloadQuery(slug, data);
        var result = await validatePayloadUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ValidateTemplatePayloadResult>(result));
    }
}

