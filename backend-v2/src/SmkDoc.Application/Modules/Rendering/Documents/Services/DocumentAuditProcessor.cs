using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditProcessor(
    IGenerationLogQueue logQueue,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider,
    IStorageService? storageService = null,
    IDocumentMetrics? metrics = null) : IDocumentAuditProcessor
{
    public const int MaxInlinePayloadSizeBytes = 16 * 1024; // 16 KB threshold for PostgreSQL OLTP storage

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
        var now = timeProvider.GetUtcNow();
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

        await logQueue.EnqueueAsync(failLog, ct);

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
        var now = timeProvider.GetUtcNow();
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

        await logQueue.EnqueueAsync(failLog, ct);

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
        var now = timeProvider.GetUtcNow();
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

        await logQueue.EnqueueAsync(log, ct);

        metrics?.RecordGenerationSuccess("Engine", outputFormat.Extension, elapsedMs, fileSizeBytes);
    }

    private ProjectId ResolveProjectId() =>
        projectContext.GetRequiredProjectId();

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

        int byteCount = System.Text.Encoding.UTF8.GetByteCount(dataJson);
        if (byteCount <= MaxInlinePayloadSizeBytes || storageService == null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(dataJson);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
            var hexHash = Convert.ToHexStringLower(hashBytes);
            return (dataJson, new Sha256Hash(hexHash));
        }

        await using var stream = await HybridSpoolStream.CreateFromUtf8StringAsync(dataJson, ct: ct);
        byte[] hashBytesLarge = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        stream.Position = 0;
        var hexHashLarge = Convert.ToHexStringLower(hashBytesLarge);
        var payloadHashLarge = new Sha256Hash(hexHashLarge);

        var objectKey = $"audit/{projectId.Value:N}/{now:yyyy/MM/dd}/{hexHashLarge}.json";
        await storageService.UploadAsync(StorageBuckets.AuditPayloads, objectKey, stream, "application/json", ct);

        return ($"[OFFLOADED:{objectKey}]", payloadHashLarge);
    }
}
