using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.CancelDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.CreateDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RetryDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobResult;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobStatus;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobItems;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Canonical Pipeline C asynchronous batch and single-document job orchestration endpoints.
/// Supports durable PostgreSQL lease claiming, keyset cursor pagination, streaming ZIP/document results,
/// idempotent creation, and optimistic-concurrency state transitions (<c>:cancel</c>, <c>:retry</c>).
/// </summary>
[ApiController]
[Route("api/v1/document-jobs")]
[Authorize(Policy = AuthorizationPolicies.CanGenerateDocuments)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DocumentJobsController(
    IUseCase<CreateDocumentJobCommand, DocumentJobResultDto> createJobUseCase,
    IUseCase<ListDocumentJobsQuery, DocumentJobListResult> listJobsUseCase,
    IUseCase<GetDocumentJobStatusQuery, DocumentJobResultDto> getJobStatusUseCase,
    IUseCase<GetDocumentJobResultQuery, DocumentJobFileResult> getJobResultUseCase,
    IUseCase<ListDocumentJobItemsQuery, IReadOnlyList<DocumentJobItemDto>> listJobItemsUseCase,
    IUseCase<CancelDocumentJobCommand, DocumentJobResultDto> cancelJobUseCase,
    IUseCase<RetryDocumentJobCommand, DocumentJobResultDto> retryJobUseCase) : ControllerBase
{
    /// <summary>Create an asynchronous Pipeline C document rendering job (single or batch).</summary>
    [HttpPost]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobResultDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateJob(
        [FromBody] CreateDocumentJobRequest request,
        [FromHeader(Name = IdempotencyFilter.HeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var items = ResolveJobItems(request);

        var command = new CreateDocumentJobCommand(
            request.Slug,
            items,
            request.OutputFormat,
            request.WebhookUrl,
            idempotencyKey,
            RequestFingerprint: null,
            MaxAttempts: request.MaxAttempts);

        var result = await createJobUseCase.ExecuteAsync(command, ct);
        Response.Headers.RetryAfter = "2";

        return AcceptedAtAction(
            nameof(GetJobById),
            new { jobId = result.JobId },
            new ApiResponse<DocumentJobResultDto>(result));
    }

    /// <summary>List document jobs for the active project using keyset cursor pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobListResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var query = new ListDocumentJobsQuery(status, cursor, limit);
        var result = await listJobsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DocumentJobListResult>(result));
    }

    /// <summary>Get the current status and version metadata of a document job.</summary>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobById([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new GetDocumentJobStatusQuery(jobId);
        var result = await getJobStatusUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DocumentJobResultDto>(result));
    }

    /// <summary>Stream the rendered document or batch ZIP bundle for a completed job.</summary>
    [HttpGet("{jobId:guid}/result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadResult([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new GetDocumentJobResultQuery(jobId);
        var result = await getJobResultUseCase.ExecuteAsync(query, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>List per-item execution status entries belonging to a document job.</summary>
    [HttpGet("{jobId:guid}/items")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentJobItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListJobItems([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new ListDocumentJobItemsQuery(jobId);
        var result = await listJobItemsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IReadOnlyList<DocumentJobItemDto>>(result));
    }

    /// <summary>Request cancellation of a queued or running document job with optimistic concurrency.</summary>
    [HttpPost("{jobId:guid}:cancel")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> CancelJob(
        [FromRoute] Guid jobId,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new CancelDocumentJobCommand(jobId, expectedVersion);
        var result = await cancelJobUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<DocumentJobResultDto>(result));
    }

    /// <summary>Re-queue a failed or canceled document job for another attempt with optimistic concurrency.</summary>
    [HttpPost("{jobId:guid}:retry")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> RetryJob(
        [FromRoute] Guid jobId,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new RetryDocumentJobCommand(jobId, expectedVersion);
        var result = await retryJobUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<DocumentJobResultDto>(result));
    }

    private static DocumentJobItemInput[] ResolveJobItems(CreateDocumentJobRequest request)
    {
        if (request.Items is { Count: > 0 })
        {
            return [.. request.Items.Select(i => new DocumentJobItemInput(i.Payload, i.DocumentRef))];
        }

        if (request.Payload is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } singlePayload)
        {
            return [new DocumentJobItemInput(singlePayload, request.DocumentRef)];
        }

        return [];
    }
}
