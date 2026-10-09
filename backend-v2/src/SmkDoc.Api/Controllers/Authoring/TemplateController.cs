using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template lifecycle management: listing, creation, metadata updates, deactivation, and downloads.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware) or Portal Bearer JWT.
/// </summary>
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
    ValidateTemplatePayloadUseCase validatePayloadUseCase,
    IExecutionContext context) : ControllerBase
{
    /// <summary>List all templates belonging to the active project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var targetProjectId = projectId ?? context.ProjectId
            ?? throw new BadHttpRequestException("ProjectId is required to list templates.");

        var query = new ListTemplatesQuery(targetProjectId);
        var templates = await listTemplatesUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateResultDto>>(templates));
    }

    /// <summary>Get template details by unique identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateByIdQuery(id);
        var template = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateResultDto>(template));
    }

    /// <summary>Create a new document template with optional initial file upload.</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTemplate(
        [FromForm] string name,
        [FromForm] string slug,
        [FromForm] string? category,
        IFormFile? file,
        CancellationToken ct)
    {
        var (stream, fileName) = await ReadUploadFileAsync(file, ct);
        var command = new CreateTemplateCommand(Guid.Empty, name, slug, category, stream, fileName);
        var created = await createTemplateUseCase.ExecuteAsync(command, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<TemplateResultDto>(created));
    }

    /// <summary>Update template display name and category metadata.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        [FromRoute] Guid id,
        [FromBody] UpdateTemplateMetadataRequest request,
        CancellationToken ct)
    {
        var command = new UpdateTemplateDetailsCommand(id, request.Name ?? string.Empty, request.Category);
        var result = await updateTemplateDetailsUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }

    /// <summary>Deactivate (soft delete) a template.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        var command = new DeactivateTemplateCommand(id);
        await deactivateTemplateUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

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
    /// Dry-run schema validation for an incoming payload against a published template's schema contract.
    /// Multi-tenant scoped. Zero side-effects (no DB write, no MinIO upload, no rendering).
    /// </summary>
    [HttpPost("{slug}/validate")]
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

    private static async Task<(Stream? Stream, string? FileName)> ReadUploadFileAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is not { Length: > 0 }) return (null, null);

        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return (ms, file.FileName);
    }
}
