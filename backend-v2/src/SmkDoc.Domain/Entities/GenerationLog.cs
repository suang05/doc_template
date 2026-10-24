using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an audit log entry of a document generation request.
/// </summary>
public sealed class GenerationLog : BaseEntity, IMustHaveProject
{
    public const int MaxCallerAppLength = 50;
    public const int MaxTriggerSourceLength = 20;
    public const int MaxOutputKeyLength = 500;

    public ProjectId ProjectId { get; private set; }
    public TemplateId? TemplateId { get; private set; }
    public Guid? TemplateVersionId { get; private set; }
    public ApiKeyId? ApiKeyId { get; private set; }
    public string? CallerApp { get; private set; }
    public string? TriggerSource { get; private set; }
    public string? InputData { get; private set; }
    public string? OutputKey { get; private set; }
    public OutputFormat? OutputFormat { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public int? PageCount { get; private set; }
    public Sha256Hash? PayloadHashSha256 { get; private set; }
    public int DurationMs { get; private set; }
    public GenerationStatus Status { get; private set; } = GenerationStatus.Success;
    public string? ErrorMsg { get; private set; }

    // For EF Core materialization
    private GenerationLog() { }

    internal GenerationLog(
        Guid? id,
        ProjectId projectId,
        GenerationTarget target,
        GenerationContext context,
        GenerationMetrics metrics,
        GenerationStatus status,
        string? errorMsg,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Status = Guard.NotNull(status, nameof(Status));

        Guard.NotNull(target, nameof(target));
        Guard.NotNull(context, nameof(context));
        Guard.NotNull(metrics, nameof(metrics));

        if (metrics.DurationMs < 0)
        {
            throw new DomainValidationException("DurationMs cannot be negative.");
        }

        if (metrics.FileSizeBytes.HasValue && metrics.FileSizeBytes.Value < 0)
        {
            throw new DomainValidationException("FileSizeBytes cannot be negative.");
        }

        if (metrics.PageCount.HasValue && metrics.PageCount.Value < 0)
        {
            throw new DomainValidationException("PageCount cannot be negative.");
        }

        if (context.CallerApp is { Length: > MaxCallerAppLength })
        {
            throw new DomainValidationException($"CallerApp must not exceed {MaxCallerAppLength} characters.");
        }

        if (context.TriggerSource is { Length: > MaxTriggerSourceLength })
        {
            throw new DomainValidationException($"TriggerSource must not exceed {MaxTriggerSourceLength} characters.");
        }

        if (target.OutputKey is { Length: > MaxOutputKeyLength })
        {
            throw new DomainValidationException($"OutputKey must not exceed {MaxOutputKeyLength} characters.");
        }

        TemplateId = target.TemplateId;
        TemplateVersionId = target.TemplateVersionId;
        OutputFormat = target.OutputFormat;
        OutputKey = target.OutputKey?.Trim();
        InputData = target.InputData;
        PayloadHashSha256 = target.PayloadHashSha256;

        ApiKeyId = context.ApiKeyId;
        CallerApp = context.CallerApp?.Trim();
        TriggerSource = context.TriggerSource?.Trim();

        DurationMs = metrics.DurationMs;
        FileSizeBytes = metrics.FileSizeBytes;
        PageCount = metrics.PageCount;

        ErrorMsg = errorMsg;
    }

    public static GenerationLog Create(
        ProjectId projectId,
        GenerationTarget target,
        GenerationContext context,
        GenerationMetrics metrics,
        GenerationStatus status,
        DateTimeOffset now,
        string? errorMsg = null,
        Guid? id = null) =>
        new(id, projectId, target, context, metrics, status, errorMsg, now);

    public static GenerationLog CreateSuccess(
        Guid generationId,
        ProjectId projectId,
        GenerationTarget target,
        GenerationContext context,
        GenerationMetrics metrics,
        DateTimeOffset now) =>
        new(generationId, projectId, target, context, metrics, GenerationStatus.Success, null, now);

    public static GenerationLog CreateValidationFailure(
        ProjectId projectId,
        GenerationTarget target,
        GenerationContext context,
        int durationMs,
        string errorMessage,
        DateTimeOffset now) =>
        new(null, projectId, target, context, GenerationMetrics.ForDuration(durationMs, 0), GenerationStatus.ValidationFailed, errorMessage, now);

    public static GenerationLog CreateFailure(
        ProjectId projectId,
        GenerationTarget target,
        GenerationContext context,
        int durationMs,
        string errorMessage,
        DateTimeOffset now) =>
        new(null, projectId, target, context, GenerationMetrics.ForDuration(durationMs, 0), GenerationStatus.Failed, errorMessage, now);
}
