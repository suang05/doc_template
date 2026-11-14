using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Document artifact downloads and pre-signed URL generation with expiration governance.
/// Supports deterministic JSON URL response and VPC-safe direct streaming.
/// </summary>
[ApiController]
[Route("api/v1/generation-logs")]
[Authorize(Policy = AuthorizationPolicies.CanGenerateDocuments)]
public sealed class DocumentArtifactController(
    IUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto> getLogDownloadUrlUseCase,
    IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult> downloadLogDocumentUseCase) : ControllerBase
{
    /// <summary>Get fresh pre-signed download URL with 1-hour expiration window.</summary>
    [HttpGet("{logId:guid}/artifact/url")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl([FromRoute] Guid logId, CancellationToken ct)
    {
        var query = new GetLogDownloadUrlQuery(logId);
        var result = await getLogDownloadUrlUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DownloadUrlResultDto>(result));
    }

    /// <summary>Stream document binary file directly through server pass-through buffer.</summary>
    [HttpGet("{logId:guid}/artifact/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile([FromRoute] Guid logId, CancellationToken ct)
    {
        var query = new DownloadGenerationLogDocumentQuery(logId);
        var result = await downloadLogDocumentUseCase.ExecuteAsync(query, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>Transitional dual-mode download for backward compatibility with legacy clients.</summary>
    [HttpGet("{logId:guid}/artifact")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return await GetDownloadUrl(logId, ct);
        return await DownloadFile(logId, ct);
    }
}

