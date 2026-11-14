using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Thread-safe in-memory queue for decoupling high-throughput generation audit logging
/// from synchronous HTTP request pipelines.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain abstraction represents an async log queue, not System.Collections.Queue.")]
public interface IGenerationLogQueue
{
    ValueTask EnqueueAsync(GenerationLog log, CancellationToken ct);
    ChannelReader<GenerationLog> Reader { get; }
    void Complete();
}

