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
[Authorize(Policy = SmkDocPolicies.CanGenerateDocuments)]
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
        var versions = await getVersionsUseCase.ExecuteAsync(new GetDocumentVersionsQuery(documentRef), ct);
        return Ok(new ApiResponse<IEnumerable<DocumentVersionDto>>(versions));
    }

    /// <summary>Download specific historical version of a legal document.</summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var result = await downloadVersionUseCase.ExecuteAsync(new DownloadDocumentVersionQuery(documentRef, version), ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }
}

