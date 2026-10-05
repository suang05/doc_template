using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Draft Upload Pipeline (3-step, RAM-only until Commit).
/// Steps 1 and 2 are pure/side-effect-free. Step 3 commits atomically to MinIO + DB.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/draft")]
[Route("api/templates/draft")]
public class TemplateDraftController(
    ParseTemplateDraftUseCase parseUseCase,
    PreviewTemplateDraftUseCase previewUseCase,
    CommitTemplateDraftUseCase commitUseCase) : ControllerBase
{
    /// <summary>
    /// Step 1 — Parse: upload file to RAM cache, return draftId + discovered placeholders.
    /// Zero side-effects (no DB or MinIO writes).
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(ApiResponse<ParseDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseDraft(IFormFile file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            throw new DomainValidationException("File is required and must not be empty.");

        using var stream = file.OpenReadStream();
        var command = new ParseTemplateDraftCommand(stream, file.FileName);
        var result = await parseUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ParseDraftResponse>(new ParseDraftResponse(result.DraftId, result.Placeholders)));
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
        byte[] pdfBytes = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Step 3 — Commit: persist draft to MinIO + write DB atomically.
    /// Returns HTTP 410 if draft has expired from cache.
    /// </summary>
    [HttpPost("{draftId}/commit")]
    [ProducesResponseType(typeof(ApiResponse<CommitDraftResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> CommitDraft(
        [FromRoute] string draftId,
        [FromBody] CommitDraftRequest request,
        CancellationToken ct)
    {
        var commandPayload = new CommitDraftCommand(request.Name, request.Slug, request.Category, request.Mappings,
            request.ProjectId);
        var command = new CommitTemplateDraftCommand(draftId, commandPayload);
        var templateId = await commitUseCase.ExecuteAsync(command, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<CommitDraftResponse>(new CommitDraftResponse(Guid.Parse(templateId))));
    }
}
