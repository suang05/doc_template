using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using System.Text.Json;

using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
[Route("api/documents")]
public class DocumentController(
    GenerateDocumentUseCase generateUseCase,
    PreviewDocumentUseCase previewUseCase,
    DocumentVersionUseCase versionUseCase,
    RenderStatelessDocumentUseCase renderStatelessUseCase) : ControllerBase
{
    /// <summary>
    /// Generate a document from template slug and input JSON data
    /// </summary>
    [HttpPost("generate/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentCommand request, CancellationToken ct)
    {
        var response = await generateUseCase.ExecuteAsync(slug, request, ct);
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
        [FromServices] ValidateTemplatePayloadUseCase payloadValidator,
        CancellationToken ct)
    {
        var command = new ValidateTemplatePayloadCommand(slug, data);
        var result = await payloadValidator.ExecuteAsync(command, ct);
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
    public async Task<IActionResult> Preview([FromRoute] string? slug, [FromBody] PreviewDocumentQuery request, CancellationToken ct)
    {
        byte[] pdfBytes = await previewUseCase.ExecuteAsync(slug, request, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Get legal document version history
    /// </summary>
    [HttpGet("{documentRef}/versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] string documentRef, CancellationToken ct)
    {
        var versions = await versionUseCase.GetVersionsByRefAsync(documentRef, ct);
        return Ok(new ApiResponse<IEnumerable<DocumentVersionDto>>(versions));
    }

    /// <summary>
    /// Download specific historical version of a document
    /// </summary>
    [HttpGet("{documentRef}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersion([FromRoute] string documentRef, [FromRoute] int version, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await versionUseCase.DownloadVersionAsync(documentRef, version, ct);
        return File(stream, contentType, fileName);
    }

    [HttpGet("download/{logId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadByLogId([FromRoute] Guid logId, CancellationToken ct)
    {
        var url = await versionUseCase.GetDownloadUrlByLogIdAsync(logId, ct);
        return Ok(new ApiResponse<object>(new { url, expiresInSeconds = 3600 }));
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
        byte[] pdfBytes = await renderStatelessUseCase.ExecuteAsync(stream, file.FileName, jsonData, ct);
        
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }
}
