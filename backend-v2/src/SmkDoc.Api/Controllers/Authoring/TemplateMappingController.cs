using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Field Mappings — get, save, and preview with sample data.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}/mappings")]
[Route("api/templates/{id:guid}/mappings")]
public class TemplateMappingController(
    FieldMappingUseCase mappingUseCase,
    PreviewMappingUseCase previewMappingUseCase) : ControllerBase
{
    /// <summary>Get all field mappings for a template.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var mappings = await mappingUseCase.GetMappingsByTemplateIdAsync(id, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(mappings));
    }

    /// <summary>Save (replace-all) field mappings for a template.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveMappings(
        [FromRoute] Guid id,
        [FromBody] List<SaveFieldMappingItemDto> mappings,
        CancellationToken ct)
    {
        await mappingUseCase.SaveMappingsAsync(id, mappings, ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }

    /// <summary>
    /// Preview PDF rendered with field mapping sample data.
    /// Zero side-effects — no DB writes, no MinIO uploads.
    /// </summary>
    [HttpPost("preview")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewMappings(
        [FromRoute] Guid id,
        [FromBody] PreviewMappingsQuery request,
        CancellationToken ct)
    {
        byte[] pdfBytes = await previewMappingUseCase.ExecuteAsync(id, request.SampleData, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }
}
