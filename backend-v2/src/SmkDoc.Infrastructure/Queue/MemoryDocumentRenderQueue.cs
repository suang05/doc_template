using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Infrastructure.Queue;

/// <summary>
/// Thread-safe in-memory queue implementing <see cref="IDocumentRenderQueue"/>
/// for local single-node development and environments without Redis.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "In-memory message queue implementation, not System.Collections.Queue.")]
public sealed class MemoryDocumentRenderQueue : IDocumentRenderQueue
{
#pragma warning disable RS0030 // [PHASE-3-TECH-DEBT] Replace MemoryDocumentRenderQueue unbounded channel with PostgreSQL document_jobs (Pipeline C)
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });
#pragma warning restore RS0030

    private readonly ConcurrentDictionary<Guid, DocumentJob> _jobs = new();

    public async Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        _jobs[job.JobId] = job;
        await _channel.Writer.WriteAsync(job.JobId, ct);
        return job.JobId;
    }

    public async Task<DocumentJob?> DequeueAsync(CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct))
        {
            if (_channel.Reader.TryRead(out var jobId) && _jobs.TryGetValue(jobId, out var job))
            {
                return job;
            }
        }

        return null;
    }

    public Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateJobAsync(DocumentJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }
}

