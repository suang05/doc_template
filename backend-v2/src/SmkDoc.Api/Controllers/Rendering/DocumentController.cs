using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using System.Text.Json;

using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Core document rendering, generation, validation, and version retrieval endpoints.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware). Ephemeral preview is stateless.
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Route("api/documents")]
public class DocumentController(
    GenerateDocumentUseCase generateUseCase,
    PreviewDocumentUseCase previewUseCase,
    GetDocumentVersionsUseCase getVersionsUseCase,
    DownloadDocumentVersionUseCase downloadVersionUseCase,
    GetLogDownloadUrlUseCase getLogDownloadUrlUseCase,
    RenderStatelessDocumentUseCase renderStatelessUseCase,
    ValidateTemplatePayloadUseCase validatePayloadUseCase) : ControllerBase
{
    /// <summary>
    /// Generate a document from template slug and input JSON data
    /// </summary>
    [HttpPost("generate/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var command = new GenerateDocumentCommand(request.Data, request.Output, request.DocumentRef, request.ChangeNote, request.SkipValidation);
        var response = await generateUseCase.ExecuteAsync(slug, command, ct);
        return Ok(new ApiResponse<GenerateDocumentResultDto>(response));
    }

    /// <summary>
    /// Pre-flight schema validation — checks payload against the template's Draft-07 schema
    /// without generating any document. Multi-tenant scoped. Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("validate/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<ValidateTemplatePayloadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ValidateTemplatePayloadResult>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidatePayload(
        [FromRoute] string slug,
        [FromBody] JsonElement data,
        CancellationToken ct)
    {
        var query = new ValidateTemplatePayloadQuery(slug, data);
        var result = await validatePayloadUseCase.ExecuteAsync(query, ct);
        return result.Valid 
            ? Ok(new ApiResponse<ValidateTemplatePayloadResult>(result)) 
            : BadRequest(new ApiResponse<ValidateTemplatePayloadResult>(result));
    }

    /// <summary>
    /// Ephemeral live preview for Monaco Editor / Form with zero side-effects
    /// </summary>
    [HttpPost("preview")]
    [HttpPost("preview/{slug}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview([FromRoute] string? slug, [FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Data, request.Html);
        var pdfStream = await previewUseCase.ExecuteStreamAsync(slug, query, ct);
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
        var versions = await getVersionsUseCase.ExecuteAsync(new GetDocumentVersionsQuery(documentRef), ct);
        return Ok(new ApiResponse<IEnumerable<DocumentVersionDto>>(versions));
    }

    /// <summary>
    /// Download specific historical version of a document
    /// </summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var result = await downloadVersionUseCase.ExecuteAsync(new DownloadDocumentVersionQuery(documentRef, version), ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>Get pre-signed download URL for a generated document.</summary>
    [HttpGet("download/{logId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DownloadUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        var url = await getLogDownloadUrlUseCase.ExecuteAsync(new GetLogDownloadUrlQuery(logId), ct);
        return Ok(new ApiResponse<DownloadUrlResponseDto>(new DownloadUrlResponseDto(url, 3600)));
    }

    /// <summary>
    /// Render any document to PDF directly from an uploaded file stream and JSON payload.
    /// </summary>
    [HttpPost("render")]
    [HttpPost("render/stateless")]
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

        var stream = file.OpenReadStream();
        var pdfStream = await renderStatelessUseCase.ExecuteStreamAsync(stream, file.FileName, jsonData, ct);
        
        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }
}
