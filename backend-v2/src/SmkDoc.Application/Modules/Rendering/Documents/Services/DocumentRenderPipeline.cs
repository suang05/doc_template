using System.Diagnostics;
using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentRenderPipeline(
    IRenderEngineFactory engineFactory,
    IDataBindingEngine dataBindingEngine,
    IStorageService storageService,
    IDocumentAuditService auditService,
    IUnitOfWork unitOfWork) : IDocumentRenderPipeline
{
    public async Task<DocumentRenderResult> RenderAndUploadAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement payload,
        bool skipValidation,
        OutputFormat outputFormat,
        string outputKey,
        CancellationToken ct = default)
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
                await auditService.LogValidationFailureAsync(
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

            await using var spooled = await SpooledOutputStream.CreateAndDisposeSourceAsync(rawOutputStream, ct);
            sw.Stop();

            await storageService.UploadAsync(
                StorageBuckets.Outputs, outputKey, spooled.ContentStream, outputFormat.MimeType, ct);

            return new DocumentRenderResult(
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
                var fallbackPayload = preparedDataJson ?? payload.GetRawText();
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
                    template.Slug.Value,
                    auditEx.Message);
            }

            throw;
        }
    }
}
