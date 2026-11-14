using System.Diagnostics;

namespace SmkDoc.Application.Common.Storage;

/// <summary>
/// Performs best-effort compensating deletion of newly uploaded storage objects when a subsequent
/// persistence step fails, preserving the primary failure context.
/// </summary>
public static class StorageOrphanCleaner
{
    public static async Task TryDeleteOrphanAsync(
        IStorageService storageService,
        string bucket,
        string storageKey,
        Exception primaryException,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(primaryException);

        try
        {
            await storageService.DeleteAsync(bucket, storageKey, cancellationToken);
        }
        catch (ExternalServiceException cleanupEx)
        {
            RecordCleanupFailure(bucket, storageKey, primaryException, cleanupEx);
        }
        catch (HttpRequestException cleanupEx)
        {
            RecordCleanupFailure(bucket, storageKey, primaryException, cleanupEx);
        }
        catch (IOException cleanupEx)
        {
            RecordCleanupFailure(bucket, storageKey, primaryException, cleanupEx);
        }
        catch (TimeoutException cleanupEx)
        {
            RecordCleanupFailure(bucket, storageKey, primaryException, cleanupEx);
        }
        catch (InvalidOperationException cleanupEx)
        {
            RecordCleanupFailure(bucket, storageKey, primaryException, cleanupEx);
        }
    }

    private static void RecordCleanupFailure(
        string bucket,
        string storageKey,
        Exception primaryException,
        Exception cleanupEx)
    {
        primaryException.Data["OrphanCleanupError"] = cleanupEx.Message;
        Trace.TraceWarning(
            "Orphaned object cleanup failed for {0}/{1}: {2}",
            bucket,
            storageKey,
            cleanupEx.Message);
    }
}
