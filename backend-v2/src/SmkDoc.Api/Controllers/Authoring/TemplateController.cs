using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

namespace SmkDoc.Api.Controllers.Authoring;

[ApiController]
[Route("api/v1/templates")]
[Route("api/templates")]
public class TemplateController(
    ListTemplatesUseCase listTemplatesUseCase,
    GetTemplateByIdUseCase getTemplateByIdUseCase,
    CreateTemplateUseCase createTemplateUseCase,
    UpdateTemplateDetailsUseCase updateTemplateDetailsUseCase,
    DeactivateTemplateUseCase deactivateTemplateUseCase,
    DownloadTemplateUseCase downloadTemplateUseCase,
    ValidateTemplatePayloadUseCase payloadValidator,
    IExecutionContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates(
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        var targetProjectId = projectId ?? context.ProjectId 
            ?? throw new BadHttpRequestException("ProjectId is required to list templates.");
        var templates = await listTemplatesUseCase.ExecuteAsync(new ListTemplatesQuery(targetProjectId), ct);
        return Ok(new ApiResponse<IEnumerable<TemplateResultDto>>(templates));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var template = await getTemplateByIdUseCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
        return Ok(new ApiResponse<TemplateResultDto>(template));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromForm] string name, [FromForm] string slug,
        [FromForm] string? category, IFormFile? file, CancellationToken ct)
    {
        Stream? stream = null;
        string? fileName = null;
        if (file is { Length: > 0 })
        {
            var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            stream = ms;
            fileName = file.FileName;
        }

        var command = new CreateTemplateCommand(Guid.Empty, name, slug, category, stream, fileName);
        var created = await createTemplateUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<object>(new { id = created.Id, slug = created.Slug }));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMetadata([FromRoute] Guid id,
        [FromBody] UpdateTemplateMetadataRequest request, CancellationToken ct)
    {
        var command = new UpdateTemplateDetailsCommand(id, request.Name ?? string.Empty, request.Category);
        var result = await updateTemplateDetailsUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        await deactivateTemplateUseCase.ExecuteAsync(new DeactivateTemplateCommand(id), ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await downloadTemplateUseCase.ExecuteAsync(new DownloadTemplateQuery(id), ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Dry-run schema validation for an incoming payload against a published template's schema contract.
    /// Multi-tenant scoped. Zero side-effects (no DB write, no MinIO upload, no rendering).
    /// </summary>
    [HttpPost("{slug}/validate")]
    [HttpPost("{slug}/validate-payload")]
    [ProducesResponseType(typeof(ApiResponse<ValidateTemplatePayloadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidatePayload(
        [FromRoute] string slug,
        [FromBody] JsonElement data,
        CancellationToken ct)
    {
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BadRequest(new ApiResponse<object>(new { error = "Request payload body cannot be null or empty." }));
        }

        var result = await payloadValidator.ExecuteAsync(new ValidateTemplatePayloadQuery(slug, data), ct);
        return Ok(new ApiResponse<ValidateTemplatePayloadResult>(result));
    }
}
