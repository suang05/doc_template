using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Core document rendering, synchronous generation, asynchronous enterprise batch queue,
/// validation, and version retrieval endpoints.
/// Dual-Track Pipeline: Track 1 (Sync Preview &amp; Generation with Rate Limiting), Track 2 (Async Bounded Job Queue).
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = SmkDocPolicies.CanGenerateDocuments)]
public sealed class DocumentController(
    IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto> generateUseCase,
    IUseCase<PreviewDocumentQuery, Stream> previewUseCase,
    IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>> getVersionsUseCase,
    IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult> downloadVersionUseCase,
    IUseCase<GetLogDownloadUrlQuery, string> getLogDownloadUrlUseCase,
    IUseCase<RenderStatelessDocumentCommand, Stream> renderStatelessUseCase,
    IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> validatePayloadUseCase,
    IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto> enqueueJobUseCase,
    IUseCase<GetDocumentJobQuery, DocumentJobDto> getJobUseCase,
    IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult> downloadLogDocumentUseCase) : ControllerBase
{
    /// <summary>
    /// Track 1: Synchronous document generation from template slug and input JSON data.
    /// Protected by fixed-window rendering rate limiter to safeguard Gotenberg Chromium instances.
    /// </summary>
    [HttpPost("generate/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var command = new GenerateDocumentCommand(
            slug,
            request.Payload,
            request.OutputFormat,
            request.DocumentRef,
            request.ChangeNote,
            request.SkipValidation);
        var response = await generateUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<GenerateDocumentResultDto>(response));
    }

    /// <summary>
    /// Track 2: Asynchronous document generation queue for high-volume enterprise batches.
    /// Enqueues the job and immediately returns HTTP 202 Accepted with Location header for status polling.
    /// Background worker throttles Gotenberg execution to prevent memory spikes and thread starvation.
    /// </summary>
    [HttpPost("generate-async/{slug}")]
    [HttpPost("generate-async")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateDocumentRequest request,
        [FromRoute] string? slug,
        [FromQuery] string? templateSlug,
        CancellationToken ct)
    {
        var finalSlug = !string.IsNullOrWhiteSpace(slug) ? slug : templateSlug;
        if (string.IsNullOrWhiteSpace(finalSlug))
        {
            throw new DomainValidationException("Template slug is required to enqueue document generation.");
        }

        var command = new EnqueueDocumentGenerationCommand(
            finalSlug,
            request.Payload,
            request.OutputFormat,
            request.DocumentRef,
            request.ChangeNote,
            request.SkipValidation);

        var job = await enqueueJobUseCase.ExecuteAsync(command, ct);
        var statusUrl = $"/api/v1/documents/jobs/{job.JobId}";
        Response.Headers.Location = statusUrl;
        return StatusCode(StatusCodes.Status202Accepted, new ApiResponse<DocumentJobDto>(job));
    }

    /// <summary>
    /// Track 2: Observability &amp; Polling endpoint to check status of an asynchronous document job.
    /// Multi-tenant scoped to ensure isolation between projects.
    /// </summary>
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new GetDocumentJobQuery(jobId);
        var job = await getJobUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DocumentJobDto>(job));
    }

    /// <summary>
    /// Pre-flight schema validation — checks payload against the template's Draft-07 schema
    /// without generating any document. Multi-tenant scoped. Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("validate/{slug}")]
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

    /// <summary>
    /// Ephemeral live preview for Monaco Editor / Form with zero side-effects.
    /// Rate limited to protect Gotenberg from interactive denial-of-service.
    /// </summary>
    [HttpPost("preview")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewRaw([FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Payload, request.Html, null);
        var pdfStream = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }

    /// <summary>
    /// Ephemeral live preview for a specific template slug with zero side-effects.
    /// Rate limited to protect Gotenberg from interactive denial-of-service.
    /// </summary>
    [HttpPost("preview/{slug}")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewBySlug([FromRoute] string slug, [FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Payload, request.Html, slug);
        var pdfStream = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }

    /// <summary>
    /// Get legal document version history
    /// </summary>
    [HttpGet("{documentRef}/versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] string documentRef, CancellationToken ct)
    {
        var query = new GetDocumentVersionsQuery(documentRef);
        var versions = await getVersionsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<DocumentVersionDto>>(versions));
    }

    /// <summary>
    /// Download specific historical version of a document
    /// </summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var query = new DownloadDocumentVersionQuery(documentRef, version);
        var result = await downloadVersionUseCase.ExecuteAsync(query, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Download or get pre-signed URL for a generated document.
    /// Dual-mode: returns JSON when Accept contains application/json, or bounded stream pass-through for direct download.
    /// Keeps MinIO strictly internal to the private VPC, eliminating network routing failures for external clients.
    /// </summary>
    [HttpGet("download/{logId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        var acceptHeader = Request.Headers.Accept.ToString();
        if (acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var query = new GetLogDownloadUrlQuery(logId);
            var url = await getLogDownloadUrlUseCase.ExecuteAsync(query, ct);
            return Ok(new ApiResponse<DownloadUrlResponseDto>(new DownloadUrlResponseDto(url, 3600)));
        }

        var downloadQuery = new DownloadGenerationLogDocumentQuery(logId);
        var result = await downloadLogDocumentUseCase.ExecuteAsync(downloadQuery, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Render any document to PDF directly from an uploaded file stream and JSON payload.
    /// Protected by rate limiting.
    /// </summary>
    [HttpPost("render")]
    [HttpPost("render/stateless")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenderStatelessPdf(IFormFile file, [FromForm] string? jsonData, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            throw new DomainValidationException("File is required and must not be empty.");
        }

        await using var stream = file.OpenReadStream();
        var command = new RenderStatelessDocumentCommand(stream, file.FileName, jsonData);
        var pdfStream = await renderStatelessUseCase.ExecuteAsync(command, ct);

        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }
}
