using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Synchronous and asynchronous document generation, batch job queue polling, and ephemeral live preview.
/// Auth: Governed by CanGenerateDocuments policy (M2M API Keys &amp; Authenticated User Sessions).
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = AuthorizationPolicies.CanGenerateDocuments)]
public sealed class DocumentGenerationController(
    IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto> generateUseCase,
    IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto> enqueueJobUseCase,
    IUseCase<GetDocumentJobQuery, DocumentJobDto> getJobUseCase,
    IUseCase<PreviewDocumentQuery, Stream> previewUseCase) : ControllerBase
{
    /// <summary>Track 1: Synchronous document generation from template slug and input JSON data.</summary>
    [HttpPost("generate/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var command = new GenerateDocumentCommand(slug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation ?? false);
        var result = await generateUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<GenerateDocumentResultDto>(result));
    }

    /// <summary>Track 2: Asynchronous document generation queue for high-volume enterprise batches (HTTP 202).</summary>
    [HttpPost("generate-async/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAsync([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var command = new EnqueueDocumentGenerationCommand(slug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation ?? false);
        var result = await enqueueJobUseCase.ExecuteAsync(command, ct);
        return AcceptedAtAction(
            nameof(GetJobStatus),
            new { jobId = result.JobId },
            new ApiResponse<DocumentJobDto>(result));
    }

    /// <summary>Track 2: Observability &amp; Polling endpoint to check status of an asynchronous document job.</summary>
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new GetDocumentJobQuery(jobId);
        var result = await getJobUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DocumentJobDto>(result));
    }

    /// <summary>Ephemeral live preview for Monaco Editor / Form with zero side-effects (Raw HTML).</summary>
    [HttpPost("preview")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewRaw([FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Payload, request.Html, null);
        var result = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(result, "application/pdf");
    }

    /// <summary>Ephemeral live preview for a specific template slug with zero side-effects.</summary>
    [HttpPost("preview/{slug}")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewBySlug([FromRoute] string slug, [FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Payload, request.Html, slug);
        var result = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(result, "application/pdf");
    }
}

