using SmkDoc.Application.Common;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

/// <summary>
/// Domain application processor implementing <see cref="IDocumentRetentionProcessor"/>.
/// Scans expired document outputs across all tenants via <see cref="IGenerationLogRepository"/>,
/// purges the physical file in MinIO storage, and marks the generation log as purged.
/// </summary>
public sealed class DocumentRetentionProcessor(
    IGenerationLogRepository logRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDocumentRetentionProcessor
{
    public async Task<int> PurgeExpiredOutputsAsync(TimeSpan retentionWindow, int batchSize, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var cutoffTime = now.Subtract(retentionWindow);

        var expiredLogs = await logRepo.GetExpiredLogsWithOutputsAsync(cutoffTime, batchSize, ct);
        if (expiredLogs.Count == 0)
        {
            return 0;
        }

        int purgedCount = 0;
        foreach (var log in expiredLogs)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await TryDeleteExpiredOutputAsync(log.OutputKey, ct);
            log.MarkOutputPurged(now);
            purgedCount++;
        }

        await unitOfWork.CommitAsync(ct);
        return purgedCount;
    }

    private async Task TryDeleteExpiredOutputAsync(string? outputKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(outputKey))
        {
            return;
        }

        try
        {
            await storageService.DeleteAsync(StorageBuckets.Outputs, outputKey, ct);
        }
        catch (Exception ex) when (ex is
            ExternalServiceException or
            HttpRequestException or
            IOException or
            TimeoutException or
            InvalidOperationException)
        {
            LogRetentionCleanupFailure(outputKey, ex);
        }
    }

    private static void LogRetentionCleanupFailure(string outputKey, Exception ex) =>
        // Storage cleanup failure does not abort purging cycle; log remains eligible for audit inspection
        System.Diagnostics.Trace.TraceWarning("Failed to purge output '{0}': {1}", outputKey, ex.Message);
}
