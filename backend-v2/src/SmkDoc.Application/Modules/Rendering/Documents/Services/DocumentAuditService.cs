using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditService(
    IRepository<GenerationLog> logRepo,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    IDocumentMetrics? metrics = null,
    TimeProvider? timeProvider = null) : IDocumentAuditService
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task LogValidationFailureAsync(
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        OutputFormat outputFormat,
        int elapsedMs,
        string errorMessage,
        CancellationToken ct = default)
    {
        var failLog = GenerationLog.CreateValidationFailure(
            templateId,
            templateVersionId,
            executionContext.ApiKeyId,
            executionContext.CallerApp,
            "api",
            dataJson,
            outputFormat,
            elapsedMs,
            errorMessage,
            this.timeProvider.GetUtcNow(),
            executionContext.ProjectId.HasValue ? new SmkDoc.Domain.ValueObjects.Ids.ProjectId(executionContext.ProjectId.Value) : SmkDoc.Domain.ValueObjects.Ids.ProjectId.Empty);

        await logRepo.AddAsync(failLog, ct);
        await unitOfWork.CommitAsync(ct);

        metrics?.RecordGenerationFailure("Unknown", outputFormat.Extension, elapsedMs, "VALIDATION_FAILED");
    }

    public async Task LogSuccessAsync(
        Guid generationId,
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        string outputKey,
        OutputFormat outputFormat,
        long fileSizeBytes,
        int elapsedMs,
        CancellationToken ct = default)
    {
        var log = GenerationLog.CreateSuccess(
            generationId,
            templateId,
            templateVersionId,
            executionContext.ApiKeyId,
            executionContext.CallerApp,
            "api",
            dataJson,
            outputKey,
            outputFormat,
            fileSizeBytes,
            elapsedMs,
            this.timeProvider.GetUtcNow(),
            executionContext.ProjectId.HasValue ? new SmkDoc.Domain.ValueObjects.Ids.ProjectId(executionContext.ProjectId.Value) : SmkDoc.Domain.ValueObjects.Ids.ProjectId.Empty);

        await logRepo.AddAsync(log, ct);

        metrics?.RecordGenerationSuccess("Engine", outputFormat.Extension, elapsedMs, fileSizeBytes);
    }
}
