using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Document artifact downloads and pre-signed URL generation with expiration governance.
/// Supports deterministic JSON URL response and VPC-safe direct streaming.
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = SmkDocPolicies.CanGenerateDocuments)]
public sealed class DocumentArtifactController(
    IUseCase<GetLogDownloadUrlQuery, string> getLogDownloadUrlUseCase,
    IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult> downloadLogDocumentUseCase) : ControllerBase
{
    /// <summary>Get fresh pre-signed download URL with 1-hour expiration window.</summary>
    [HttpGet("download/{logId:guid}/url")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUrl([FromRoute] Guid logId, CancellationToken ct)
    {
        var url = await getLogDownloadUrlUseCase.ExecuteAsync(new GetLogDownloadUrlQuery(logId), ct);
        return Ok(new ApiResponse<DownloadUrlResponseDto>(new DownloadUrlResponseDto(url, 3600)));
    }

    /// <summary>Stream document binary file directly through server pass-through buffer.</summary>
    [HttpGet("download/{logId:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile([FromRoute] Guid logId, CancellationToken ct)
    {
        var result = await downloadLogDocumentUseCase.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId), ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>Transitional dual-mode download for backward compatibility with legacy clients.</summary>
    [HttpGet("download/{logId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return await GetDownloadUrl(logId, ct);
        return await DownloadFile(logId, ct);
    }
}

