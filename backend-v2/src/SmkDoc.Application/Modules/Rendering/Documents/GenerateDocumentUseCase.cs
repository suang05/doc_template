using System.Diagnostics;
using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Documents;

public sealed class GenerateDocumentUseCase(
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
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IRepository<FieldMapping> _mappingRepo = mappingRepo;
    private readonly IRepository<TemplateDataset> _tdRepo = tdRepo;
    private readonly IRepository<Dataset> _datasetRepo = datasetRepo;
    private readonly IRepository<DataConnection> _connectionRepo = connectionRepo;
    private readonly IRepository<GenerationLog> _logRepo = logRepo;
    private readonly IRepository<Document> _documentRepo = documentRepo;
    private readonly IRepository<DocumentVersion> _docVersionRepo = docVersionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IEnumerable<IRenderEngine> _engines = engines;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator = fieldMappingApplicator;
    private readonly IDataProtectionService _dataProtection = dataProtection;
    private readonly IJsonSchemaValidationService _schemaValidation = schemaValidation;

    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        string slug, GenerateDocumentCommand request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var template = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (template == null || !template.IsActive)
            throw new NotFoundException($"Template '{slug}' not found or inactive.");

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
                var failLog = new GenerationLog(
                    template.Id, 
                    currentVersion.Id, 
                    _executionContext.ApiKeyId, 
                    _executionContext.CallerApp, 
                    "api", 
                    dataJson, 
                    null, 
                    outputFormat, 
                    0, 
                    null, 
                    null, 
                    (int)sw.ElapsedMilliseconds, 
                    "VALIDATION_FAILED", 
                    $"{validationResult.Errors.Count} schema violation(s) detected.");
                await _logRepo.AddAsync(failLog, ct);
                await _unitOfWork.CommitAsync(ct);

                throw new SchemaValidationException(
                    slug, currentVersion.Version, validationResult.Errors);
            }
        }

        byte[] outputBytes = await engine.RenderAsync(templateStream, dataJson, outputFormat, ct);

        string ext = outputFormat.Extension;
        string contentType = outputFormat.MimeType;

        var generationId = Guid.CreateVersion7();
        string outputKey = $"outputs/{DateTime.UtcNow:yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        using (var outputStream = new MemoryStream(outputBytes))
            await _storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

        var expiry = TimeSpan.FromHours(24);
        string downloadUrl = await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

        sw.Stop();

        var log = new GenerationLog(
            template.Id, 
            currentVersion.Id, 
            _executionContext.ApiKeyId, 
            _executionContext.CallerApp, 
            "api", 
            dataJson, 
            outputKey, 
            outputFormat, 
            outputBytes.LongLength, 
            null, 
            null, 
            (int)sw.ElapsedMilliseconds, 
            "SUCCESS", 
            null)
        {
            Id = generationId
        };
        await _logRepo.AddAsync(log, ct);

        if (!string.IsNullOrWhiteSpace(request.DocumentRef))
        {
            var document = await _documentRepo.FirstOrDefaultAsync(
                d => d.DocumentRef == request.DocumentRef, ct);

            if (document is null)
            {
                document = new Document(request.DocumentRef, template.Id);
                await _documentRepo.AddAsync(document, ct);
            }

            const int maxRetries = 3;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                int currentMax = await _docVersionRepo.MaxOrDefaultAsync(
                    v => v.DocumentId == document.Id, v => v.Version, 0, ct);

                var docVersion = new DocumentVersion(
                    document.Id, 
                    currentMax + 1, 
                    currentVersion.Id, 
                    generationId, 
                    request.ChangeNote, 
                    _executionContext.CallerApp);

                try
                {
                    await _docVersionRepo.AddAsync(docVersion, ct);
                    await _unitOfWork.CommitAsync(ct);
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
            await _unitOfWork.CommitAsync(ct);
        }

        return new GenerateDocumentResultDto(
            Url: downloadUrl,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiry),
            GenerationId: generationId,
            OutputFormat: ext
        );
    }
}
