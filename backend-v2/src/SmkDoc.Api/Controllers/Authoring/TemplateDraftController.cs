using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Draft Upload Pipeline (3-step, RAM-only until Commit).
/// Steps 1 and 2 are pure/side-effect-free. Step 3 commits atomically to MinIO + DB.
/// </summary>
[ApiController]
[Route("api/v1/templates/draft")]
[Authorize(Policy = AuthorizationPolicies.CanManageTemplates)]
public sealed class TemplateDraftController(
    IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto> parseUseCase,
    IUseCase<PreviewTemplateDraftQuery, Stream> previewUseCase,
    IUseCase<CommitTemplateDraftCommand, CommitDraftResultDto> commitUseCase) : ControllerBase
{
    /// <summary>
    /// Step 1 — Parse: upload file to RAM cache, return draftId + discovered placeholders.
    /// Zero side-effects (no DB or MinIO writes).
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(ApiResponse<ParseDraftResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseDraft(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("File is required and must not be empty.");

        await using var stream = file.OpenReadStream();
        var command = new ParseTemplateDraftCommand(stream, file.FileName);
        var result = await parseUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ParseDraftResultDto>(result));
    }

    /// <summary>
    /// Step 2 — Preview: render PDF from cached draft with sample data.
    /// Zero side-effects. Returns HTTP 410 if draft has expired from cache.
    /// </summary>
    [HttpPost("{draftId}/preview")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> PreviewDraft(
        [FromRoute] string draftId,
        [FromBody] PreviewDraftRequest request,
        CancellationToken ct)
    {
        var query = new PreviewTemplateDraftQuery(draftId, request.DataJson);
        var result = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(result, "application/pdf");
    }

    /// <summary>
    /// Step 3 — Commit: persist draft to MinIO + write DB atomically.
    /// Returns HTTP 410 if draft has expired from cache.
    /// </summary>
    [HttpPost("{draftId}/commit")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<CommitDraftResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> CommitDraft(
        [FromRoute] string draftId,
        [FromBody] CommitDraftRequest request,
        CancellationToken ct)
    {
        var command = new CommitTemplateDraftCommand(
            draftId,
            request.Name,
            request.Slug,
            request.Category,
            request.Mappings ?? []);
        var result = await commitUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(TemplateLifecycleController.GetById), "TemplateLifecycle", new { id = result.TemplateId }, new ApiResponse<CommitDraftResultDto>(result));
    }
}
