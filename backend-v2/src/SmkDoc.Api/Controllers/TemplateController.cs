using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Application.UseCases.Templates;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController : ControllerBase
{
    private readonly TemplateManagementUseCase _templateUseCase;
    private readonly TemplateValidateUseCase _validateUseCase;
    private readonly FieldMappingUseCase _mappingUseCase;
    private readonly PreviewMappingUseCase _previewMappingUseCase;
    private readonly TemplateDatasetUseCase _templateDatasetUseCase;
    private readonly TemplateDraftUseCase _draftUseCase;
    private readonly IHtmlStudioUseCase _htmlStudioUseCase;
    private readonly IHtmlPersistenceUseCase _htmlPersistenceUseCase;

    public TemplateController(
        TemplateManagementUseCase templateUseCase,
        TemplateValidateUseCase validateUseCase,
        FieldMappingUseCase mappingUseCase,
        PreviewMappingUseCase previewMappingUseCase,
        TemplateDatasetUseCase templateDatasetUseCase,
        TemplateDraftUseCase draftUseCase,
        IHtmlStudioUseCase htmlStudioUseCase,
        IHtmlPersistenceUseCase htmlPersistenceUseCase)
    {
        _templateUseCase = templateUseCase;
        _validateUseCase = validateUseCase;
        _mappingUseCase = mappingUseCase;
        _previewMappingUseCase = previewMappingUseCase;
        _templateDatasetUseCase = templateDatasetUseCase;
        _draftUseCase = draftUseCase;
        _htmlStudioUseCase = htmlStudioUseCase;
        _htmlPersistenceUseCase = htmlPersistenceUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var templates = await _templateUseCase.ListTemplatesAsync(ct);
        return Ok(new { data = templates });
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

        var request = new CreateTemplateRequest(name, slug, category, null);
        var created = await _templateUseCase.CreateTemplateAsync(request, stream, fileName, ct);
        return Ok(new { id = created.Id, slug = created.Slug });
    }

    [HttpGet("{id:guid}/html")]
    public async Task<IActionResult> GetHtml([FromRoute] Guid id, CancellationToken ct)
    {
        string html = await _htmlStudioUseCase.GetHtmlSourceAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/studio")]
    public async Task<IActionResult> GetStudioBundle([FromRoute] Guid id, CancellationToken ct)
    {
        var bundle = await _htmlStudioUseCase.GetStudioBundleAsync(id, ct);
        return Ok(bundle);
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<IActionResult> GetSchema([FromRoute] Guid id, CancellationToken ct)
    {
        var schemaDto = await _htmlStudioUseCase.GetTemplateSchemaAsync(id, ct);
        return Ok(schemaDto);
    }


    [HttpPut("{id:guid}/html")]
    public async Task<IActionResult> SaveHtml([FromRoute] Guid id, [FromBody] SaveTemplateHtmlRequest request, CancellationToken ct)
    {
        int newVersion = await _htmlPersistenceUseCase.SaveHtmlVersionAsync(id, request, ct);
        return Ok(new { version = newVersion });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata([FromRoute] Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        await _templateUseCase.UpdateMetadataAsync(id, request, ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        await _templateUseCase.DeactivateTemplateAsync(id, ct);
        return Ok(new { success = true });
    }

    [HttpGet("{id:guid}/mappings")]
    public async Task<IActionResult> GetMappings([FromRoute] Guid id, CancellationToken ct)
    {
        var mappings = await _mappingUseCase.GetMappingsByTemplateIdAsync(id, ct);
        return Ok(new { mappings });
    }

    [HttpPut("{id:guid}/mappings")]
    public async Task<IActionResult> SaveMappings([FromRoute] Guid id, [FromBody] List<SaveFieldMappingItem> mappings, CancellationToken ct)
    {
        await _mappingUseCase.SaveMappingsAsync(id, mappings, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/mappings/preview")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewMappings([FromRoute] Guid id, [FromBody] PreviewMappingsRequest request, CancellationToken ct)
    {
        byte[] pdfBytes = await _previewMappingUseCase.ExecuteAsync(id, request.SampleData, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfBytes, "application/pdf");
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> Validate([FromRoute] Guid id, [FromBody] SaveTemplateHtmlRequest request, CancellationToken ct)
    {
        var result = await _validateUseCase.ValidateHtmlAsync(request.Html, ct);
        return Ok(result);
    }

    // ── Draft Upload Pipeline ─────────────────────────────────────────────────

    /// <summary>Step 1 — Parse file, cache in RAM, return draftId + placeholders. No DB/MinIO write.</summary>
    [HttpPost("draft/parse")]
    public async Task<IActionResult> ParseDraft(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var stream = file.OpenReadStream();
        var result = await _draftUseCase.ParseAsync(stream, file.FileName, ct);
        return Ok(new { draftId = result.DraftId, placeholders = result.Placeholders });
    }

    /// <summary>Step 2 — Render preview PDF from cached draft (pure, no side-effects). Returns 410 if draft expired.</summary>
    [HttpPost("draft/{draftId}/preview")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewDraft([FromRoute] string draftId, [FromBody] PreviewDraftRequest request, CancellationToken ct)
    {
        try
        {
            byte[] pdfBytes = await _draftUseCase.PreviewAsync(draftId, request.DataJson, ct);
            Response.Headers.ContentDisposition = "inline";
            return File(pdfBytes, "application/pdf");
        }
        catch (DraftExpiredException ex)
        {
            return StatusCode(410, new { error = ex.Message });
        }
    }

    /// <summary>Step 3 — Commit draft: upload to MinIO + write DB atomically. Returns 410 if draft expired.</summary>
    [HttpPost("draft/{draftId}/commit")]
    public async Task<IActionResult> CommitDraft([FromRoute] string draftId, [FromBody] CommitDraftRequest request, CancellationToken ct)
    {
        try
        {
            var templateId = await _draftUseCase.CommitAsync(draftId, request, ct);
            return Ok(new { templateId });
        }
        catch (DraftExpiredException ex)
        {
            return StatusCode(410, new { error = ex.Message });
        }
    }

    // ── Stateless scan ────────────────────────────────────────────────────────

    /// <summary>Stateless — scan placeholder fields from an uploaded file without creating a template.</summary>
    [HttpPost("scan-fields")]
    public async Task<IActionResult> ScanFieldsStateless(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = file.OpenReadStream();
        var placeholders = await _templateUseCase.ScanPlaceholdersFromStreamAsync(stream, ext, ct);
        return Ok(new { placeholders });
    }

    [HttpGet("{id:guid}/scan-fields")]
    public async Task<IActionResult> ScanFields([FromRoute] Guid id, CancellationToken ct)
    {
        var placeholders = await _templateUseCase.ScanPlaceholdersAsync(id, ct);
        return Ok(new { placeholders });
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var versions = await _templateUseCase.ListVersionsAsync(id, ct);
        return Ok(new { versions });
    }

    [HttpPost("{id:guid}/rollback/{version:int}")]
    public async Task<IActionResult> RollbackVersion([FromRoute] Guid id, [FromRoute] int version, CancellationToken ct)
    {
        int newVersion = await _templateUseCase.RollbackVersionAsync(id, version, ct);
        return Ok(new { version = newVersion });
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _templateUseCase.DownloadTemplateAsync(id, ct);
        return File(stream, contentType, fileName);
    }

    [HttpGet("{id:guid}/datasets")]
    public async Task<IActionResult> GetDatasets([FromRoute] Guid id, CancellationToken ct)
    {
        var datasets = await _templateDatasetUseCase.GetByTemplateIdAsync(id, ct);
        return Ok(new { datasets });
    }

    [HttpPut("{id:guid}/datasets")]
    public async Task<IActionResult> SaveDatasets([FromRoute] Guid id, [FromBody] List<SaveTemplateDatasetItem> items, CancellationToken ct)
    {
        await _templateDatasetUseCase.SaveAsync(id, items, ct);
        return Ok(new { success = true });
    }
}
