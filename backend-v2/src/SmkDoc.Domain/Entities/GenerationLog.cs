using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an audit log entry of a document generation request.
/// </summary>
public class GenerationLog : BaseEntity
{
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
    public string Status { get; private set; } = "SUCCESS";
    public string? ErrorMsg { get; private set; }

    // Navigation properties
    public virtual Template? Template { get; private set; }
    public virtual TemplateVersion? TemplateVersion { get; private set; }
    public virtual ApiKey? ApiKey { get; private set; }

    // For EF Core materialization
    private GenerationLog() { }

    public GenerationLog(
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
        string status, 
        string? errorMsg,
        Guid? id = null)
        : base(id)
    {
        if (durationMs < 0)
        {
            throw new DomainValidationException("DurationMs cannot be negative.");
        }

        if (fileSizeBytes.HasValue && fileSizeBytes.Value < 0)
        {
            throw new DomainValidationException("FileSizeBytes cannot be negative.");
        }

        TemplateId = templateId;
        TemplateVersionId = templateVersionId;
        ApiKeyId = apiKeyId;
        CallerApp = callerApp;
        TriggerSource = triggerSource;
        InputData = inputData;
        OutputKey = outputKey;
        OutputFormat = outputFormat;
        FileSizeBytes = fileSizeBytes;
        PageCount = pageCount;
        PayloadHashSha256 = payloadHashSha256;
        DurationMs = durationMs;
        Status = string.IsNullOrWhiteSpace(status) ? "SUCCESS" : status.Trim();
        ErrorMsg = errorMsg;
    }
}
