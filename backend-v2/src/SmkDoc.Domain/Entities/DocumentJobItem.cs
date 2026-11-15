using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Child work item belonging to a <see cref="DocumentJob"/> aggregate root.
/// Supports single-document (1 item) and batch-document (N items) async Pipeline C execution.
/// </summary>
public sealed class DocumentJobItem : BaseEntity
{
    public const int MaxErrorCodeLength = 64;
    public const int MaxErrorMessageLength = 1000;
    public const int MaxStorageKeyLength = 512;
    public const int MaxDocumentRefLength = 128;

    public DocumentJobId DocumentJobId { get; private set; }
    public int ItemIndex { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public string? DocumentRef { get; private set; }
    public DocumentJobStatus Status { get; private set; } = DocumentJobStatus.Queued;
    public Guid? GenerationLogId { get; private set; }
    public string? OutputStorageKey { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public DocumentJob? DocumentJob { get; internal set; }

    // For EF Core materialization only
    private DocumentJobItem() { }

    internal DocumentJobItem(
        Guid? id,
        DocumentJobId documentJobId,
        int itemIndex,
        string payloadJson,
        string? documentRef,
        DateTimeOffset now)
        : base(id, now)
    {
        if (documentJobId.IsEmpty)
        {
            throw new DomainValidationException("DocumentJobId cannot be empty.");
        }

        if (itemIndex < 0)
        {
            throw new DomainValidationException("ItemIndex cannot be negative.");
        }

        DocumentJobId = documentJobId;
        ItemIndex = itemIndex;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        DocumentRef = string.IsNullOrWhiteSpace(documentRef)
            ? null
            : Guard.MaxLength(documentRef.Trim(), nameof(DocumentRef), MaxDocumentRefLength);
        Status = DocumentJobStatus.Queued;
    }

    internal static DocumentJobItem Create(
        DocumentJobId documentJobId,
        int itemIndex,
        string payloadJson,
        string? documentRef,
        DateTimeOffset now) =>
        new(null, documentJobId, itemIndex, payloadJson, documentRef, now);

    public void MarkRunning(DateTimeOffset now)
    {
        Status = DocumentJobStatus.Running;
        ErrorCode = null;
        ErrorMessage = null;
        SetUpdated(now);
    }

    public void MarkSucceeded(Guid? generationLogId, string? outputStorageKey, DateTimeOffset now)
    {
        Status = DocumentJobStatus.Succeeded;
        GenerationLogId = generationLogId;
        OutputStorageKey = string.IsNullOrWhiteSpace(outputStorageKey) ? null : outputStorageKey.Trim();
        ErrorCode = null;
        ErrorMessage = null;
        SetUpdated(now);
    }

    public void MarkFailed(string errorCode, string errorMessage, DateTimeOffset now)
    {
        Status = DocumentJobStatus.Failed;
        ErrorCode = Truncate(errorCode, MaxErrorCodeLength);
        ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
        SetUpdated(now);
    }

    public void MarkCanceled(DateTimeOffset now)
    {
        if (Status == DocumentJobStatus.Succeeded)
        {
            return;
        }

        Status = DocumentJobStatus.Canceled;
        SetUpdated(now);
    }

    public void ResetForRetry(DateTimeOffset now)
    {
        if (Status == DocumentJobStatus.Succeeded)
        {
            return;
        }

        Status = DocumentJobStatus.Queued;
        ErrorCode = null;
        ErrorMessage = null;
        SetUpdated(now);
    }

    private static string Truncate(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
