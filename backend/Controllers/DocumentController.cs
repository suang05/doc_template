using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;
using SmkDocServer.Infrastructure.Services.Storage;

namespace SmkDocServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IGenerationLogService _logService;
    private readonly IMinioStorageService _minioStorage;
    private readonly MinioSettings _minioSettings;
    private readonly IPdfConverter _pdfConverter;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentService documentService,
        IGenerationLogService logService,
        IMinioStorageService minioStorage,
        IOptions<MinioSettings> minioOptions,
        IPdfConverter pdfConverter,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logService      = logService;
        _minioStorage    = minioStorage;
        _minioSettings   = minioOptions.Value;
        _pdfConverter    = pdfConverter;
        _logger          = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDocument([FromBody] DocumentGenerationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _documentService.GenerateAndStoreDocumentAsync(request);
            return Ok(result);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Template not found.");
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating document.");
            return StatusCode(500, new { Message = "An internal error occurred." });
        }
    }

    /// <summary>
    /// Generates a live PDF preview and streams bytes directly to the caller.
    /// IMPORTANT: This endpoint NEVER saves to MinIO or writes audit logs.
    /// Use for UI preview only — not for production document generation.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> PreviewDocument([FromBody] DocumentGenerationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            byte[] pdfBytes = await _documentService.PreviewDocumentAsync(request);

            // Stream PDF inline — no download trigger, no side effects (Rule 12)
            Response.Headers.Append("Content-Disposition", "inline");
            return File(pdfBytes, "application/pdf");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "[Preview] Template not found.");
            return NotFound(new { ex.Message });
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "[Preview] Format not supported.");
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Preview] Error generating preview.");
            return StatusCode(500, new { Message = "An internal error occurred during preview generation." });
        }
    }

    /// <summary>
    /// Converts raw HTML directly to PDF via Gotenberg Chromium.
    /// Zero side effects — no template lookup, no MinIO save, no audit log.
    /// </summary>
    [HttpPost("html-to-pdf")]
    public async Task<IActionResult> HtmlToPdf([FromBody] HtmlToPdfRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HtmlContent))
            return BadRequest(new { Message = "htmlContent is required." });

        try
        {
            var start    = DateTimeOffset.UtcNow;
            byte[] bytes = await _pdfConverter.ConvertHtmlWithLayoutAsync(
                request.HtmlContent,
                request.HeaderHtml,
                request.FooterHtml);
            var ms = (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds;

            Response.Headers.Append("X-Duration-Ms", ms.ToString());
            Response.Headers.Append("Content-Disposition", "inline");
            return File(bytes, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HtmlToPdf] Conversion failed.");
            return StatusCode(500, new { Message = "Failed to convert HTML to PDF." });
        }
    }

    /// <summary>
    /// Streams a generated PDF preview inline via the backend (for iframe — avoids browser→MinIO direct connection).
    /// objectName must match pattern: generated_*.pdf or preview_*.pdf (validated to prevent enumeration).
    /// </summary>
    [HttpGet("preview-file")]
    public async Task<IActionResult> PreviewFile([FromQuery] string obj)
    {
        if (string.IsNullOrWhiteSpace(obj) ||
            (!obj.StartsWith("preview_", StringComparison.OrdinalIgnoreCase) &&
             !obj.StartsWith("generated_", StringComparison.OrdinalIgnoreCase)) ||
            !obj.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            obj.Contains('/') || obj.Contains('\\'))
        {
            return BadRequest(new { Message = "Invalid preview object name." });
        }

        try
        {
            var stream = await _minioStorage.DownloadFileAsync(_minioSettings.DocumentBucket, obj);
            Response.Headers.Append("Content-Disposition", "inline");
            return File(stream, "application/pdf");
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return NotFound(new { Message = "Preview file is no longer available." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming preview file {Obj}", obj);
            return StatusCode(500, new { Message = "Failed to retrieve preview." });
        }
    }

    /// <summary>
    /// Streams a previously generated document from MinIO via the backend.
    /// Avoids requiring the browser to reach MinIO directly (Docker networking issue on Windows).
    /// </summary>
    [HttpGet("download/{logId:guid}")]
    public async Task<IActionResult> DownloadDocument(Guid logId)
    {
        try
        {
            var log = await _logService.GetByIdAsync(logId);
            if (log == null || string.IsNullOrEmpty(log.OutputFileName))
                return NotFound(new { Message = "Document not found or has expired." });

            var stream = await _minioStorage.DownloadFileAsync(_minioSettings.DocumentBucket, log.OutputFileName);

            string ext = Path.GetExtension(log.OutputFileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".pdf"  => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _       => "application/octet-stream"
            };

            // Build a friendly filename: <templateName>_<date><ext>
            string baseName = Path.GetFileNameWithoutExtension(log.TemplateName);
            string date     = log.CreatedAt.ToString("yyyyMMdd_HHmm");
            string fileName = $"{baseName}_{date}{ext}";

            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            return File(stream, contentType, enableRangeProcessing: false);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return NotFound(new { Message = "The generated file is no longer available in storage." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming document for log {LogId}", logId);
            return StatusCode(500, new { Message = "Failed to retrieve the generated document." });
        }
    }
}

public record HtmlToPdfRequest(string HtmlContent, string? HeaderHtml = null, string? FooterHtml = null);
