using System.Collections.Concurrent;
using System.Threading.Channels;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Infrastructure.Queue;

/// <summary>
/// Thread-safe in-memory queue implementing <see cref="IDocumentRenderQueue"/>
/// for local single-node development and environments without Redis.
/// </summary>
public sealed class MemoryDocumentRenderQueue : IDocumentRenderQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, DocumentJob> _jobs = new();

    public async Task<Guid> EnqueueAsync(DocumentJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        _jobs[job.JobId] = job;
        await _channel.Writer.WriteAsync(job.JobId, ct);
        return job.JobId;
    }

    public async Task<DocumentJob?> DequeueAsync(CancellationToken ct = default)
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

    public Task<DocumentJob?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateJobAsync(DocumentJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }
}

