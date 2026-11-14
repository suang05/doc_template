using System.Data.Common;
using System.Threading.Channels;
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

public sealed partial class GenerationLogBatchWorker(
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

        if (logger.IsEnabled(LogLevel.Information))
        {
            LogWorkerStarted(logger, _batchSize, _flushInterval.TotalMilliseconds);
        }

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
            catch (ChannelClosedException ex)
            {
                LogChannelClosed(logger, ex);
            }
            catch (DbException ex)
            {
                LogDatabaseLoopError(logger, ex);
            }
            catch (TimeoutException ex)
            {
                LogTimeoutLoopError(logger, ex);
            }
            catch (InvalidOperationException ex)
            {
                LogInvalidStateLoopError(logger, ex);
            }
        }

        // Graceful Drain: drain all remaining logs before host terminates
        await DrainRemainingLogsAsync(batch);
    }

    private async Task DrainRemainingLogsAsync(List<GenerationLog> batch)
    {
        LogDrainStarting(logger);
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

        LogDrainComplete(logger);
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
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogFlushedBatch(logger, snapshot.Length);
            }
        }
        catch (DbException ex)
        {
            LogFlushFailure(ex, snapshot.Length);
        }
        catch (TimeoutException ex)
        {
            LogFlushFailure(ex, snapshot.Length);
        }
        catch (InvalidOperationException ex)
        {
            LogFlushFailure(ex, snapshot.Length);
        }
        catch (IOException ex)
        {
            LogFlushFailure(ex, snapshot.Length);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogFlushFailure(ex, snapshot.Length);
        }
        finally
        {
            batch.Clear();
        }
    }

    private void LogFlushFailure(Exception ex, int count) =>
        WriteFlushFailure(logger, ex, count);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "GenerationLogBatchWorker started with batch size {BatchSize} and flush interval {FlushIntervalMs}ms.")]
    private static partial void LogWorkerStarted(ILogger logger, int batchSize, double flushIntervalMs);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "GenerationLog channel closed unexpectedly in GenerationLogBatchWorker processing loop.")]
    private static partial void LogChannelClosed(ILogger logger, ChannelClosedException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Database error occurred in GenerationLogBatchWorker processing loop.")]
    private static partial void LogDatabaseLoopError(ILogger logger, DbException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Timeout error occurred in GenerationLogBatchWorker processing loop.")]
    private static partial void LogTimeoutLoopError(ILogger logger, TimeoutException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Invalid state error occurred in GenerationLogBatchWorker processing loop.")]
    private static partial void LogInvalidStateLoopError(ILogger logger, InvalidOperationException ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "GenerationLogBatchWorker shutting down. Draining remaining queued logs...")]
    private static partial void LogDrainStarting(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "GenerationLogBatchWorker graceful drain complete.")]
    private static partial void LogDrainComplete(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Successfully flushed batch of {Count} generation logs.")]
    private static partial void LogFlushedBatch(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to flush batch of {Count} generation logs to database.")]
    private static partial void WriteFlushFailure(ILogger logger, Exception ex, int count);
}

