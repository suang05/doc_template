using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

/// <summary>
/// Domain application service implementing <see cref="IDocumentRetentionService"/>.
/// Scans expired document outputs across all tenants via <see cref="IGenerationLogRepository"/>,
/// purges the physical file in MinIO storage, and marks the generation log as purged.
/// </summary>
public sealed class DocumentRetentionService(
    IGenerationLogRepository logRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDocumentRetentionService
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

            if (!string.IsNullOrWhiteSpace(log.OutputKey))
            {
                try
                {
                    await storageService.DeleteAsync(StorageBuckets.Outputs, log.OutputKey, ct);
                }
                catch (Exception)
                {
                    // Storage cleanup failure does not abort purging cycle; log remains eligible for audit inspection
                }
            }

            log.MarkOutputPurged(now);
            purgedCount++;
        }

        await unitOfWork.CommitAsync(ct);
        return purgedCount;
    }
}
