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
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template lifecycle management: listing, creation, metadata updates, and deactivation.
/// Auth: Channel A (X-API-Key) or Channel B (Portal Bearer JWT) via ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateLifecycleController(
    IUseCase<ListTemplatesQuery, TemplatePagedResultDto> listTemplatesUseCase,
    IUseCase<GetTemplateByIdQuery, TemplateResultDto> getTemplateByIdUseCase,
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase,
    IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> updateTemplateDetailsUseCase,
    IUseCase<DeactivateTemplateCommand> deactivateTemplateUseCase,
    ICurrentProjectContext projectContext) : ControllerBase
{
    /// <summary>List templates belonging to the active project with optional search, category filter, and server-side pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
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
        var result = await listTemplatesUseCase.ExecuteAsync(query, ct);
        return Ok(new PagedApiResponse<TemplateResultDto>(result.Items, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>Get template details by unique identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateByIdQuery(id);
        var result = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
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
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var command = new DeactivateTemplateCommand(id);
        await deactivateTemplateUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}

