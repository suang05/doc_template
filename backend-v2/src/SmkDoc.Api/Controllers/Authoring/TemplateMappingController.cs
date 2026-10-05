using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Field Mappings — get, save, and preview with sample data.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}/mappings")]
[Route("api/templates/{id:guid}/mappings")]
public class TemplateMappingController(
    GetTemplateMappingsUseCase getMappingsUseCase,
    SaveTemplateMappingsUseCase saveMappingsUseCase,
    PreviewMappingUseCase previewMappingUseCase) : ControllerBase
{
    /// <summary>Get all field mappings for a template.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var mappings = await getMappingsUseCase.ExecuteAsync(new GetTemplateMappingsQuery(id), ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(mappings));
    }

    /// <summary>Save (replace-all) field mappings for a template.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveMappings(
        [FromRoute] Guid id,
        [FromBody] List<SaveFieldMappingItemRequest> mappings,
        CancellationToken ct)
    {
        var dtos = mappings.Select(m => new SaveFieldMappingItemDto(
            m.Placeholder, m.SourcePath, m.Label, m.Required, m.DefaultValue, m.Transform,
            m.SortOrder, m.DataSourceType, m.DatasetAlias, m.ResultPath, m.MathExpression)).ToList();
        await saveMappingsUseCase.ExecuteAsync(new SaveTemplateMappingsCommand(id, dtos), ct);
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
        [FromBody] PreviewMappingRequest request,
        CancellationToken ct)
    {
        byte[] pdfBytes = await previewMappingUseCase.ExecuteAsync(id, request.SampleData, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }
}
