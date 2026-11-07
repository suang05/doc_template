using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Legal document version history retrieval and historical document artifact downloads.
/// Scoped by Document Reference (e.g., invoice number, contract code) under tenant isolation.
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = AuthorizationPolicies.CanGenerateDocuments)]
public sealed class DocumentHistoryController(
    IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>> getVersionsUseCase,
    IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult> downloadVersionUseCase) : ControllerBase
{
    /// <summary>Get legal document version history by document reference.</summary>
    [HttpGet("{documentRef}/versions")]
    [HttpGet("{documentRef}/history")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] string documentRef, CancellationToken ct)
    {
        var query = new GetDocumentVersionsQuery(documentRef);
        var result = await getVersionsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<DocumentVersionDto>>(result));
    }

    /// <summary>Download specific historical version of a legal document.</summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var query = new DownloadDocumentVersionQuery(documentRef, version);
        var result = await downloadVersionUseCase.ExecuteAsync(query, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }
}

