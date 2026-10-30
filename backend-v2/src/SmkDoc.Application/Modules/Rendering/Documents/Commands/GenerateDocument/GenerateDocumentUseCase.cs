using System.Diagnostics;
using FluentValidation;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
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
    ICurrentProjectContext projectContext,
    IValidator<GenerateDocumentCommand> validator,
    TimeProvider? timeProvider = null) : IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<GenerateDocumentResultDto> ExecuteAsync(
        GenerateDocumentCommand request, CancellationToken ct = default)
        => ExecuteAsync(request.Slug, request, ct);

    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        string slug, GenerateDocumentCommand request, CancellationToken ct = default)
    {
        // 1. Fail-Fast Static Input Validation
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Document generation requires an active project context.");
        }

        var sw = Stopwatch.StartNew();

        var template = await templateRepo.GetBySlugWithDetailsAsync(slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{slug}' not found or inactive.");
        }

        if (template.CurrentVersionId is null)
        {
            throw new BusinessRuleViolationException($"Template '{slug}' has no published version.");
        }

        var currentVersion = template.CurrentVersion
            ?? template.FindVersion(template.CurrentVersionId.Value)
            ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version for template '{slug}' not found.");

        var outputFormat = request.OutputFormat.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _ => OutputFormat.Pdf
        };

        var engineType = currentVersion.GetRenderEngineType();
        var engine = engines.FirstOrDefault(e => e.EngineType == engineType)
            ?? throw new RenderException(slug, engineType.ToString(), $"No render engine registered for engine type '{engineType}'.");

        string? preparedDataJson = null;
        string ext = outputFormat.Extension;
        string contentType = outputFormat.MimeType;
        var generationId = Guid.CreateVersion7();
        var tenantId = projectContext.ProjectId.Value;
        string outputKey = $"outputs/{tenantId:N}/{_timeProvider.GetUtcNow():yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";
        long fileSizeBytes;
        string sha256;

        // --- Steps 1 & 2: Data Preparation, Schema Gate, Zero-LOH Spooling & MinIO Upload ---
        try
        {
            var preparedData = await dataPreparationService.PrepareDataAsync(
                template, currentVersion, request.Payload, request.SkipValidation, ct);
            preparedDataJson = preparedData.DataJson;

            if (preparedData.ValidationResult is { IsValid: false })
            {
                sw.Stop();
                var errorMessage = $"{preparedData.ValidationResult.Errors.Count} schema violation(s) detected.";
                await auditService.LogValidationFailureAsync(
                    template.Id.Value,
                    currentVersion.Id,
                    preparedData.DataJson,
                    outputFormat,
                    (int)sw.ElapsedMilliseconds,
                    errorMessage,
                    ct);

                await unitOfWork.CommitAsync(ct);

                throw new SchemaValidationException(
                    slug, currentVersion.Version, preparedData.ValidationResult.Errors);
            }

            using var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
            var rawOutputStream = await engine.RenderStreamAsync(templateStream, preparedData.DataJson, outputFormat, ct);

            await using var spooled = await SpooledOutputStream.CreateAndDisposeSourceAsync(rawOutputStream, ct);
            fileSizeBytes = spooled.FileSizeBytes;
            sha256 = spooled.Sha256Hex;

            await storageService.UploadAsync(StorageBuckets.Outputs, outputKey, spooled.ContentStream, contentType, ct);
        }
        catch (Exception ex) when (ex is not SchemaValidationException and not OperationCanceledException)
        {
            sw.Stop();
            try
            {
                var fallbackPayload = preparedDataJson ?? request.Payload.GetRawText();
                await auditService.LogFailureAsync(
                    template.Id.Value,
                    currentVersion.Id,
                    fallbackPayload,
                    outputFormat,
                    (int)sw.ElapsedMilliseconds,
                    ex.Message,
                    CancellationToken.None);

                await unitOfWork.CommitAsync(CancellationToken.None);
            }
            catch (Exception auditEx)
            {
                ex.Data["AuditFailurePersistenceError"] = auditEx.Message;
                Trace.TraceWarning(
                    "Failed to persist FAILED generation audit log for template '{0}': {1}",
                    slug,
                    auditEx.Message);
            }

            throw;
        }

        // --- Step 3: Presigned URL, Audit Logging, Version Tracking & Compensating Cleanup ---
        try
        {
            var expiry = TimeSpan.FromHours(24);
            string downloadUrl = await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

            sw.Stop();

            await auditService.LogSuccessAsync(
                generationId,
                template.Id.Value,
                currentVersion.Id,
                preparedDataJson!,
                outputKey,
                outputFormat,
                fileSizeBytes,
                (int)sw.ElapsedMilliseconds,
                ct);

            if (!string.IsNullOrWhiteSpace(request.DocumentRef))
            {
                await versioningService.RecordVersionAsync(
                    request.DocumentRef,
                    template.Id.Value,
                    currentVersion.Id,
                    generationId,
                    request.ChangeNote,
                    ct);
            }

            await unitOfWork.CommitAsync(ct);

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
        catch (Exception primaryEx)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Outputs, outputKey, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                primaryEx.Data["OrphanedOutputKey"] = outputKey;
                primaryEx.Data["CompensatingDeleteError"] = cleanupEx.Message;
                Trace.TraceWarning(
                    "Failed to delete orphaned output object '{0}' after database commit failure: {1}",
                    outputKey,
                    cleanupEx.Message);
            }

            throw;
        }
    }
}
