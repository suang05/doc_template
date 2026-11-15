using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class DocumentJobWorkerOptions
{
    public const string SectionName = "DocumentJobWorker";

    public int PollingIntervalMilliseconds { get; set; } = 500;
    public int LeaseSeconds { get; set; } = 90;
    public int HeartbeatIntervalSeconds { get; set; } = 20;
    public int MaxConcurrency { get; set; } = 2;
}

/// <summary>
/// Asynchronous Pipeline C document job processor.
/// Claims jobs atomically via <c>FOR UPDATE SKIP LOCKED</c>, maintains lease heartbeats,
/// supports cooperative cancellation, creates batch ZIP bundles for multi-item jobs,
/// and applies jittered exponential retry backoff up to <see cref="DocumentJob.MaxAttempts"/>.
/// </summary>
public sealed partial class DocumentJobWorker(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<DocumentJobWorker> logger,
    IOptions<DocumentJobWorkerOptions>? options = null) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(Math.Max(100, options?.Value.PollingIntervalMilliseconds ?? 500));
    private readonly int _leaseSeconds = Math.Clamp(options?.Value.LeaseSeconds ?? 90, 30, 600);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(Math.Clamp(options?.Value.HeartbeatIntervalSeconds ?? 20, 5, 120));
    private readonly int _maxConcurrency = Math.Clamp(options?.Value.MaxConcurrency ?? 2, 1, 8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger, _maxConcurrency, _leaseSeconds);

        var loops = Enumerable.Range(0, _maxConcurrency)
            .Select(idx => RunWorkerLoopAsync($"{Environment.MachineName}-{Environment.ProcessId}-{idx}", stoppingToken))
            .ToArray();

        await Task.WhenAll(loops);
        LogWorkerStopped(logger);
    }

    private async Task RunWorkerLoopAsync(string workerId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool claimedWork = false;
            try
            {
                claimedWork = await ProcessSingleClaimAsync(workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException or IOException)
            {
                LogLoopFailure(logger, ex, workerId);
            }

            if (!claimedWork)
            {
                try
                {
                    await Task.Delay(_pollInterval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task<bool> ProcessSingleClaimAsync(string workerId, CancellationToken stoppingToken)
    {
        var claimed = await ClaimNextJobAsync(workerId, stoppingToken);
        if (claimed is null)
        {
            return false;
        }

        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = RunHeartbeatAsync(claimed.Value.JobId, workerId, jobCts, stoppingToken);

        try
        {
            await ExecuteClaimedJobAsync(claimed.Value.JobId, claimed.Value.ProjectId, workerId, jobCts.Token);
        }
        finally
        {
            try
            {
                await jobCts.CancelAsync();
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected on job completion.
                }
            }
            finally
            {
                jobCts.Dispose();
            }
        }

        return true;
    }

    private async Task<ClaimedJobDescriptor?> ClaimNextJobAsync(string workerId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, project_id FROM public.fn_claim_next_document_job(@p_worker_id, @p_lease_seconds);";
        cmd.Parameters.Add(new NpgsqlParameter("p_worker_id", NpgsqlDbType.Text) { Value = workerId });
        cmd.Parameters.Add(new NpgsqlParameter("p_lease_seconds", NpgsqlDbType.Integer) { Value = _leaseSeconds });

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ClaimedJobDescriptor(reader.GetGuid(0), reader.GetGuid(1));
    }

    private async Task RunHeartbeatAsync(
        Guid jobId,
        string workerId,
        CancellationTokenSource jobCts,
        CancellationToken hostStoppingToken)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);
        while (await timer.WaitForNextTickAsync(jobCts.Token))
        {
            try
            {
                await using var conn = await dataSource.OpenConnectionAsync(hostStoppingToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT renewed, cancel_requested FROM public.fn_renew_document_job_lease(@p_job_id, @p_worker_id, @p_lease_seconds);";
                cmd.Parameters.Add(new NpgsqlParameter("p_job_id", NpgsqlDbType.Uuid) { Value = jobId });
                cmd.Parameters.Add(new NpgsqlParameter("p_worker_id", NpgsqlDbType.Text) { Value = workerId });
                cmd.Parameters.Add(new NpgsqlParameter("p_lease_seconds", NpgsqlDbType.Integer) { Value = _leaseSeconds });

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, hostStoppingToken);
                if (!await reader.ReadAsync(hostStoppingToken))
                {
                    await jobCts.CancelAsync();
                    break;
                }

                bool renewed = reader.GetBoolean(0);
                bool cancelRequested = reader.GetBoolean(1);
                if (!renewed || cancelRequested)
                {
                    await jobCts.CancelAsync();
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException)
            {
                LogHeartbeatError(logger, ex, jobId);
            }
        }
    }

    private static bool IsTransientFailure(Exception ex) =>
        ex is ExternalServiceException or
            HttpRequestException or
            TimeoutException or
            DbException or
            IOException or
            InvalidOperationException;

    private async Task<bool> IsJobCanceledAsync(Guid jobId, ProjectId typedProjectId, CancellationToken ct) =>
        ct.IsCancellationRequested || await IsCancelRequestedInDbAsync(jobId, typedProjectId, CancellationToken.None);

    private async Task<(int ExitReason, string? SingleOutputKey, Guid? SingleGenerationLogId, string? FirstDeterministicError)>
        ProcessJobItemsAsync(DocumentJob job, Guid jobId, ProjectId typedProjectId, bool isSingleItemJob, CancellationToken ct)
    {
        const int completed = 0;
        const int canceled = 1;
        const int retryScheduled = 2;

        string? firstDeterministicError = null;
        string? singleOutputKey = null;
        Guid? singleGenerationLogId = null;

        foreach (var item in job.Items.OrderBy(i => i.ItemIndex))
        {
            if (await IsJobCanceledAsync(jobId, typedProjectId, ct))
            {
                return (canceled, singleOutputKey, singleGenerationLogId, firstDeterministicError);
            }

            if (item.Status == DocumentJobStatus.Succeeded)
            {
                singleOutputKey = item.OutputStorageKey;
                singleGenerationLogId = item.GenerationLogId;
                continue;
            }

            var step = await ProcessSingleItemAsync(job, item, typedProjectId, isSingleItemJob, ct);
            switch (step.Outcome)
            {
                case 1: // Canceled
                    return (canceled, singleOutputKey, singleGenerationLogId, firstDeterministicError);
                case 2: // Retry scheduled
                    return (retryScheduled, singleOutputKey, singleGenerationLogId, firstDeterministicError);
                case 3: // Deterministic failure
                    firstDeterministicError ??= step.Error;
                    if (isSingleItemJob)
                    {
                        return (completed, singleOutputKey, singleGenerationLogId, firstDeterministicError);
                    }

                    break;
                default:
                    singleOutputKey = step.OutputKey;
                    singleGenerationLogId = step.GenerationLogId;
                    break;
            }
        }

        return (completed, singleOutputKey, singleGenerationLogId, firstDeterministicError);
    }

    private async Task<(int Outcome, string? OutputKey, Guid? GenerationLogId, string? Error)>
        ProcessSingleItemAsync(DocumentJob job, DocumentJobItem item, ProjectId typedProjectId, bool isSingleItemJob, CancellationToken ct)
    {
        item.MarkRunning(timeProvider.GetUtcNow());

        try
        {
            var outcome = await RenderSingleItemAsync(job, item, typedProjectId, ct);
            item.MarkSucceeded(outcome.GenerationLogId, outcome.OutputKey, timeProvider.GetUtcNow());
            return (0, outcome.OutputKey, outcome.GenerationLogId, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (1, null, null, null);
        }
        catch (Exception ex) when (IsDeterministicFailure(ex, out string errorCode))
        {
            item.MarkFailed(errorCode, ex.Message, timeProvider.GetUtcNow());
            if (isSingleItemJob)
            {
                job.MarkFailed(errorCode, ex.Message, timeProvider.GetUtcNow());
            }

            return (3, null, null, $"{errorCode}: {ex.Message}");
        }
        catch (Exception ex) when (IsTransientFailure(ex))
        {
            var now = timeProvider.GetUtcNow();
            var nextAvailable = ComputeJitteredBackoff(now, job.Attempt);
            job.ScheduleRetry(ErrorCodes.ExternalServiceUnavailable, ex.Message, nextAvailable, now);
            LogJobRetryScheduled(logger, ex, job.Id.Value, job.Attempt, nextAvailable);
            return (2, null, null, null);
        }
    }

    private async Task ExecuteClaimedJobAsync(Guid jobId, Guid projectId, string workerId, CancellationToken ct)
    {
        var typedProjectId = new ProjectId(projectId);
        var typedJobId = new DocumentJobId(jobId);

        await using var outerScope = scopeFactory.CreateAsyncScope();
        var outerTenantScope = outerScope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = outerTenantScope.BeginTenant(typedProjectId, $"worker:{workerId}");

        var jobRepo = outerScope.ServiceProvider.GetRequiredService<IDocumentJobRepository>();
        var outerUow = outerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await jobRepo.GetByIdWithItemsAsync(typedJobId, ct);
        if (job is null)
        {
            return;
        }

        if (job.CancelRequested || ct.IsCancellationRequested)
        {
            job.MarkCanceled(timeProvider.GetUtcNow());
            jobRepo.Update(job);
            await outerUow.CommitAsync(CancellationToken.None);
            return;
        }

        bool isSingleItemJob = job.Items.Count == 1;
        var execution = await ProcessJobItemsAsync(job, jobId, typedProjectId, isSingleItemJob, ct);

        await FinalizeOuterJobAsync(
            outerScope.ServiceProvider,
            jobRepo,
            outerUow,
            job,
            execution.ExitReason == 1,
            execution.ExitReason == 2,
            isSingleItemJob,
            execution.SingleOutputKey,
            execution.SingleGenerationLogId,
            execution.FirstDeterministicError,
            ct);
    }

    private async Task<ItemRenderOutcome> RenderSingleItemAsync(
        DocumentJob job,
        DocumentJobItem item,
        ProjectId projectId,
        CancellationToken ct)
    {
        await using var itemScope = scopeFactory.CreateAsyncScope();
        var tenantScope = itemScope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = tenantScope.BeginTenant(projectId, job.CallerIdentity);

        using var jsonDoc = JsonDocument.Parse(item.PayloadJson);
        var generateUseCase = itemScope.ServiceProvider.GetRequiredService<IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>>();
        var logRepo = itemScope.ServiceProvider.GetRequiredService<IGenerationLogRepository>();

        var result = await generateUseCase.ExecuteAsync(
            new GenerateDocumentCommand(
                job.TemplateSlug,
                jsonDoc.RootElement.Clone(),
                job.OutputFormat.Extension,
                item.DocumentRef,
                ChangeNote: string.Create(CultureInfo.InvariantCulture, $"Async document job {job.Id.Value:D}")),
            ct);

        var log = await logRepo.GetByIdAsync(result.GenerationId, ct);
        return new ItemRenderOutcome(result.GenerationId, log?.OutputKey);
    }

    private async Task FinalizeOuterJobAsync(
        IServiceProvider scopedServices,
        IDocumentJobRepository jobRepo,
        IUnitOfWork outerUow,
        DocumentJob job,
        bool canceledMidJob,
        bool abortedForTransientRetry,
        bool isSingleItemJob,
        string? singleOutputKey,
        Guid? singleGenerationLogId,
        string? firstDeterministicError,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        if (canceledMidJob || await IsCancelRequestedInDbAsync(job.Id.Value, job.ProjectId, CancellationToken.None))
        {
            job.MarkCanceled(now);
        }
        else if (!abortedForTransientRetry && job.Status == DocumentJobStatus.Running)
        {
            var succeededItems = job.Items.Where(i => i.Status == DocumentJobStatus.Succeeded).ToList();
            if (succeededItems.Count == 0)
            {
                job.MarkFailed(ErrorCodes.RenderFailed, firstDeterministicError ?? "All document items failed rendering.", now);
            }
            else if (isSingleItemJob)
            {
                job.MarkSucceeded(singleOutputKey, singleGenerationLogId, now);
            }
            else
            {
                var storageService = scopedServices.GetRequiredService<IStorageService>();
                string zipKey = await CreateAndUploadBatchZipAsync(storageService, job, succeededItems, ct);
                job.MarkSucceeded(zipKey, resultGenerationLogId: null, now);
            }
        }

        try
        {
            jobRepo.Update(job);
            await outerUow.CommitAsync(CancellationToken.None);
        }
        catch (PreconditionFailedException)
        {
            // Concurrent cancellation bumped Version while worker was finishing; reload and reconcile cleanly.
            await ReconcileConcurrentCancelAsync(job.Id, job.ProjectId);
        }
    }

    private async Task ReconcileConcurrentCancelAsync(DocumentJobId jobId, ProjectId projectId)
    {
        await using var retryScope = scopeFactory.CreateAsyncScope();
        var tenantScope = retryScope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = tenantScope.BeginTenant(projectId, "system:job-reconcile");

        var repo = retryScope.ServiceProvider.GetRequiredService<IDocumentJobRepository>();
        var uow = retryScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var latest = await repo.GetByIdWithItemsAsync(jobId, CancellationToken.None);
        if (latest is null || latest.Status.IsTerminal)
        {
            return;
        }

        if (latest.CancelRequested)
        {
            latest.MarkCanceled(timeProvider.GetUtcNow());
            repo.Update(latest);
            await uow.CommitAsync(CancellationToken.None);
        }
    }

    private static async Task<string> CreateAndUploadBatchZipAsync(
        IStorageService storageService,
        DocumentJob job,
        IReadOnlyList<DocumentJobItem> succeededItems,
        CancellationToken ct)
    {
        await using var zipSpool = new HybridSpoolStream();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = new ZipArchive(zipSpool, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in succeededItems.OrderBy(i => i.ItemIndex))
            {
                if (string.IsNullOrWhiteSpace(item.OutputStorageKey))
                {
                    continue;
                }

                string entryName = BuildUniqueEntryName(item, job.OutputFormat.Extension, usedNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = await entry.OpenAsync(ct);
                await using var sourceStream = await storageService.DownloadAsync(StorageBuckets.Outputs, item.OutputStorageKey, ct);
                await sourceStream.CopyToAsync(entryStream, ct);
            }
        }

        zipSpool.Position = 0;
        string zipStorageKey = $"jobs/{job.ProjectId.Value:N}/{job.Id.Value:N}/result.zip";
        await storageService.UploadAsync(StorageBuckets.Outputs, zipStorageKey, zipSpool, "application/zip", ct);
        return zipStorageKey;
    }

    private static string BuildUniqueEntryName(DocumentJobItem item, string extension, HashSet<string> usedNames)
    {
        string rawBase = string.IsNullOrWhiteSpace(item.DocumentRef)
            ? string.Create(CultureInfo.InvariantCulture, $"item-{item.ItemIndex + 1:D4}")
            : Path.GetFileName(item.DocumentRef.Trim());

        if (string.IsNullOrWhiteSpace(rawBase))
        {
            rawBase = string.Create(CultureInfo.InvariantCulture, $"item-{item.ItemIndex + 1:D4}");
        }

        string candidate = $"{rawBase}.{extension}";
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        string disambiguated = string.Create(CultureInfo.InvariantCulture, $"{rawBase}-{item.ItemIndex + 1:D4}.{extension}");
        usedNames.Add(disambiguated);
        return disambiguated;
    }

    private async Task<bool> IsCancelRequestedInDbAsync(Guid jobId, ProjectId projectId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.current_project_id', @p_pid, true);";
            setCmd.Parameters.AddWithValue("p_pid", projectId.Value.ToString("D"));
            await setCmd.ExecuteNonQueryAsync(ct);
        }

        await using var queryCmd = conn.CreateCommand();
        queryCmd.Transaction = tx;
        queryCmd.CommandText = "SELECT cancel_requested FROM public.document_jobs WHERE id = @p_id LIMIT 1;";
        queryCmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = jobId });
        var result = await queryCmd.ExecuteScalarAsync(ct);
        await tx.CommitAsync(ct);
        return result is true;
    }

    private static bool IsDeterministicFailure(Exception ex, out string errorCode)
    {
        switch (ex)
        {
            case SchemaValidationException:
                errorCode = ErrorCodes.SchemaValidationFailed;
                return true;
            case ValidationException:
                errorCode = ErrorCodes.ValidationFailed;
                return true;
            case NotFoundException:
                errorCode = ErrorCodes.NotFound;
                return true;
            case BusinessRuleViolationException brv:
                errorCode = brv.ErrorCode;
                return true;
            case SecurityViolationException sv:
                errorCode = sv.ErrorCode;
                return true;
            case DomainException de:
                errorCode = de.ErrorCode;
                return true;
            default:
                errorCode = ErrorCodes.RenderFailed;
                return false;
        }
    }

    private static DateTimeOffset ComputeJitteredBackoff(DateTimeOffset now, int attempt)
    {
        int cappedAttempt = Math.Clamp(attempt, 1, 6);
        double baseSeconds = Math.Pow(2d, cappedAttempt) * 2d; // 4s, 8s, 16s, 32s, 64s, 128s
        int jitterMs = RandomNumberGenerator.GetInt32(0, 1500);
        return now.AddSeconds(baseSeconds).AddMilliseconds(jitterMs);
    }

    private readonly record struct ClaimedJobDescriptor(Guid JobId, Guid ProjectId);
    private readonly record struct ItemRenderOutcome(Guid GenerationLogId, string? OutputKey);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentJobWorker started with MaxConcurrency={Concurrency}, LeaseSeconds={LeaseSeconds}.")]
    private static partial void LogWorkerStarted(ILogger logger, int concurrency, int leaseSeconds);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentJobWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "DocumentJobWorker {WorkerId} encountered an infrastructure error in polling loop.")]
    private static partial void LogLoopFailure(ILogger logger, Exception ex, string workerId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DocumentJobWorker failed to renew lease heartbeat for job {JobId}.")]
    private static partial void LogHeartbeatError(ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Document job {JobId} failed attempt {Attempt} transiently; scheduled retry at {NextAvailableAt}.")]
    private static partial void LogJobRetryScheduled(ILogger logger, Exception ex, Guid jobId, int attempt, DateTimeOffset nextAvailableAt);
}
