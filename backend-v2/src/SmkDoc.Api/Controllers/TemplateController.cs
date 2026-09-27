using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Application.UseCases.Templates;

using ValidateTemplatePayloadCommand = SmkDoc.Application.DTOs.Templates.ValidateTemplatePayloadCommand;
using ValidateTemplatePayloadResult = SmkDoc.Application.DTOs.Templates.ValidateTemplatePayloadResult;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Route("api/templates")]
public class TemplateController(
    TemplateManagementUseCase templateUseCase,
    TemplateValidateUseCase validateUseCase,
    FieldMappingUseCase mappingUseCase,
    PreviewMappingUseCase previewMappingUseCase,
    TemplateDatasetUseCase templateDatasetUseCase,
    TemplateDraftUseCase draftUseCase,
    IHtmlStudioUseCase htmlStudioUseCase,
    IHtmlPersistenceUseCase htmlPersistenceUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var templates = await templateUseCase.ListTemplatesAsync(ct);
        return Ok(new ApiResponse<IEnumerable<TemplateDto>>(templates));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromForm] string name, [FromForm] string slug, [FromForm] string? category, IFormFile? file, CancellationToken ct)
    {
        Stream? stream = null;
        string? fileName = null;
        if (file != null && file.Length > 0)
        {
            var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            stream = ms;
            fileName = file.FileName;
        }

        var request = new CreateTemplateCommand(name, slug, category, null);
        var created = await templateUseCase.CreateTemplateAsync(request, stream, fileName, ct);
        return Ok(new ApiResponse<object>(new { id = created.Id, slug = created.Slug }));
    }

    [HttpGet("{id:guid}/html")]
    public async Task<IActionResult> GetHtml([FromRoute] Guid id, CancellationToken ct)
    {
        string html = await htmlStudioUseCase.GetHtmlSourceAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/studio")]
    [ProducesResponseType(typeof(ApiResponse<TemplateStudioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudioBundle([FromRoute] Guid id, CancellationToken ct)
    {
        var bundle = await htmlStudioUseCase.GetStudioBundleAsync(id, ct);
        return Ok(new ApiResponse<TemplateStudioDto>(bundle));
    }

    [HttpGet("{id:guid}/schema")]
    [ProducesResponseType(typeof(ApiResponse<TemplateSchemaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchema([FromRoute] Guid id, CancellationToken ct)
    {
        var schemaDto = await htmlStudioUseCase.GetTemplateSchemaAsync(id, ct);
        return Ok(new ApiResponse<TemplateSchemaDto>(schemaDto));
    }


    [HttpPut("{id:guid}/html")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveHtml([FromRoute] Guid id, [FromBody] SaveTemplateHtmlCommand request, CancellationToken ct)
    {
        int newVersion = await htmlPersistenceUseCase.SaveHtmlVersionAsync(id, request, ct);
        return Ok(new ApiResponse<object>(new { version = newVersion }));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMetadata([FromRoute] Guid id, [FromBody] UpdateTemplateMetadataCommand request, CancellationToken ct)
    {
        await templateUseCase.UpdateMetadataAsync(id, request, ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        await templateUseCase.DeactivateTemplateAsync(id, ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }

    [HttpGet("{id:guid}/mappings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FieldMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var mappings = await mappingUseCase.GetMappingsByTemplateIdAsync(id, ct);
        return Ok(new ApiResponse<IEnumerable<FieldMappingDto>>(mappings));
    }

    [HttpPut("{id:guid}/mappings")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveMappings([FromRoute] Guid id, [FromBody] List<SaveFieldMappingItemDto> mappings, CancellationToken ct)
    {
        await mappingUseCase.SaveMappingsAsync(id, mappings, ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }

    [HttpPost("{id:guid}/mappings/preview")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewMappings([FromRoute] Guid id, [FromBody] PreviewMappingsQuery request, CancellationToken ct)
    {
        byte[] pdfBytes = await previewMappingUseCase.ExecuteAsync(id, request.SampleData, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(ApiResponse<TemplateValidationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate([FromRoute] Guid id, [FromBody] SaveTemplateHtmlCommand request, CancellationToken ct)
    {
        var result = await validateUseCase.ValidateHtmlAsync(request.Html, ct);
        return Ok(new ApiResponse<TemplateValidationResultDto>(result));
    }

    /// <summary>
    /// Dry-run schema validation for an incoming payload against a published template's schema contract.
    /// Multi-tenant scoped. Zero side-effects (no DB write, no MinIO upload, no rendering).
    /// </summary>
    [HttpPost("{slug}/validate")]
    [HttpPost("{slug}/validate-payload")]
    [ProducesResponseType(typeof(ApiResponse<ValidateTemplatePayloadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidatePayload(
        [FromRoute] string slug,
        [FromBody] System.Text.Json.JsonElement data,
        [FromServices] ValidateTemplatePayloadUseCase payloadValidator,
        CancellationToken ct)
    {
        if (data.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
        {
            return BadRequest(new ApiResponse<object>(new { error = "Request payload body cannot be null or empty." }));
        }

        var result = await payloadValidator.ExecuteAsync(new ValidateTemplatePayloadCommand(slug, data), ct);
        return Ok(new ApiResponse<ValidateTemplatePayloadResult>(result));
    }

    // ── Draft Upload Pipeline ─────────────────────────────────────────────────

    /// <summary>Step 1 — Parse file, cache in RAM, return draftId + placeholders. No DB/MinIO write.</summary>
    [HttpPost("draft/parse")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ParseDraft(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var stream = file.OpenReadStream();
        var result = await draftUseCase.ParseAsync(stream, file.FileName, ct);
        return Ok(new ApiResponse<object>(new { draftId = result.DraftId, placeholders = result.Placeholders }));
    }

    /// <summary>Step 2 — Render preview PDF from cached draft (pure, no side-effects). Returns 410 if draft expired.</summary>
    [HttpPost("draft/{draftId}/preview")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewDraft([FromRoute] string draftId, [FromBody] PreviewDraftQuery request, CancellationToken ct)
    {
        byte[] pdfBytes = await draftUseCase.PreviewAsync(draftId, request.DataJson, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>Step 3 — Commit draft: upload to MinIO + write DB atomically. Returns 410 if draft expired.</summary>
    [HttpPost("draft/{draftId}/commit")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CommitDraft([FromRoute] string draftId, [FromBody] CommitDraftCommand request, CancellationToken ct)
    {
        var templateId = await draftUseCase.CommitAsync(draftId, request, ct);
        return Ok(new ApiResponse<object>(new { templateId }));
    }

    // ── Stateless scan ────────────────────────────────────────────────────────

    /// <summary>Stateless — scan placeholder fields from an uploaded file without creating a template.</summary>
    [HttpPost("scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ScanFieldsStateless(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = file.OpenReadStream();
        var placeholders = await templateUseCase.ScanPlaceholdersFromStreamAsync(stream, ext, ct);
        return Ok(new ApiResponse<object>(new { placeholders }));
    }

    [HttpGet("{id:guid}/scan-fields")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ScanFields([FromRoute] Guid id, CancellationToken ct)
    {
        var placeholders = await templateUseCase.ScanPlaceholdersAsync(id, ct);
        return Ok(new ApiResponse<object>(new { placeholders }));
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var versions = await templateUseCase.ListVersionsAsync(id, ct);
        return Ok(new ApiResponse<object>(new { versions }));
    }

    [HttpPost("{id:guid}/rollback/{version:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RollbackVersion([FromRoute] Guid id, [FromRoute] int version, CancellationToken ct)
    {
        int newVersion = await templateUseCase.RollbackVersionAsync(id, version, ct);
        return Ok(new ApiResponse<object>(new { version = newVersion }));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await templateUseCase.DownloadTemplateAsync(id, ct);
        return File(stream, contentType, fileName);
    }

    [HttpGet("{id:guid}/datasets")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatasets([FromRoute] Guid id, CancellationToken ct)
    {
        var datasets = await templateDatasetUseCase.GetByTemplateIdAsync(id, ct);
        return Ok(new ApiResponse<object>(new { datasets }));
    }

    [HttpPut("{id:guid}/datasets")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDatasets([FromRoute] Guid id, [FromBody] List<SaveTemplateDatasetItemDto> items, CancellationToken ct)
    {
        await templateDatasetUseCase.SaveAsync(id, items, ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }
}
