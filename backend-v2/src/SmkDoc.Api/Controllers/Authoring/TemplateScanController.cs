using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Field Scanning — discover placeholders from stored or uploaded templates.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware).
/// </summary>
[ApiController]
[Route("api/v1/templates")]
[Route("api/templates")]
public class TemplateScanController(
    ScanUploadedTemplateUseCase scanUploadedUseCase,
    ScanTemplatePlaceholdersUseCase scanStoredUseCase) : ControllerBase
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
        if (file is not { Length: > 0 })
            throw new DomainValidationException("File is required and must not be empty.");

        var ext = Path.GetExtension(file.FileName);
        using var stream = file.OpenReadStream();
        var query = new ScanUploadedTemplateQuery(stream, ext);
        var placeholders = await scanUploadedUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResponse>(new ScanFieldsResponse(placeholders)));
    }

    /// <summary>Scan placeholders from a stored template file in MinIO.</summary>
    [HttpGet("{id:guid}/scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanFieldsById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new ScanTemplatePlaceholdersQuery(id);
        var placeholders = await scanStoredUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResponse>(new ScanFieldsResponse(placeholders)));
    }
}
