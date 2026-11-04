using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Unified Monaco Studio and Authoring Controller: HTML source editing, schema inspection,
/// syntax validation, and field mapping configuration with live preview.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware) or Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
public sealed class TemplateStudioController(
    IUseCase<GetTemplateHtmlSourceQuery, string> getHtmlSourceUseCase,
    IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto> getStudioBundleUseCase,
    IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto> getSchemaUseCase,
    IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto> saveHtmlUseCase,
    IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto> validateUseCase,
    IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>> getMappingsUseCase,
    IUseCase<SaveTemplateMappingsCommand> saveMappingsUseCase,
    IUseCase<PreviewMappingQuery, Stream> previewMappingUseCase) : ControllerBase
{
    /// <summary>Get raw HTML source for Monaco Editor.</summary>
    [HttpGet("html")]
    [Produces("text/html")]
    public async Task<IActionResult> GetHtml([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateHtmlSourceQuery(id);
        var result = await getHtmlSourceUseCase.ExecuteAsync(query, ct);
        return Content(result, "text/html; charset=utf-8");
    }

    /// <summary>Get full Studio bundle: HTML source + field mappings + JSON schema.</summary>
    [HttpGet("studio")]
    [HttpGet("studio/bundle")]
    [ProducesResponseType(typeof(ApiResponse<TemplateStudioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudioBundle([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateStudioBundleQuery(id);
        var result = await getStudioBundleUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateStudioDto>(result));
    }

    /// <summary>Get inferred JSON Schema Draft-07 for a template.</summary>
    [HttpGet("schema")]
    [ProducesResponseType(typeof(ApiResponse<TemplateSchemaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchema([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateSchemaQuery(id);
        var result = await getSchemaUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateSchemaDto>(result));
    }

    /// <summary>Save HTML from Monaco Editor — increments template version with optimistic concurrency check.</summary>
    [HttpPut("html")]
    [ProducesResponseType(typeof(ApiResponse<SaveHtmlResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> SaveHtml(
        [FromRoute] Guid id,
        [FromBody] SaveHtmlRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new SaveTemplateHtmlCommand(id, request.Html, request.SamplePayload, request.ChangeNote, ifMatch);
        var result = await saveHtmlUseCase.ExecuteAsync(command, ct);
        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(new ApiResponse<SaveHtmlResultDto>(result));
    }

    /// <summary>Validate HTML template syntax and Handlebars tags without persisting.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<TemplateValidationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(
        [FromRoute] Guid id,
        [FromBody] ValidateHtmlRequest request,
        CancellationToken ct)
    {
        var query = new ValidateTemplateHtmlQuery(request.Html);
        var result = await validateUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateValidationResultDto>(result));
    }

    /// <summary>Get all field mappings for a template.</summary>
    [HttpGet("mappings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateMappingsQuery(id);
        var result = await getMappingsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(result));
    }

    /// <summary>Save (replace-all) field mappings for a template.</summary>
    [HttpPut("mappings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveMappings(
        [FromRoute] Guid id,
        [FromBody] SaveTemplateMappingsRequest request,
        CancellationToken ct)
    {
        var mappings = request.Mappings ?? [];
        var dtos = mappings.Select(m => new SaveFieldMappingItemDto(
            m.Placeholder, m.SourcePath, m.Label, m.Required, m.DefaultValue, m.Transform,
            m.SortOrder, m.DataSourceType, m.DatasetAlias, m.ResultPath, m.MathExpression)).ToList();
        var command = new SaveTemplateMappingsCommand(id, dtos);
        await saveMappingsUseCase.ExecuteAsync(command, ct);

        var query = new GetTemplateMappingsQuery(id);
        var result = await getMappingsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(result));
    }

    /// <summary>
    /// Preview PDF rendered with field mapping sample data.
    /// Zero side-effects — no DB writes, no MinIO uploads.
    /// </summary>
    [HttpPost("mappings/preview")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewMappings(
        [FromRoute] Guid id,
        [FromBody] PreviewMappingRequest request,
        CancellationToken ct)
    {
        var query = new PreviewMappingQuery(id, request.SampleData);
        var pdfStream = await previewMappingUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }
}

