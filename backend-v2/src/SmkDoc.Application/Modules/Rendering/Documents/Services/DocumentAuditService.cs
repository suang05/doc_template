using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

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
        var projectId = executionContext.ProjectId.HasValue && executionContext.ProjectId.Value != Guid.Empty
            ? new ProjectId(executionContext.ProjectId.Value)
            : throw new SmkDoc.Domain.Exceptions.BusinessRuleViolationException(
                "Document audit logging requires a valid project context.", "MISSING_PROJECT_CONTEXT");

        var apiKeyId = executionContext.ApiKeyId.HasValue && executionContext.ApiKeyId.Value != Guid.Empty
            ? (ApiKeyId?)new ApiKeyId(executionContext.ApiKeyId.Value)
            : null;

        var target = GenerationTarget.ForTemplate(
            new TemplateId(templateId),
            templateVersionId,
            outputFormat,
            inputData: dataJson);

        var context = new GenerationContext(
            executionContext.CallerApp,
            "api",
            apiKeyId);

        var failLog = GenerationLog.CreateValidationFailure(
            projectId,
            target,
            context,
            elapsedMs,
            errorMessage,
            this.timeProvider.GetUtcNow());

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
        var projectId = executionContext.ProjectId.HasValue && executionContext.ProjectId.Value != Guid.Empty
            ? new ProjectId(executionContext.ProjectId.Value)
            : throw new SmkDoc.Domain.Exceptions.BusinessRuleViolationException(
                "Document audit logging requires a valid project context.", "MISSING_PROJECT_CONTEXT");

        var apiKeyId = executionContext.ApiKeyId.HasValue && executionContext.ApiKeyId.Value != Guid.Empty
            ? (ApiKeyId?)new ApiKeyId(executionContext.ApiKeyId.Value)
            : null;

        var target = new GenerationTarget(
            new TemplateId(templateId),
            templateVersionId,
            outputFormat,
            outputKey,
            dataJson);

        var context = new GenerationContext(
            executionContext.CallerApp,
            "api",
            apiKeyId);

        var logMetrics = GenerationMetrics.Of(elapsedMs, fileSizeBytes);

        var log = GenerationLog.CreateSuccess(
            generationId,
            projectId,
            target,
            context,
            logMetrics,
            this.timeProvider.GetUtcNow());

        await logRepo.AddAsync(log, ct);

        metrics?.RecordGenerationSuccess("Engine", outputFormat.Extension, elapsedMs, fileSizeBytes);
    }
}
