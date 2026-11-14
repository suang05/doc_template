using System.Diagnostics.CodeAnalysis;
using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Distributed and in-memory queue abstraction for asynchronous enterprise batch rendering jobs.
/// Protects Gotenberg from thread starvation and memory spikes.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain abstraction represents an async render job queue, not System.Collections.Queue.")]
public interface IDocumentRenderQueue
{
    /// <summary>Enqueues a new document rendering job.</summary>
    Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct);

    /// <summary>Dequeues the next pending job from the queue.</summary>
    Task<DocumentJob?> DequeueAsync(CancellationToken ct);

    /// <summary>Retrieves a job by its unique JobId.</summary>
    Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct);

    /// <summary>Updates job status and result metadata.</summary>
    Task UpdateJobAsync(DocumentJob job, CancellationToken ct);
}

