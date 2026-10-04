using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;

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
    private readonly ParseTemplateDraftUseCase _parseUseCase = parseUseCase;
    private readonly PreviewTemplateDraftUseCase _previewUseCase = previewUseCase;
    private readonly CommitTemplateDraftUseCase _commitUseCase = commitUseCase;

    /// <summary>
    /// Step 1 — Parse: upload file to RAM cache, return draftId + discovered placeholders.
    /// Zero side-effects (no DB or MinIO writes).
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(ApiResponse<ParseDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseDraft(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var stream = file.OpenReadStream();
        var result = await _parseUseCase.ExecuteAsync(new ParseTemplateDraftCommand(stream, file.FileName), ct);
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
        [FromBody] PreviewDraftQuery request,
        CancellationToken ct)
    {
        byte[] pdfBytes = await _previewUseCase.ExecuteAsync(new PreviewTemplateDraftQuery(draftId, request.DataJson), ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Step 3 — Commit: persist draft to MinIO + write DB atomically.
    /// Returns HTTP 410 if draft has expired from cache.
    /// </summary>
    [HttpPost("{draftId}/commit")]
    [ProducesResponseType(typeof(ApiResponse<CommitDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> CommitDraft(
        [FromRoute] string draftId,
        [FromBody] CommitDraftCommand request,
        CancellationToken ct)
    {
        var templateId = await _commitUseCase.ExecuteAsync(new CommitTemplateDraftCommand(draftId, request), ct);
        return Ok(new ApiResponse<CommitDraftResponse>(new CommitDraftResponse(Guid.Parse(templateId))));
    }
}
