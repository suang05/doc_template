using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
public sealed class DocumentRetentionWorker(
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
        logger.LogInformation(
            "DocumentRetentionWorker started. RetentionPeriod={RetentionHours}h, CheckInterval={IntervalMinutes}m, BatchSize={BatchSize}",
            _retentionPeriod.TotalHours,
            _checkInterval.TotalMinutes,
            _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, timeProvider, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<IDocumentRetentionService>();

                int totalPurged = 0;
                int purgedInBatch;
                do
                {
                    purgedInBatch = await retentionService.PurgeExpiredOutputsAsync(
                        _retentionPeriod, _batchSize, stoppingToken);
                    totalPurged += purgedInBatch;
                }
                while (purgedInBatch >= _batchSize && !stoppingToken.IsCancellationRequested);

                if (totalPurged > 0)
                {
                    logger.LogInformation("DocumentRetentionWorker cycle completed. Purged {Count} files.", totalPurged);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during DocumentRetentionWorker cycle.");
            }
        }

        logger.LogInformation("DocumentRetentionWorker stopped.");
    }
}

