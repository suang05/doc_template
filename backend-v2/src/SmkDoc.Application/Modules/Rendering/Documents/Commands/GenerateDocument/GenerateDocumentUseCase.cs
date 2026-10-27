using System.Diagnostics;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents;

public sealed class GenerateDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> engines,
    IDocumentDataPreparationService dataPreparationService,
    IDocumentAuditService auditService,
    IDocumentVersioningService versioningService,
    IUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    TimeProvider? timeProvider = null) : IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IEnumerable<IRenderEngine> _engines = engines;
    private readonly IDocumentDataPreparationService _dataPreparationService = dataPreparationService;
    private readonly IDocumentAuditService _auditService = auditService;
    private readonly IDocumentVersioningService _versioningService = versioningService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<GenerateDocumentResultDto> ExecuteAsync(
        GenerateDocumentCommand request, CancellationToken ct = default)
        => ExecuteAsync(request.Slug, request, ct);

    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        string slug, GenerateDocumentCommand request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var template = await _templateRepo.GetBySlugWithDetailsAsync(slug, ct);
        if (template == null || !template.IsActive)
            throw new NotFoundException($"Template '{slug}' not found or inactive.");

        if (template.CurrentVersionId is null)
            throw new BusinessRuleViolationException($"Template '{slug}' has no published version.");

        var currentVersion = template.CurrentVersion
            ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version for template '{slug}' not found.");

        var outputFormat = request.OutputFormat.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _      => OutputFormat.Pdf
        };

        var engineType = currentVersion.GetRenderEngineType();

        var engine = _engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new RenderException(slug, engineType.ToString(), $"No render engine registered for engine type '{engineType}'.");

        // --- Step 1: Fail-Fast Data Preparation & Schema Validation Gate ---
        var preparedData = await _dataPreparationService.PrepareDataAsync(
            template, currentVersion, request.Payload, request.SkipValidation, ct);

        if (preparedData.ValidationResult is { IsValid: false })
        {
            sw.Stop();
            var errorMessage = $"{preparedData.ValidationResult.Errors.Count} schema violation(s) detected.";
            await _auditService.LogValidationFailureAsync(
                template.Id.Value,
                currentVersion.Id,
                preparedData.DataJson,
                outputFormat,
                (int)sw.ElapsedMilliseconds,
                errorMessage,
                ct);

            await _unitOfWork.CommitAsync(ct);

            throw new SchemaValidationException(
                slug, currentVersion.Version, preparedData.ValidationResult.Errors);
        }

        // --- Step 2: Download Template from Storage & Render Output ---
        using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
        await using var outputStream = await engine.RenderStreamAsync(templateStream, preparedData.DataJson, outputFormat, ct);

        string ext = outputFormat.Extension;
        string contentType = outputFormat.MimeType;

        var generationId = Guid.CreateVersion7();
        var tenantId = _executionContext.ProjectId.HasValue && _executionContext.ProjectId.Value != Guid.Empty
            ? _executionContext.ProjectId.Value
            : template.ProjectId.Value;
        string outputKey = $"outputs/{tenantId:N}/{_timeProvider.GetUtcNow():yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        long fileSizeBytes = 0;
        string sha256 = string.Empty;
        if (outputStream.CanSeek)
        {
            fileSizeBytes = outputStream.Length;
            outputStream.Position = 0;
            byte[] hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(outputStream, ct);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
            outputStream.Position = 0;
        }

        await _storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

        var expiry = TimeSpan.FromHours(24);
        string downloadUrl = await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

        sw.Stop();

        // --- Step 3: Audit Logging & Version Tracking ---
        await _auditService.LogSuccessAsync(
            generationId,
            template.Id.Value,
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
                template.Id.Value,
                currentVersion.Id,
                generationId,
                request.ChangeNote,
                ct);
        }

        await _unitOfWork.CommitAsync(ct);

        return new GenerateDocumentResultDto(
            GenerationId: generationId,
            DocumentRef: request.DocumentRef,
            OutputFormat: ext,
            FileSizeBytes: fileSizeBytes,
            Sha256: sha256,
            Url: downloadUrl,
            ExpiresAt: _timeProvider.GetUtcNow().Add(expiry)
        );
    }
}
