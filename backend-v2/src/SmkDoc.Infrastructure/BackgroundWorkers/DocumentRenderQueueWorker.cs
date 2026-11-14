using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class DocumentRenderWorkerOptions
{
    public int MaxConcurrentRenders { get; set; } = 4;
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>
/// Background worker processing asynchronous document render jobs from <see cref="IDocumentRenderQueue"/>.
/// Throttles Gotenberg Chromium conversions via <see cref="SemaphoreSlim"/> to bound CPU, RAM,
/// and socket allocation, preventing thread starvation and pod crashes during enterprise batch generation.
/// </summary>
public sealed partial class DocumentRenderQueueWorker(
    IDocumentRenderQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentRenderQueueWorker> logger,
    TimeProvider timeProvider,
    IOptions<DocumentRenderWorkerOptions>? options = null) : BackgroundService
{
    private readonly int _maxConcurrentRenders = Math.Max(1, options?.Value.MaxConcurrentRenders ?? 4);
    private readonly TimeSpan _pollDelay = options?.Value.PollDelay ?? TimeSpan.FromMilliseconds(250);
    private readonly SemaphoreSlim _throttler = new(Math.Max(1, options?.Value.MaxConcurrentRenders ?? 4));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogWorkerStarted(logger, _maxConcurrentRenders, _pollDelay.TotalMilliseconds);
        }

        var activeTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryProcessNextQueuedJobAsync(activeTasks, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is
                ExternalServiceException or
                RedisException or
                TimeoutException or
                InvalidOperationException or
                JsonException)
            {
                await HandleLoopErrorAsync(ex, stoppingToken);
            }
        }

        await DrainInFlightJobsAsync(activeTasks);
        LogWorkerStopped(logger);
    }

    private async Task TryProcessNextQueuedJobAsync(List<Task> activeTasks, CancellationToken stoppingToken)
    {
        await _throttler.WaitAsync(stoppingToken);

        var job = await TryDequeueJobAsync(stoppingToken);
        if (job == null)
        {
            _throttler.Release();
            await Task.Delay(_pollDelay, stoppingToken);
            return;
        }

        activeTasks.Add(ProcessJobSafeAsync(job, stoppingToken));
        activeTasks.RemoveAll(t => t.IsCompleted);
    }

    private async Task<DocumentJob?> TryDequeueJobAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await queue.DequeueAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDequeueError(logger, ex);
            return null;
        }
    }

    private async Task DrainInFlightJobsAsync(List<Task> activeTasks)
    {
        if (activeTasks.Count == 0)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            LogWaitingInFlightJobs(logger, activeTasks.Count);
        }

        await Task.WhenAll(activeTasks);
    }

    private async Task HandleLoopErrorAsync(Exception ex, CancellationToken stoppingToken)
    {
        LogUnexpectedLoopError(logger, ex);
        await Task.Delay(_pollDelay, stoppingToken);
    }

    private async Task ProcessJobSafeAsync(DocumentJob job, CancellationToken stoppingToken)
    {
        try
        {
            job.MarkProcessing(timeProvider.GetUtcNow());
            await queue.UpdateJobAsync(job, stoppingToken);

            using var scope = scopeFactory.CreateScope();

            // Hydrate ambient tenant & caller context
            var projectSetter = scope.ServiceProvider.GetRequiredService<ICurrentProjectContextSetter>();
            projectSetter.SetProject(new ProjectId(job.ProjectId), job.CallerIdentity);

            var useCase = scope.ServiceProvider.GetRequiredService<IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>>();

            using var payloadDoc = JsonDocument.Parse(job.PayloadJson);
            var command = new GenerateDocumentCommand(
                job.Slug,
                payloadDoc.RootElement.Clone(),
                job.OutputFormat,
                job.DocumentRef,
                job.ChangeNote,
                job.SkipValidation);

            var result = await useCase.ExecuteAsync(command, stoppingToken);

            job.MarkCompleted(result, timeProvider.GetUtcNow());
            await queue.UpdateJobAsync(job, stoppingToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                LogJobCompleted(logger, job.JobId, job.ProjectId, job.Slug);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogJobCanceled(logger, job.JobId);
        }
        catch (DomainException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (ExternalServiceException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (RedisException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (DbException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (HttpRequestException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (IOException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (JsonException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (TimeoutException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (InvalidOperationException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (ArgumentException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (FormatException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        catch (NotSupportedException ex)
        {
            await MarkJobFailedAsync(job, ex);
        }
        finally
        {
            _throttler.Release();
        }
    }

    private async Task MarkJobFailedAsync(DocumentJob job, Exception ex)
    {
        WriteJobFailed(logger, ex, job.JobId);
        job.MarkFailed(ex.Message, timeProvider.GetUtcNow());

        try
        {
            await queue.UpdateJobAsync(job, CancellationToken.None);
        }
        catch (ExternalServiceException updateEx)
        {
            WriteJobStatusUpdateFailed(logger, updateEx, job.JobId);
        }
        catch (RedisException updateEx)
        {
            WriteJobStatusUpdateFailed(logger, updateEx, job.JobId);
        }
        catch (TimeoutException updateEx)
        {
            WriteJobStatusUpdateFailed(logger, updateEx, job.JobId);
        }
        catch (InvalidOperationException updateEx)
        {
            WriteJobStatusUpdateFailed(logger, updateEx, job.JobId);
        }
    }

    public override void Dispose()
    {
        _throttler.Dispose();
        base.Dispose();
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRenderQueueWorker started with MaxConcurrentRenders={MaxConcurrentRenders}, PollDelay={PollDelayMs}ms")]
    private static partial void LogWorkerStarted(ILogger logger, int maxConcurrentRenders, double pollDelayMs);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error while dequeueing document render job from queue")]
    private static partial void LogDequeueError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRenderQueueWorker waiting for {Count} in-flight jobs to complete...")]
    private static partial void LogWaitingInFlightJobs(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentRenderQueueWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unexpected error in DocumentRenderQueueWorker loop")]
    private static partial void LogUnexpectedLoopError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Document render job {JobId} completed successfully for project {ProjectId} (Slug: {Slug})")]
    private static partial void LogJobCompleted(ILogger logger, Guid jobId, Guid projectId, string slug);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Document render job {JobId} canceled during shutdown")]
    private static partial void LogJobCanceled(ILogger logger, Guid jobId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Document render job {JobId} failed")]
    private static partial void WriteJobFailed(ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to update job status for failed job {JobId}")]
    private static partial void WriteJobStatusUpdateFailed(ILogger logger, Exception ex, Guid jobId);
}

