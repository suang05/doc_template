using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditService(
    IGenerationLogRepository logRepo,
    ICurrentProjectContext projectContext,
    IStorageService? storageService = null,
    IDocumentMetrics? metrics = null,
    TimeProvider? timeProvider = null) : IDocumentAuditService
{
    public const int MaxInlinePayloadSizeBytes = 16 * 1024; // 16 KB threshold for PostgreSQL OLTP storage

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IStorageService? _storageService = storageService;

    public async Task LogValidationFailureAsync(
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        OutputFormat outputFormat,
        int elapsedMs,
        string errorMessage,
        CancellationToken ct = default)
    {
        var projectId = ResolveProjectId();
        var now = _timeProvider.GetUtcNow();
        var (storedInputData, payloadHash) = await PreparePayloadAsync(projectId, dataJson, now, ct);

        var target = GenerationTarget.ForTemplate(
            new TemplateId(templateId),
            templateVersionId,
            outputFormat,
            inputData: storedInputData,
            payloadHash: payloadHash);

        var context = BuildGenerationContext();

        var failLog = GenerationLog.CreateValidationFailure(
            projectId,
            target,
            context,
            elapsedMs,
            errorMessage,
            now);

        await logRepo.AddAsync(failLog, ct);

        metrics?.RecordGenerationFailure("Unknown", outputFormat.Extension, elapsedMs, "VALIDATION_FAILED");
    }

    public async Task LogFailureAsync(
        Guid templateId,
        Guid templateVersionId,
        string? dataJson,
        OutputFormat outputFormat,
        int elapsedMs,
        string errorMessage,
        CancellationToken ct = default)
    {
        var projectId = ResolveProjectId();
        var now = _timeProvider.GetUtcNow();
        var (storedInputData, payloadHash) = await PreparePayloadAsync(projectId, dataJson, now, ct);

        var target = GenerationTarget.ForTemplate(
            new TemplateId(templateId),
            templateVersionId,
            outputFormat,
            inputData: storedInputData,
            payloadHash: payloadHash);

        var context = BuildGenerationContext();

        var failLog = GenerationLog.CreateFailure(
            projectId,
            target,
            context,
            elapsedMs,
            errorMessage,
            now);

        await logRepo.AddAsync(failLog, ct);

        metrics?.RecordGenerationFailure("Engine", outputFormat.Extension, elapsedMs, "FAILED");
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
        var projectId = ResolveProjectId();
        var now = _timeProvider.GetUtcNow();
        var (storedInputData, payloadHash) = await PreparePayloadAsync(projectId, dataJson, now, ct);

        var target = new GenerationTarget(
            new TemplateId(templateId),
            templateVersionId,
            outputFormat,
            outputKey,
            storedInputData,
            payloadHash);

        var context = BuildGenerationContext();
        var logMetrics = GenerationMetrics.Of(elapsedMs, fileSizeBytes);

        var log = GenerationLog.CreateSuccess(
            generationId,
            projectId,
            target,
            context,
            logMetrics,
            now);

        await logRepo.AddAsync(log, ct);

        metrics?.RecordGenerationSuccess("Engine", outputFormat.Extension, elapsedMs, fileSizeBytes);
    }

    private ProjectId ResolveProjectId() =>
        projectContext.IsResolved
            ? projectContext.ProjectId
            : throw new BusinessRuleViolationException(
                "Document audit logging requires a valid project context.", "MISSING_PROJECT_CONTEXT");

    private GenerationContext BuildGenerationContext()
    {
        var apiKeyId = projectContext.ApiKeyId.HasValue && !projectContext.ApiKeyId.Value.IsEmpty
            ? projectContext.ApiKeyId
            : null;

        var callerApp = projectContext.CallerIdentity?.Trim();
        if (callerApp is { Length: > GenerationLog.MaxCallerAppLength })
        {
            callerApp = callerApp[..GenerationLog.MaxCallerAppLength];
        }

        return new GenerationContext(callerApp, "api", apiKeyId);
    }

    private async Task<(string? StoredInputData, Sha256Hash? PayloadHash)> PreparePayloadAsync(
        ProjectId projectId,
        string? dataJson,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return (null, null);
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(dataJson);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        var hexHash = Convert.ToHexStringLower(hashBytes);
        var payloadHash = new Sha256Hash(hexHash);

        if (bytes.Length <= MaxInlinePayloadSizeBytes || _storageService == null)
        {
            return (dataJson, payloadHash);
        }

        var objectKey = $"audit/{projectId.Value:N}/{now:yyyy/MM/dd}/{hexHash}.json";
        using var stream = new MemoryStream(bytes);
        await _storageService.UploadAsync(StorageBuckets.AuditPayloads, objectKey, stream, "application/json", ct);

        return ($"[OFFLOADED:{objectKey}]", payloadHash);
    }
}
