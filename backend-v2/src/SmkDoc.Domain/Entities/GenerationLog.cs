using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an audit log entry of a document generation request.
/// </summary>
public sealed class GenerationLog : BaseEntity
{
    public const int MaxCallerAppLength = 50;
    public const int MaxTriggerSourceLength = 20;
    public const int MaxOutputKeyLength = 500;

    public Guid? TemplateId { get; private set; }
    public Guid? TemplateVersionId { get; private set; }
    public Guid? ApiKeyId { get; private set; }
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
        Guid? templateId,
        Guid? templateVersionId,
        Guid? apiKeyId,
        string? callerApp,
        string? triggerSource,
        string? inputData,
        string? outputKey,
        OutputFormat? outputFormat,
        long? fileSizeBytes,
        int? pageCount,
        Sha256Hash? payloadHashSha256,
        int durationMs,
        GenerationStatus status,
        string? errorMsg,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        if (durationMs < 0)
        {
            throw new DomainValidationException("DurationMs cannot be negative.");
        }

        if (fileSizeBytes.HasValue && fileSizeBytes.Value < 0)
        {
            throw new DomainValidationException("FileSizeBytes cannot be negative.");
        }

        if (pageCount.HasValue && pageCount.Value < 0)
        {
            throw new DomainValidationException("PageCount cannot be negative.");
        }

        Status = Guard.NotNull(status, nameof(Status));

        if (callerApp is { Length: > MaxCallerAppLength })
        {
            throw new DomainValidationException($"CallerApp must not exceed {MaxCallerAppLength} characters.");
        }

        if (triggerSource is { Length: > MaxTriggerSourceLength })
        {
            throw new DomainValidationException($"TriggerSource must not exceed {MaxTriggerSourceLength} characters.");
        }

        if (outputKey is { Length: > MaxOutputKeyLength })
        {
            throw new DomainValidationException($"OutputKey must not exceed {MaxOutputKeyLength} characters.");
        }

        TemplateId = templateId;
        TemplateVersionId = templateVersionId;
        ApiKeyId = apiKeyId;
        CallerApp = callerApp?.Trim();
        TriggerSource = triggerSource?.Trim();
        InputData = inputData;
        OutputKey = outputKey?.Trim();
        OutputFormat = outputFormat;
        FileSizeBytes = fileSizeBytes;
        PageCount = pageCount;
        PayloadHashSha256 = payloadHashSha256;
        DurationMs = durationMs;
        ErrorMsg = errorMsg;
    }

    public static GenerationLog Create(
        Guid? templateId,
        Guid? templateVersionId,
        Guid? apiKeyId,
        string? callerApp,
        string? triggerSource,
        string? inputData,
        string? outputKey,
        OutputFormat? outputFormat,
        long? fileSizeBytes,
        int? pageCount,
        Sha256Hash? payloadHashSha256,
        int durationMs,
        GenerationStatus status,
        string? errorMsg,
        DateTimeOffset now) =>
        new(
            null,
            templateId,
            templateVersionId,
            apiKeyId,
            callerApp,
            triggerSource,
            inputData,
            outputKey,
            outputFormat,
            fileSizeBytes,
            pageCount,
            payloadHashSha256,
            durationMs,
            status,
            errorMsg,
            now);

    public static GenerationLog CreateSuccess(
        Guid generationId,
        Guid templateId,
        Guid templateVersionId,
        Guid? apiKeyId,
        string? callerApp,
        string triggerSource,
        string inputData,
        string outputKey,
        OutputFormat outputFormat,
        long fileSizeBytes,
        int durationMs,
        DateTimeOffset now) =>
        new(
            generationId,
            templateId,
            templateVersionId,
            apiKeyId,
            callerApp,
            triggerSource,
            inputData,
            outputKey,
            outputFormat,
            fileSizeBytes,
            null,
            null,
            durationMs,
            GenerationStatus.Success,
            null,
            now);

    public static GenerationLog CreateValidationFailure(
        Guid templateId,
        Guid templateVersionId,
        Guid? apiKeyId,
        string? callerApp,
        string triggerSource,
        string inputData,
        OutputFormat outputFormat,
        int durationMs,
        string errorMessage,
        DateTimeOffset now) =>
        new(
            null,
            templateId,
            templateVersionId,
            apiKeyId,
            callerApp,
            triggerSource,
            inputData,
            null,
            outputFormat,
            0,
            null,
            null,
            durationMs,
            GenerationStatus.ValidationFailed,
            errorMessage,
            now);

    public static GenerationLog CreateFailure(
        Guid templateId,
        Guid templateVersionId,
        Guid? apiKeyId,
        string? callerApp,
        string triggerSource,
        string inputData,
        OutputFormat outputFormat,
        int durationMs,
        string errorMessage,
        DateTimeOffset now) =>
        new(
            null,
            templateId,
            templateVersionId,
            apiKeyId,
            callerApp,
            triggerSource,
            inputData,
            null,
            outputFormat,
            0,
            null,
            null,
            durationMs,
            GenerationStatus.Failed,
            errorMessage,
            now);
}
