using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// HTML Studio — Monaco Editor integration: get/save HTML source, view schema, validate.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
[Route("api/templates/{id:guid}")]
public class TemplateHtmlController(
    IHtmlStudioUseCase htmlStudioUseCase,
    IHtmlPersistenceUseCase htmlPersistenceUseCase,
    TemplateValidateUseCase validateUseCase) : ControllerBase
{
    /// <summary>Get raw HTML source for the Monaco Editor.</summary>
    [HttpGet("html")]
    [Produces("text/html")]
    public async Task<IActionResult> GetHtml([FromRoute] Guid id, CancellationToken ct)
    {
        string html = await htmlStudioUseCase.GetHtmlSourceAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>Get the full Studio bundle: HTML source + field mappings + JSON schema.</summary>
    [HttpGet("studio")]
    [ProducesResponseType(typeof(ApiResponse<TemplateStudioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudioBundle([FromRoute] Guid id, CancellationToken ct)
    {
        var bundle = await htmlStudioUseCase.GetStudioBundleAsync(id, ct);
        return Ok(new ApiResponse<TemplateStudioDto>(bundle));
    }

    /// <summary>Get the inferred JSON Schema Draft-07 for a template.</summary>
    [HttpGet("schema")]
    [ProducesResponseType(typeof(ApiResponse<TemplateSchemaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchema([FromRoute] Guid id, CancellationToken ct)
    {
        var schemaDto = await htmlStudioUseCase.GetTemplateSchemaAsync(id, ct);
        return Ok(new ApiResponse<TemplateSchemaDto>(schemaDto));
    }

    /// <summary>Save HTML from editor — auto-increments template version.</summary>
    [HttpPut("html")]
    [ProducesResponseType(typeof(ApiResponse<SaveHtmlResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveHtml(
        [FromRoute] Guid id,
        [FromBody] SaveTemplateHtmlCommand request,
        CancellationToken ct)
    {
        int newVersion = await htmlPersistenceUseCase.SaveHtmlVersionAsync(id, request, ct);
        return Ok(new ApiResponse<SaveHtmlResponse>(new SaveHtmlResponse(newVersion)));
    }

    /// <summary>Validate HTML template syntax without saving.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<TemplateValidationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(
        [FromRoute] Guid id,
        [FromBody] SaveTemplateHtmlCommand request,
        CancellationToken ct)
    {
        var result = await validateUseCase.ValidateHtmlAsync(request.Html, ct);
        return Ok(new ApiResponse<TemplateValidationResultDto>(result));
    }
}
