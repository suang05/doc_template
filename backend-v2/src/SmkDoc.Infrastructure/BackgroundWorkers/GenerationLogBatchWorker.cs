using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class GenerationLogWorkerOptions
{
    public int BatchSize { get; set; } = 100;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public int ChannelCapacity { get; set; } = 10_000;
}

public sealed class GenerationLogBatchWorker(
    IGenerationLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<GenerationLogBatchWorker> logger,
    TimeProvider timeProvider,
    IOptions<GenerationLogWorkerOptions>? options = null,
    IHostApplicationLifetime? lifetime = null) : BackgroundService
{
    private readonly int _batchSize = options?.Value.BatchSize ?? 100;
    private readonly TimeSpan _flushInterval = options?.Value.FlushInterval ?? TimeSpan.FromMilliseconds(500);
    private readonly TimeProvider _timeProvider = timeProvider;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stoppingRegistration = lifetime?.ApplicationStopping.Register(() => queue.Complete());

        logger.LogInformation(
            "GenerationLogBatchWorker started with batch size {BatchSize} and flush interval {FlushIntervalMs}ms.",
            _batchSize,
            _flushInterval.TotalMilliseconds);

        var batch = new List<GenerationLog>(_batchSize);
        var lastFlushTime = _timeProvider.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(_flushInterval);

                try
                {
                    if (await queue.Reader.WaitToReadAsync(timeoutCts.Token))
                    {
                        while (batch.Count < _batchSize && queue.Reader.TryRead(out var log))
                        {
                            batch.Add(log);
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flush interval elapsed; fall through to batch check
                }

                if (batch.Count > 0 && (batch.Count >= _batchSize || _timeProvider.GetUtcNow() - lastFlushTime >= _flushInterval))
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    lastFlushTime = _timeProvider.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred in GenerationLogBatchWorker processing loop.");
            }
        }

        // Graceful Drain: drain all remaining logs before host terminates
        await DrainRemainingLogsAsync(batch);
    }

    private async Task DrainRemainingLogsAsync(List<GenerationLog> batch)
    {
        logger.LogInformation("GenerationLogBatchWorker shutting down. Draining remaining queued logs...");
        while (queue.Reader.TryRead(out var log))
        {
            batch.Add(log);
            if (batch.Count >= _batchSize)
            {
                await FlushBatchAsync(batch, CancellationToken.None);
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, CancellationToken.None);
        }

        logger.LogInformation("GenerationLogBatchWorker graceful drain complete.");
    }

    private async Task FlushBatchAsync(List<GenerationLog> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        var snapshot = batch.ToArray();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGenerationLogRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.AddRangeAsync(snapshot, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogDebug("Successfully flushed batch of {Count} generation logs.", snapshot.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to flush batch of {Count} generation logs to database.", snapshot.Length);
        }
        finally
        {
            batch.Clear();
        }
    }
}

