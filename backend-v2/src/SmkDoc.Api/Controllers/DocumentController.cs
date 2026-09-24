using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.UseCases.Documents;
using System.Text.Json;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentController : ControllerBase
{
    private readonly GenerateDocumentUseCase _generateUseCase;
    private readonly PreviewDocumentUseCase _previewUseCase;
    private readonly DocumentVersionUseCase _versionUseCase;
    private readonly RenderStatelessDocumentUseCase _renderStatelessUseCase;
    private readonly ValidatePayloadUseCase _validateUseCase;

    public DocumentController(
        GenerateDocumentUseCase generateUseCase,
        PreviewDocumentUseCase previewUseCase,
        DocumentVersionUseCase versionUseCase,
        RenderStatelessDocumentUseCase renderStatelessUseCase,
        ValidatePayloadUseCase validateUseCase)
    {
        _generateUseCase      = generateUseCase;
        _previewUseCase       = previewUseCase;
        _versionUseCase       = versionUseCase;
        _renderStatelessUseCase = renderStatelessUseCase;
        _validateUseCase      = validateUseCase;
    }

    /// <summary>
    /// Generate a document from template slug and input JSON data
    /// </summary>
    [HttpPost("generate/{slug}")]
    [ProducesResponseType(typeof(GenerateDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var response = await _generateUseCase.ExecuteAsync(slug, request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Pre-flight schema validation — checks payload against the template's Draft-07 schema
    /// without generating any document. Zero side-effects (no DB writes, no MinIO uploads).
    /// </summary>
    [HttpPost("validate/{slug}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidatePayload(
        [FromRoute] string slug,
        [FromBody] JsonElement data,
        CancellationToken ct)
    {
        var result = await _validateUseCase.ExecuteAsync(slug, data, ct);

        var response = new
        {
            valid          = result.IsValid,
            templateSlug   = result.TemplateSlug,
            schemaVersion  = result.SchemaVersion,
            errors         = result.Errors.Select(e => new
            {
                path    = e.PropertyPath,
                message = e.Message,
                rule    = e.SchemaRule
            })
        };

        return result.IsValid ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Ephemeral live preview for Monaco Editor / Form with zero side-effects
    /// </summary>
    [HttpPost("preview")]
    [HttpPost("preview/{slug}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview([FromRoute] string? slug, [FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        byte[] pdfBytes = await _previewUseCase.ExecuteAsync(slug, request, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Get legal document version history
    /// </summary>
    [HttpGet("{documentRef}/versions")]
    [ProducesResponseType(typeof(List<DocumentVersionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] string documentRef, CancellationToken ct)
    {
        var versions = await _versionUseCase.GetVersionsByRefAsync(documentRef, ct);
        return Ok(new { versions });
    }

    /// <summary>
    /// Download specific historical version of a document
    /// </summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _versionUseCase.DownloadVersionAsync(documentRef, version, ct);
        return File(stream, contentType, fileName);
    }

    [HttpGet("download/{logId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        var url = await _versionUseCase.GetDownloadUrlByLogIdAsync(logId, ct);
        return Ok(new { url, expiresInSeconds = 3600 });
    }

    /// <summary>
    /// Render any document to PDF directly from an uploaded file stream and JSON payload.
    /// </summary>
    [HttpPost("render")]
    [HttpPost("render/stateless")]
    [Produces("application/pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RenderStatelessPdf(IFormFile file, [FromForm] string? jsonData, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        using var stream = file.OpenReadStream();
        byte[] pdfBytes = await _renderStatelessUseCase.ExecuteAsync(stream, file.FileName, jsonData, ct);
        
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }
}
