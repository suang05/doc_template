using System.Diagnostics;
using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.Documents;

public class GenerateDocumentUseCase
{
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IRepository<FieldMapping> _mappingRepo;
    private readonly IRepository<TemplateDataset> _tdRepo;
    private readonly IRepository<Dataset> _datasetRepo;
    private readonly IRepository<DataConnection> _connectionRepo;
    private readonly IRepository<GenerationLog> _logRepo;
    private readonly IRepository<Document> _documentRepo;
    private readonly IRepository<DocumentVersion> _docVersionRepo;
    private readonly IStorageService _storageService;
    private readonly IEnumerable<IRenderEngine> _engines;
    private readonly IExecutionContext _executionContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator;
    private readonly IDataProtectionService _dataProtection;
    private readonly IJsonSchemaValidationService _schemaValidation;

    public GenerateDocumentUseCase(
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IRepository<FieldMapping> mappingRepo,
        IRepository<TemplateDataset> tdRepo,
        IRepository<Dataset> datasetRepo,
        IRepository<DataConnection> connectionRepo,
        IRepository<GenerationLog> logRepo,
        IRepository<Document> documentRepo,
        IRepository<DocumentVersion> docVersionRepo,
        IStorageService storageService,
        IEnumerable<IRenderEngine> engines,
        IExecutionContext executionContext,
        IUnitOfWork unitOfWork,
        IFieldMappingApplicatorService fieldMappingApplicator,
        IDataProtectionService dataProtection,
        IJsonSchemaValidationService schemaValidation)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _mappingRepo = mappingRepo;
        _tdRepo = tdRepo;
        _datasetRepo = datasetRepo;
        _connectionRepo = connectionRepo;
        _logRepo = logRepo;
        _documentRepo = documentRepo;
        _docVersionRepo = docVersionRepo;
        _storageService = storageService;
        _engines = engines;
        _executionContext = executionContext;
        _unitOfWork = unitOfWork;
        _fieldMappingApplicator = fieldMappingApplicator;
        _dataProtection = dataProtection;
        _schemaValidation = schemaValidation;
    }

    public async Task<GenerateDocumentResponse> ExecuteAsync(
        string slug, GenerateDocumentRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var template = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (template == null || !template.IsActive)
            throw new KeyNotFoundException($"Template '{slug}' not found or inactive.");

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{slug}' has no published version.");

        var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version for template '{slug}' not found.");

        var outputFormat = request.Output.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _      => OutputFormat.Pdf
        };

        var engineType = currentVersion.GetRenderEngineType();

        var engine = _engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new InvalidOperationException($"No render engine registered for engine type '{engineType}'.");

        using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);

        var mappings = await _mappingRepo.ListAsync(m => m.TemplateId == template.Id, ct) ?? [];
        string dataJson;
        if (mappings.Count > 0)
        {
            var aliasMap = await DatasetAliasMapBuilder.BuildAsync(
                template.Id, _tdRepo, _datasetRepo, _connectionRepo, _dataProtection, ct);
            dataJson = await _fieldMappingApplicator.ApplyAsync(request.Data, mappings, aliasMap);
        }
        else
        {
            dataJson = request.Data.ValueKind != JsonValueKind.Undefined ? request.Data.GetRawText() : "{}";
        }

        // --- Schema Validation Gate (Draft-07) ---
        if (!request.SkipValidation && !string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            var validationResult = _schemaValidation.Validate(currentVersion.DataSchema, dataJson);
            if (!validationResult.IsValid)
            {
                // Log VALIDATION_FAILED as a Legal Audit Trail entry (no output artifact).
                sw.Stop();
                var failLog = new GenerationLog
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    TemplateVersionId = currentVersion.Id,
                    ApiKeyId = _executionContext.ApiKeyId,
                    CallerApp = _executionContext.CallerApp,
                    TriggerSource = "api",
                    InputData = dataJson,
                    OutputKey = null,
                    OutputFormat = request.Output,
                    FileSizeBytes = 0,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Status = "VALIDATION_FAILED",
                    ErrorMsg = $"{validationResult.Errors.Count} schema violation(s) detected."
                };
                await _logRepo.AddAsync(failLog, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                throw new SchemaValidationException(
                    slug, currentVersion.Version, validationResult.Errors);
            }
        }

        byte[] outputBytes = await engine.RenderAsync(templateStream, dataJson, outputFormat, ct);

        string ext = outputFormat switch
        {
            OutputFormat.Docx => "docx",
            OutputFormat.Xlsx => "xlsx",
            _                 => "pdf"
        };
        string contentType = outputFormat switch
        {
            OutputFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OutputFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _                 => "application/pdf"
        };

        var generationId = Guid.NewGuid();
        string outputKey = $"outputs/{DateTime.UtcNow:yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        using (var outputStream = new MemoryStream(outputBytes))
            await _storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

        var expiry = TimeSpan.FromHours(24);
        string downloadUrl = await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

        sw.Stop();

        var log = new GenerationLog
        {
            Id = generationId,
            TemplateId = template.Id,
            TemplateVersionId = currentVersion.Id,
            ApiKeyId = _executionContext.ApiKeyId,
            CallerApp = _executionContext.CallerApp,
            TriggerSource = "api",
            InputData = dataJson,
            OutputKey = outputKey,
            OutputFormat = ext,
            FileSizeBytes = outputBytes.LongLength,
            DurationMs = (int)sw.ElapsedMilliseconds,
            Status = "SUCCESS"
        };
        await _logRepo.AddAsync(log, ct);

        if (!string.IsNullOrWhiteSpace(request.DocumentRef))
        {
            var document = await _documentRepo.FirstOrDefaultAsync(
                d => d.DocumentRef == request.DocumentRef, ct);

            if (document is null)
            {
                document = new Document
                {
                    DocumentRef = request.DocumentRef,
                    TemplateId = template.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _documentRepo.AddAsync(document, ct);
            }

            const int maxRetries = 3;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                int currentMax = await _docVersionRepo.MaxOrDefaultAsync(
                    v => v.DocumentId == document.Id, v => v.Version, 0, ct);

                var docVersion = new DocumentVersion
                {
                    DocumentId = document.Id,
                    Version = currentMax + 1,
                    TemplateVersionId = currentVersion.Id,
                    GenerationLogId = generationId,
                    ChangeNote = request.ChangeNote,
                    CreatedBy = _executionContext.CallerApp
                };

                try
                {
                    await _docVersionRepo.AddAsync(docVersion, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    break;
                }
                catch (Exception) when (retry < maxRetries - 1)
                {
                    _docVersionRepo.Remove(docVersion);
                }
            }
        }
        else
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new GenerateDocumentResponse(
            Url: downloadUrl,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiry),
            GenerationId: generationId,
            OutputFormat: ext
        );
    }
}
