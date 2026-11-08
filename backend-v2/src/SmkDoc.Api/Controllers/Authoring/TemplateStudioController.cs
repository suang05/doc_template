using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Monaco Studio authoring controller: HTML source editing, studio bundle inspection,
/// schema retrieval, and HTML syntax validation.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware) or Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
public sealed class TemplateStudioController(
    IUseCase<GetTemplateHtmlSourceQuery, string> getHtmlSourceUseCase,
    IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto> getStudioBundleUseCase,
    IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto> getSchemaUseCase,
    IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto> saveHtmlUseCase,
    IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto> validateUseCase) : ControllerBase
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

    /// <summary>Get full Studio bundle: HTML source + persisted sample payload + JSON schema.</summary>
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
        CancellationToken ct)
    {
        var command = new SaveTemplateHtmlCommand(id, request.Html, request.SamplePayload, request.ChangeNote, request.PageSettings);
        var result = await saveHtmlUseCase.ExecuteAsync(command, ct);
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
}
