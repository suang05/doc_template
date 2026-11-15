using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class DocumentRetentionWorkerOptions
{
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(30);
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Background worker periodically purging expired document outputs from MinIO storage
/// across active tenant projects via explicit RLS-scoped <see cref="ITenantScope"/> execution.
/// </summary>
public sealed partial class DocumentRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DocumentRetentionWorker> logger,
    IOptions<DocumentRetentionWorkerOptions>? options = null,
    NpgsqlDataSource? dataSource = null) : BackgroundService
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
                await RunPurgeIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is
                DbException or
                ExternalServiceException or
                HttpRequestException or
                IOException or
                TimeoutException or
                InvalidOperationException)
            {
                LogCycleFailure(ex);
            }
        }

        LogWorkerStopped(logger);
    }

    private async Task RunPurgeIterationAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_checkInterval, timeProvider, stoppingToken);

        int totalPurged = 0;

        if (dataSource is not null)
        {
            var activeProjectIds = await ListActiveProjectIdsAsync(dataSource, stoppingToken);
            foreach (var projectId in activeProjectIds)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                totalPurged += await PurgeSingleProjectAsync(new ProjectId(projectId), stoppingToken);
            }
        }
        else
        {
            using var scope = scopeFactory.CreateScope();
            var retentionProcessor = scope.ServiceProvider.GetRequiredService<IDocumentRetentionProcessor>();
            totalPurged += await PurgeAllBatchesInScopeAsync(retentionProcessor, stoppingToken);
        }

        if (totalPurged > 0)
        {
            LogCycleCompleted(logger, totalPurged);
        }
    }

    private async Task<int> PurgeSingleProjectAsync(ProjectId projectId, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantScope = scope.ServiceProvider.GetService<ITenantScope>();
        using var _ = tenantScope?.BeginTenant(projectId, "system:retention-worker");

        var retentionProcessor = scope.ServiceProvider.GetRequiredService<IDocumentRetentionProcessor>();
        return await PurgeAllBatchesInScopeAsync(retentionProcessor, stoppingToken);
    }

    private async Task<int> PurgeAllBatchesInScopeAsync(IDocumentRetentionProcessor retentionProcessor, CancellationToken stoppingToken)
    {
        int purged = 0;
        int purgedInBatch;
        do
        {
            purgedInBatch = await retentionProcessor.PurgeExpiredOutputsAsync(_retentionPeriod, _batchSize, stoppingToken);
            purged += purgedInBatch;
        }
        while (purgedInBatch >= _batchSize && !stoppingToken.IsCancellationRequested);

        return purged;
    }

    private static async Task<List<Guid>> ListActiveProjectIdsAsync(NpgsqlDataSource source, CancellationToken ct)
    {
        var ids = new List<Guid>();
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT project_id FROM public.fn_list_active_project_ids();";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
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

