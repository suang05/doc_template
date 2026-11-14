using System.Diagnostics;
using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents;

public sealed class GenerateDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IDataBindingEngine dataBindingEngine,
    IDocumentAuditProcessor auditProcessor,
    IDocumentVersionProcessor versionProcessor,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>
{
    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        GenerateDocumentCommand command, CancellationToken ct)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.GetRequiredProjectId();

        var outputFormat = ParseOutputFormat(command.OutputFormat);

        string slug = command.Slug;
        var template = await templateRepo.GetBySlugWithDetailsAsync(slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{slug}' not found or inactive.");
        }

        var currentVersion = template.GetPublishedVersion();
        var generationId = Guid.CreateVersion7();
        var tenantId = projectId.Value;
        string ext = outputFormat.Extension;
        string outputKey = $"outputs/{tenantId:N}/{timeProvider.GetUtcNow():yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        var render = await RenderAndUploadAsync(
            template, currentVersion, command.Payload, command.SkipValidation, outputFormat, outputKey, ct);

        return await FinalizeDocumentAsync(
            command, template, currentVersion, generationId, outputKey, outputFormat, render, ct);
    }

    private async Task<RenderOutput> RenderAndUploadAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement payload,
        bool skipValidation,
        OutputFormat outputFormat,
        string outputKey,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var engineType = currentVersion.GetRenderEngineType();
        var engine = engineFactory.GetEngine(engineType);
        string? preparedDataJson = null;

        try
        {
            var boundData = await dataBindingEngine.BindAndValidateAsync(
                template, currentVersion, payload, skipValidation, ct);
            preparedDataJson = boundData.DataJson;

            if (boundData.ValidationResult is { IsValid: false })
            {
                sw.Stop();
                var errorMessage = $"{boundData.ValidationResult.Errors.Count} schema violation(s) detected.";
                await auditProcessor.LogValidationFailureAsync(
                    template.Id.Value,
                    currentVersion.Id,
                    boundData.DataJson,
                    outputFormat,
                    (int)sw.ElapsedMilliseconds,
                    errorMessage,
                    ct);

                await unitOfWork.CommitAsync(ct);

                throw new SchemaValidationException(
                    template.Slug.Value, currentVersion.Version, boundData.ValidationResult.Errors);
            }

            using var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
            var rawOutputStream = await engine.RenderStreamAsync(templateStream, boundData.DataJson, outputFormat, ct);

            await using var spooled = await SpooledRenderOutput.CreateAndDisposeSourceAsync(rawOutputStream, ct);
            sw.Stop();

            await storageService.UploadAsync(
                StorageBuckets.Outputs, outputKey, spooled.ContentStream, outputFormat.MimeType, ct);

            return new RenderOutput(
                boundData.DataJson,
                spooled.FileSizeBytes,
                spooled.Sha256Hex,
                (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not SchemaValidationException and not OperationCanceledException)
        {
            sw.Stop();
            try
            {
                var fallbackPayload = preparedDataJson ?? (payload.ValueKind != JsonValueKind.Undefined ? payload.GetRawText() : "{}");
                await auditProcessor.LogFailureAsync(
                    template.Id.Value,
                    currentVersion.Id,
                    fallbackPayload,
                    outputFormat,
                    (int)sw.ElapsedMilliseconds,
                    ex.Message,
                    CancellationToken.None);

                await unitOfWork.CommitAsync(CancellationToken.None);
            }
            catch (System.Data.Common.DbException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }
            catch (ExternalServiceException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }
            catch (HttpRequestException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }
            catch (IOException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }
            catch (TimeoutException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }
            catch (InvalidOperationException auditEx)
            {
                RecordAuditFailure(template.Slug.Value, ex, auditEx);
            }

            throw;
        }
    }

    private static void RecordAuditFailure(string templateSlug, Exception primaryEx, Exception auditEx)
    {
        primaryEx.Data["AuditFailurePersistenceError"] = auditEx.Message;
        Trace.TraceWarning(
            "Failed to persist FAILED generation audit log for template '{0}': {1}",
            templateSlug,
            auditEx.Message);
    }

    private async Task<GenerateDocumentResultDto> FinalizeDocumentAsync(
        GenerateDocumentCommand command,
        Template template,
        TemplateVersion currentVersion,
        Guid generationId,
        string outputKey,
        OutputFormat outputFormat,
        RenderOutput render,
        CancellationToken ct)
    {
        try
        {
            var expiry = TimeSpan.FromHours(24);
            string downloadUrl = await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

            await auditProcessor.LogSuccessAsync(
                generationId,
                template.Id.Value,
                currentVersion.Id,
                render.PreparedDataJson,
                outputKey,
                outputFormat,
                render.FileSizeBytes,
                render.ElapsedMilliseconds,
                ct);

            if (!string.IsNullOrWhiteSpace(command.DocumentRef))
            {
                await versionProcessor.RecordVersionAsync(
                    command.DocumentRef,
                    template.Id.Value,
                    currentVersion.Id,
                    generationId,
                    command.ChangeNote,
                    ct);
            }

            await unitOfWork.CommitAsync(ct);

            return new GenerateDocumentResultDto(
                GenerationId: generationId,
                DocumentRef: command.DocumentRef,
                OutputFormat: outputFormat.Extension,
                FileSizeBytes: render.FileSizeBytes,
                Sha256: render.Sha256Hex,
                Url: downloadUrl,
                ExpiresAt: timeProvider.GetUtcNow().Add(expiry)
            );
        }
        catch (Exception primaryEx)
        {
            await SmkDoc.Application.Common.Storage.StorageOrphanCleaner.TryDeleteOrphanAsync(
                storageService,
                StorageBuckets.Outputs,
                outputKey,
                primaryEx,
                CancellationToken.None);

            throw;
        }
    }

    private static OutputFormat ParseOutputFormat(string format) =>
        format.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _ => OutputFormat.Pdf
        };

    private readonly record struct RenderOutput(
        string PreparedDataJson,
        long FileSizeBytes,
        string Sha256Hex,
        int ElapsedMilliseconds);
}
