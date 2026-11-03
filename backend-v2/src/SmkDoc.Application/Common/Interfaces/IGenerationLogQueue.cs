using System.Threading.Channels;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Thread-safe in-memory queue for decoupling high-throughput generation audit logging
/// from synchronous HTTP request pipelines.
/// </summary>
public interface IGenerationLogQueue
{
    ValueTask EnqueueAsync(GenerationLog log, CancellationToken ct = default);
    ChannelReader<GenerationLog> Reader { get; }
    void Complete();
}

