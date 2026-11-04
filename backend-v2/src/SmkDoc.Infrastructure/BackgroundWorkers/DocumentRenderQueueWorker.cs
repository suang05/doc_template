using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Domain.ValueObjects.Ids;

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
public sealed class DocumentRenderQueueWorker(
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
        logger.LogInformation(
            "DocumentRenderQueueWorker started with MaxConcurrentRenders={MaxConcurrentRenders}, PollDelay={PollDelayMs}ms",
            _maxConcurrentRenders,
            _pollDelay.TotalMilliseconds);

        var activeTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _throttler.WaitAsync(stoppingToken);

                DocumentJob? job = null;
                try
                {
                    job = await queue.DequeueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error while dequeueing document render job from queue");
                    _throttler.Release();
                    await Task.Delay(_pollDelay, stoppingToken);
                    continue;
                }

                if (job == null)
                {
                    _throttler.Release();
                    await Task.Delay(_pollDelay, stoppingToken);
                    continue;
                }

                // Process job asynchronously with bounded concurrency
                var jobTask = ProcessJobSafeAsync(job, stoppingToken);
                activeTasks.Add(jobTask);

                // Clean up completed tasks
                activeTasks.RemoveAll(t => t.IsCompleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in DocumentRenderQueueWorker loop");
                await Task.Delay(_pollDelay, stoppingToken);
            }
        }

        // Wait for in-flight jobs on graceful shutdown
        if (activeTasks.Count > 0)
        {
            logger.LogInformation("DocumentRenderQueueWorker waiting for {Count} in-flight jobs to complete...", activeTasks.Count);
            await Task.WhenAll(activeTasks);
        }

        logger.LogInformation("DocumentRenderQueueWorker stopped.");
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

            logger.LogInformation(
                "Document render job {JobId} completed successfully for project {ProjectId} (Slug: {Slug})",
                job.JobId,
                job.ProjectId,
                job.Slug);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Document render job {JobId} canceled during shutdown", job.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Document render job {JobId} failed", job.JobId);
            job.MarkFailed(ex.Message, timeProvider.GetUtcNow());

            try
            {
                await queue.UpdateJobAsync(job, CancellationToken.None);
            }
            catch (Exception updateEx)
            {
                logger.LogError(updateEx, "Failed to update job status for failed job {JobId}", job.JobId);
            }
        }
        finally
        {
            _throttler.Release();
        }
    }

    public override void Dispose()
    {
        _throttler.Dispose();
        base.Dispose();
    }
}

