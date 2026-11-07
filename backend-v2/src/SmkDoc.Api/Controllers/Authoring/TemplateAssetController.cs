using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template asset and validation operations: file download, placeholder extraction, and payload dry-run validation.
/// Auth: Channel A (X-API-Key) or Channel B (Portal Bearer JWT) via ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateAssetController(
    IUseCase<DownloadTemplateQuery, DownloadTemplateResult> downloadTemplateUseCase,
    IUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto> scanUploadedUseCase,
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

    /// <summary>
    /// Stateless scan — upload any file and get its placeholders without creating a template.
    /// Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScanFieldsStateless(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("File is required and must not be empty.");

        var ext = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var query = new ScanUploadedTemplateQuery(stream, ext);
        var result = await scanUploadedUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResultDto>(result));
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
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new DomainValidationException("Request payload body cannot be null or empty.");
        }

        var query = new ValidateTemplatePayloadQuery(slug, data);
        var result = await validatePayloadUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ValidateTemplatePayloadResult>(result));
    }
}

