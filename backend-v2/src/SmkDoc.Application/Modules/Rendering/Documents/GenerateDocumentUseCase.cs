using System.Diagnostics;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents;

public sealed class GenerateDocumentUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> engines,
    IDocumentDataPreparationService dataPreparationService,
    IDocumentAuditService auditService,
    IDocumentVersioningService versioningService,
    IUnitOfWork unitOfWork)
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IEnumerable<IRenderEngine> _engines = engines;
    private readonly IDocumentDataPreparationService _dataPreparationService = dataPreparationService;
    private readonly IDocumentAuditService _auditService = auditService;
    private readonly IDocumentVersioningService _versioningService = versioningService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        string slug, GenerateDocumentCommand request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var template = await _templateRepo.GetBySlugWithDetailsAsync(slug, ct)
            ?? await _templateRepo.GetBySlugAsync(slug, ct);
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

        // --- Step 1: Fail-Fast Data Preparation & Schema Validation Gate ---
        var preparedData = await _dataPreparationService.PrepareDataAsync(
            template, currentVersion, request.Data, request.SkipValidation, ct);

        if (preparedData.ValidationResult is { IsValid: false })
        {
            sw.Stop();
            var errorMessage = $"{preparedData.ValidationResult.Errors.Count} schema violation(s) detected.";
            await _auditService.LogValidationFailureAsync(
                template.Id,
                currentVersion.Id,
                preparedData.DataJson,
                outputFormat,
                (int)sw.ElapsedMilliseconds,
                errorMessage,
                ct);

            throw new SchemaValidationException(
                slug, currentVersion.Version, preparedData.ValidationResult.Errors);
        }

        // --- Step 2: Download Template from Storage & Render Output ---
        using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
        await using var outputStream = await engine.RenderStreamAsync(templateStream, preparedData.DataJson, outputFormat, ct);

        string ext = outputFormat.Extension;
        string contentType = outputFormat.MimeType;

        var generationId = Guid.CreateVersion7();
        string outputKey = $"outputs/{DateTime.UtcNow:yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        long fileSizeBytes = outputStream.CanSeek ? outputStream.Length : 0;

        await _storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

        var expiry = TimeSpan.FromHours(24);
        string downloadUrl = await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

        sw.Stop();

        // --- Step 3: Audit Logging & Version Tracking ---
        await _auditService.LogSuccessAsync(
            generationId,
            template.Id,
            currentVersion.Id,
            preparedData.DataJson,
            outputKey,
            outputFormat,
            fileSizeBytes,
            (int)sw.ElapsedMilliseconds,
            ct);

        if (!string.IsNullOrWhiteSpace(request.DocumentRef))
        {
            await _versioningService.RecordVersionAsync(
                request.DocumentRef,
                template.Id,
                currentVersion.Id,
                generationId,
                request.ChangeNote,
                ct);
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
