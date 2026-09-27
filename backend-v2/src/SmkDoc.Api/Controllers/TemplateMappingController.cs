using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Application.UseCases.FieldMappings;

namespace SmkDoc.Api.Controllers;

/// <summary>
/// Template Field Mappings — get, save, and preview with sample data.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}/mappings")]
[Authorize(Policy = "ApiKeyPolicy")]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveMappings(
        [FromRoute] Guid id,
        [FromBody] List<SaveFieldMappingItemDto> mappings,
        CancellationToken ct)
    {
        await mappingUseCase.SaveMappingsAsync(id, mappings, ct);
        return NoContent();
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
