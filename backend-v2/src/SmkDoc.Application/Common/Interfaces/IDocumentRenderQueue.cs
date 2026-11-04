using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Distributed and in-memory queue abstraction for asynchronous enterprise batch rendering jobs.
/// Protects Gotenberg from thread starvation and memory spikes.
/// </summary>
public interface IDocumentRenderQueue
{
    /// <summary>Enqueues a new document rendering job.</summary>
    Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct = default);

    /// <summary>Dequeues the next pending job from the queue.</summary>
    Task<DocumentJob?> DequeueAsync(CancellationToken ct = default);

    /// <summary>Retrieves a job by its unique JobId.</summary>
    Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Updates job status and result metadata.</summary>
    Task UpdateJobAsync(DocumentJob job, CancellationToken ct = default);
}

