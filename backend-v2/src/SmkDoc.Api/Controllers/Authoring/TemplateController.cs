using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template lifecycle management: listing, creation, metadata updates, deactivation, downloads, and placeholder scanning.
/// Auth: Channel A (X-API-Key) or Channel B (Portal Bearer JWT) via ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    IUseCase<ListTemplatesQuery, TemplatePagedResultDto> listTemplatesUseCase,
    IUseCase<GetTemplateByIdQuery, TemplateResultDto> getTemplateByIdUseCase,
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase,
    IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> updateTemplateDetailsUseCase,
    IUseCase<DeactivateTemplateCommand> deactivateTemplateUseCase,
    IUseCase<DownloadTemplateQuery, DownloadTemplateResult> downloadTemplateUseCase,
    IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> validatePayloadUseCase,
    IUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto> scanUploadedUseCase,
    IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto> scanStoredUseCase,
    ICurrentProjectContext projectContext) : ControllerBase
{
    /// <summary>List templates belonging to the active project with optional search, category filter, and server-side pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to list templates.");
        }

        if (projectId.HasValue && projectId.Value != Guid.Empty && projectId.Value != projectContext.ProjectId.Value)
        {
            throw new ForbiddenException("Access denied: requested projectId does not match the active project context.");
        }

        var query = new ListTemplatesQuery(search, category, isActive, page, pageSize);
        var pagedResult = await listTemplatesUseCase.ExecuteAsync(query, ct);

        Response.Headers["X-Total-Count"] = pagedResult.TotalCount.ToString();
        Response.Headers["X-Page"] = pagedResult.Page.ToString();
        Response.Headers["X-Page-Size"] = pagedResult.PageSize.ToString();

        var result = pagedResult.Items;
        return Ok(new ApiResponse<IEnumerable<TemplateResultDto>>(result));
    }

    /// <summary>Get template details by unique identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateByIdQuery(id);
        var result = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
        Response.Headers.ETag = $"\"{(result.UpdatedAt ?? result.CreatedAt).ToUnixTimeMilliseconds()}\"";
        return Ok(new ApiResponse<TemplateResultDto>(result));
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
        await using var stream = file?.Length > 0 ? file.OpenReadStream() : null;
        var fileName = file?.Length > 0 ? file.FileName : null;
        var command = new CreateTemplateCommand(name, slug, category, stream, fileName);
        var result = await createTemplateUseCase.ExecuteAsync(command, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
    }

    /// <summary>Update template display name and category metadata with optimistic concurrency check.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> UpdateMetadata(
        [FromRoute] Guid id,
        [FromBody] UpdateTemplateMetadataRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new UpdateTemplateDetailsCommand(id, request.Name ?? string.Empty, request.Category, ifMatch);
        var result = await updateTemplateDetailsUseCase.ExecuteAsync(command, ct);
        Response.Headers.ETag = $"\"{(result.UpdatedAt ?? result.CreatedAt).ToUnixTimeMilliseconds()}\"";
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
    /// Stateless scan — upload any file and get its placeholders without creating a template.
    /// Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScanFieldsStateless(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("File is required and must not be empty.");

        var ext = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var query = new ScanUploadedTemplateQuery(stream, ext);
        var result = await scanUploadedUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResultDto>(result));
    }

    /// <summary>Scan placeholders from a stored template file in MinIO.</summary>
    [HttpGet("{id:guid}/scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<ScanFieldsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanFieldsById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new ScanTemplatePlaceholdersQuery(id);
        var result = await scanStoredUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ScanFieldsResultDto>(result));
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
}
