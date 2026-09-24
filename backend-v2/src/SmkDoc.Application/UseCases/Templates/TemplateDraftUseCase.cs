using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.UseCases.Templates;

/// <summary>
/// Orchestrates the 3-step draft upload pipeline:
///   1. ParseAsync  — scan placeholders, cache file in RAM (no DB/MinIO write)
///   2. PreviewAsync — render PDF from cache for live preview (pure, no side-effects)
///   3. CommitAsync  — upload to MinIO + write DB atomically, then clear cache
/// </summary>
public class TemplateDraftUseCase
{
    private readonly ITemplateScannerService _scanner;
    private readonly ITemplateDraftCache _draftCache;
    private readonly IEnumerable<IRenderEngine> _engines;
    private readonly IStorageService _storageService;
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IRepository<FieldMapping> _mappingRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Project>? _projectRepo;
    private readonly ISchemaInferenceService? _schemaInferenceService;

    public TemplateDraftUseCase(
        ITemplateScannerService scanner,
        ITemplateDraftCache draftCache,
        IEnumerable<IRenderEngine> engines,
        IStorageService storageService,
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IRepository<FieldMapping> mappingRepo,
        IUnitOfWork unitOfWork,
        IRepository<Project>? projectRepo = null,
        ISchemaInferenceService? schemaInferenceService = null)
    {
        _scanner                = scanner;
        _draftCache             = draftCache;
        _engines                = engines;
        _storageService         = storageService;
        _templateRepo           = templateRepo;
        _versionRepo            = versionRepo;
        _mappingRepo            = mappingRepo;
        _unitOfWork             = unitOfWork;
        _projectRepo            = projectRepo;
        _schemaInferenceService = schemaInferenceService;
    }

    // ── Step 1 ───────────────────────────────────────────────────────────────
    public async Task<ParseDraftResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await fileStream.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        using var scanStream = new MemoryStream(fileBytes);
        var placeholders = await _scanner.ScanPlaceholdersAsync(scanStream, ext, ct);

        var entry   = new TemplateDraftEntry(fileBytes, fileName, ext, placeholders);
        var draftId = await _draftCache.StoreAsync(entry, ct);

        return new ParseDraftResult(draftId, placeholders);
    }

    // ── Step 2 ───────────────────────────────────────────────────────────────
    public async Task<byte[]> PreviewAsync(string draftId, string dataJson, CancellationToken ct)
    {
        var draft = await _draftCache.GetAsync(draftId, ct)
            ?? throw new DraftExpiredException(draftId);

        var engine = Engine(draft.FileExtension)
            ?? throw new NotSupportedException($"No engine registered for '{draft.FileExtension}'");

        using var ms = new MemoryStream(draft.FileBytes);
        return await engine.RenderAsync(ms, dataJson, OutputFormat.Pdf, ct);
    }

    // ── Step 3 ───────────────────────────────────────────────────────────────
    public async Task<string> CommitAsync(string draftId, CommitDraftRequest request, CancellationToken ct)
    {
        var draft = await _draftCache.GetAsync(draftId, ct)
            ?? throw new DraftExpiredException(draftId);

        var format      = FormatFromExt(draft.FileExtension);
        var templateId  = Guid.NewGuid();
        var versionId   = Guid.NewGuid();
        var storageKey  = $"{templateId}/{versionId}{draft.FileExtension}";
        var contentType = ContentTypeFromExt(draft.FileExtension);

        // Upload to MinIO first; use the returned key as the canonical storage path
        string uploadedKey;
        using (var ms = new MemoryStream(draft.FileBytes))
            uploadedKey = await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, ms, contentType, ct);

        Guid targetProjectId = Guid.Empty;
        if (_projectRepo != null)
        {
            var defaultProj = await _projectRepo.FirstOrDefaultAsync(p => p.IsActive, ct)
                ?? await _projectRepo.FirstOrDefaultAsync(p => true, ct);
            if (defaultProj != null) targetProjectId = defaultProj.Id;
        }

        try
        {
            var template = new Template
            {
                Id               = templateId,
                ProjectId        = targetProjectId,
                Name             = request.Name,
                Slug             = request.Slug,
                Category         = request.Category,
                IsActive         = true,
                CurrentVersionId = null, // Set to null initially to break circular dependency in EF Core
            };
            var placeholderList = request.Mappings?.Select(m => m.Placeholder).ToList() ?? new List<string>();
            string? inferredSchema = _schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: request.Slug);
            string? samplePayload = _schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

            var version = new TemplateVersion
            {
                Id            = versionId,
                TemplateId    = templateId,
                Version       = 1,
                StorageKey    = uploadedKey,
                FileFormat    = format,
                Status        = TemplateVersionStatus.Published,
                CommitMessage = "Initial upload via draft pipeline",
                DataSchema    = inferredSchema,
                SamplePayload = samplePayload,
            };

            await _templateRepo.AddAsync(template, ct);
            await _versionRepo.AddAsync(version, ct);

            if (request.Mappings != null)
            {
                foreach (var m in request.Mappings)
                {
                await _mappingRepo.AddAsync(new FieldMapping
                {
                    Id             = Guid.NewGuid(),
                    TemplateId     = templateId,
                    Placeholder    = m.Placeholder,
                    SourcePath     = m.SourcePath,
                    Label          = m.Label,
                    Required       = m.Required,
                    DefaultValue   = m.DefaultValue,
                    Transform      = m.Transform,
                    SortOrder      = m.SortOrder,
                    DataSourceType = m.DataSourceType,
                    DatasetAlias   = m.DatasetAlias,
                    ResultPath     = m.ResultPath,
                    MathExpression = m.MathExpression,
                }, ct);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // Once both Template and TemplateVersion rows exist, link CurrentVersionId
            template.CurrentVersionId = versionId;
            _templateRepo.Update(template);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            // Best-effort rollback of MinIO upload — use CancellationToken.None so a cancelled ct
            // does not prevent cleanup, and swallow any secondary error to preserve the original.
            try { await _storageService.DeleteAsync(StorageBuckets.Templates, uploadedKey, CancellationToken.None); }
            catch { /* swallow; original exception re-thrown below */ }
            throw;
        }

        await _draftCache.RemoveAsync(draftId, ct);
        return templateId.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private IRenderEngine Engine(string ext)
    {
        var type = ext switch
        {
            ".xlsx" => RenderEngineType.Excel,
            ".docx" => RenderEngineType.Docx,
            _       => RenderEngineType.Html,
        };
        return _engines.FirstOrDefault(e => e.EngineType == type)
            ?? throw new InvalidOperationException($"No render engine registered for '{ext}'.");
    }

    private static TemplateFormat FormatFromExt(string ext) => ext switch
    {
        ".xlsx" => TemplateFormat.Xlsx,
        ".docx" => TemplateFormat.Docx,
        _       => TemplateFormat.Html,
    };

    private static string ContentTypeFromExt(string ext) => ext switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _       => "text/html; charset=utf-8",
    };
}

public class DraftExpiredException : Exception
{
    public DraftExpiredException(string draftId)
        : base($"Draft '{draftId}' has expired or does not exist. Please re-upload the file.") { }
}
