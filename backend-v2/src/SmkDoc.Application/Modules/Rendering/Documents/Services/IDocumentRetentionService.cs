namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

/// <summary>
/// Service responsible for scanning and purging expired document output files from storage.
/// Invariant: Storage retention enforcement prevents unbounded storage consumption.
/// </summary>
public interface IDocumentRetentionService
{
    /// <summary>
    /// Scans for document outputs older than the specified retention window, deletes them from storage,
    /// and updates the generation log state.
    /// </summary>
    /// <param name="retentionWindow">Age after which generated document outputs are eligible for deletion.</param>
    /// <param name="batchSize">Maximum number of records to process in a single purge batch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The total number of expired outputs purged in this cycle.</returns>
    Task<int> PurgeExpiredOutputsAsync(TimeSpan retentionWindow, int batchSize, CancellationToken ct = default);
}

