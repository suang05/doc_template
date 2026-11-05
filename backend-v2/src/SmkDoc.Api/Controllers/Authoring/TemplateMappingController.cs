using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template field mappings management and live preview rendering.
/// Auth: Channel A (X-API-Key) or Channel B (Portal Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}/mappings")]
public sealed class TemplateMappingController(
    IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>> getMappingsUseCase,
    IUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>> saveMappingsUseCase,
    IUseCase<PreviewMappingQuery, Stream> previewMappingUseCase) : ControllerBase
{
    /// <summary>Get all field mappings for a template.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateMappingsQuery(id);
        var result = await getMappingsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(result));
    }

    /// <summary>Save (replace-all) field mappings for a template.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> SaveMappings(
        [FromRoute] Guid id,
        [FromBody] SaveTemplateMappingsRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var mappings = (request.Mappings ?? []).Select(m => new SaveFieldMappingItemDto(
            m.Placeholder, m.SourcePath, m.Label, m.Required, m.DefaultValue, m.Transform,
            m.SortOrder, m.DataSourceType, m.DatasetAlias, m.ResultPath, m.MathExpression)).ToList();
        var command = new SaveTemplateMappingsCommand(id, mappings, ifMatch);
        var result = await saveMappingsUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(result));
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
        var query = new PreviewMappingQuery(id, request.SampleData);
        var result = await previewMappingUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(result, "application/pdf");
    }
}

