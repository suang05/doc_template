using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Services.Logging;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Bounded channel log queue implementation, not System.Collections.Queue.")]
public sealed class ChannelGenerationLogQueue : IGenerationLogQueue
{
    private readonly Channel<GenerationLog> _channel;

    public ChannelGenerationLogQueue(int capacity = 10_000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<GenerationLog>(options);
    }

    public ValueTask EnqueueAsync(GenerationLog log, CancellationToken ct) =>
        _channel.Writer.WriteAsync(log, ct);

    public ChannelReader<GenerationLog> Reader => _channel.Reader;

    public void Complete() => _channel.Writer.TryComplete();
}

