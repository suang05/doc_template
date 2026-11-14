using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Modules.Rendering.Documents.Services;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class DocumentRetentionWorkerOptions
{
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(30);
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Background worker periodically purging expired document outputs from MinIO storage.
/// Invariant: Storage retention enforcement prevents unbounded storage consumption.
/// </summary>
public sealed partial class DocumentRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DocumentRetentionWorker> logger,
    IOptions<DocumentRetentionWorkerOptions>? options = null) : BackgroundService
{
    private readonly TimeSpan _retentionPeriod = options?.Value.RetentionPeriod ?? TimeSpan.FromHours(24);
    private readonly TimeSpan _checkInterval = options?.Value.CheckInterval ?? TimeSpan.FromMinutes(30);
    private readonly int _batchSize = options?.Value.BatchSize ?? 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogWorkerStarted(logger, _retentionPeriod.TotalHours, _checkInterval.TotalMinutes, _batchSize);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, timeProvider, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var retentionProcessor = scope.ServiceProvider.GetRequiredService<IDocumentRetentionProcessor>();

                int totalPurged = 0;
                int purgedInBatch;
                do
                {
                    purgedInBatch = await retentionProcessor.PurgeExpiredOutputsAsync(
                        _retentionPeriod, _batchSize, stoppingToken);
                    totalPurged += purgedInBatch;
                }
                while (purgedInBatch >= _batchSize && !stoppingToken.IsCancellationRequested);

                if (totalPurged > 0)
                {
                    LogCycleCompleted(logger, totalPurged);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbException ex)
            {
                LogCycleFailure(ex);
            }
            catch (ExternalServiceException ex)
            {
                LogCycleFailure(ex);
            }
            catch (HttpRequestException ex)
            {
                LogCycleFailure(ex);
            }
            catch (IOException ex)
            {
                LogCycleFailure(ex);
            }
            catch (TimeoutException ex)
            {
                LogCycleFailure(ex);
            }
            catch (InvalidOperationException ex)
            {
                LogCycleFailure(ex);
            }
        }

        LogWorkerStopped(logger);
    }

    private void LogCycleFailure(Exception ex) =>
        WriteCycleFailure(logger, ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRetentionWorker started. RetentionPeriod={RetentionHours}h, CheckInterval={IntervalMinutes}m, BatchSize={BatchSize}")]
    private static partial void LogWorkerStarted(ILogger logger, double retentionHours, double intervalMinutes, int batchSize);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRetentionWorker cycle completed. Purged {Count} files.")]
    private static partial void LogCycleCompleted(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRetentionWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unexpected error occurred during DocumentRetentionWorker cycle.")]
    private static partial void WriteCycleFailure(ILogger logger, Exception ex);
}

