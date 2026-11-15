using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Aggregate root representing a durable asynchronous Pipeline C document rendering job persisted in PostgreSQL.
/// Supports worker lease claiming (<c>FOR UPDATE SKIP LOCKED</c>), heartbeats, cooperative cancellation, and retries.
/// </summary>
public sealed class DocumentJob : AggregateRoot<DocumentJobId>, IMustHaveProject
{
    public const int DefaultMaxAttempts = 3;
    public const int MaxTemplateSlugLength = 128;
    public const int MaxCallerIdentityLength = 128;
    public const int MaxIdempotencyKeyLength = 128;
    public const int MaxWebhookUrlLength = 1024;
    public const int MaxErrorCodeLength = 64;
    public const int MaxErrorMessageLength = 1000;

    private readonly List<DocumentJobItem> _items = [];

    public ProjectId ProjectId { get; private set; }
    public TemplateId TemplateId { get; private set; }
    public string TemplateSlug { get; private set; } = string.Empty;
    public Guid? TemplateVersionId { get; private set; }
    public string CallerIdentity { get; private set; } = string.Empty;
    public string? IdempotencyKey { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public OutputFormat OutputFormat { get; private set; } = OutputFormat.Pdf;
    public Uri? WebhookUrl { get; private set; }
    public DocumentJobStatus Status { get; private set; } = DocumentJobStatus.Queued;
    public int Attempt { get; private set; }
    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;
    public DateTimeOffset AvailableAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public string? LockedBy { get; private set; }
    public bool CancelRequested { get; private set; }
    public Guid? ResultGenerationLogId { get; private set; }
    public string? ResultStorageKey { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<DocumentJobItem> Items => _items.AsReadOnly();

    // For EF Core materialization only
    private DocumentJob() { }

    private DocumentJob(
        DocumentJobId id,
        ProjectId projectId,
        TemplateId templateId,
        string templateSlug,
        Guid? templateVersionId,
        string callerIdentity,
        string? idempotencyKey,
        string? requestFingerprint,
        OutputFormat outputFormat,
        Uri? webhookUrl,
        int maxAttempts,
        DateTimeOffset now)
        : base(id, now)
    {
        if (projectId.IsEmpty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        if (templateId.IsEmpty)
        {
            throw new DomainValidationException("TemplateId cannot be empty.");
        }

        ProjectId = projectId;
        TemplateId = templateId;
        TemplateSlug = Guard.MaxLength(Guard.NotBlank(templateSlug, nameof(TemplateSlug)), nameof(TemplateSlug), MaxTemplateSlugLength);
        TemplateVersionId = templateVersionId;
        CallerIdentity = Guard.MaxLength(
            string.IsNullOrWhiteSpace(callerIdentity) ? "system" : callerIdentity.Trim(),
            nameof(CallerIdentity),
            MaxCallerIdentityLength);
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : Guard.MaxLength(idempotencyKey.Trim(), nameof(IdempotencyKey), MaxIdempotencyKeyLength);
        RequestFingerprint = string.IsNullOrWhiteSpace(requestFingerprint) ? null : requestFingerprint.Trim();
        OutputFormat = Guard.NotNull(outputFormat, nameof(OutputFormat));
        if (webhookUrl is not null)
        {
            if (!webhookUrl.IsAbsoluteUri || (webhookUrl.Scheme != Uri.UriSchemeHttp && webhookUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new DomainValidationException("WebhookUrl must be an absolute HTTP or HTTPS URI.");
            }

            if (webhookUrl.ToString().Length > MaxWebhookUrlLength)
            {
                throw new DomainValidationException($"WebhookUrl cannot exceed {MaxWebhookUrlLength} characters.");
            }
        }

        WebhookUrl = webhookUrl;
        MaxAttempts = Guard.Positive(maxAttempts, nameof(MaxAttempts));
        Status = DocumentJobStatus.Queued;
        Attempt = 0;
        AvailableAt = now;
    }

    public static DocumentJob Create(
        ProjectId projectId,
        TemplateId templateId,
        string templateSlug,
        Guid? templateVersionId,
        string callerIdentity,
        OutputFormat outputFormat,
        IReadOnlyList<(string PayloadJson, string? DocumentRef)> items,
        DateTimeOffset now,
        Uri? webhookUrl = null,
        string? idempotencyKey = null,
        string? requestFingerprint = null,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (items is null || items.Count == 0)
        {
            throw new DomainValidationException("At least one render item is required for a document job.");
        }

        var job = new DocumentJob(
            DocumentJobId.New(),
            projectId,
            templateId,
            templateSlug,
            templateVersionId,
            callerIdentity,
            idempotencyKey,
            requestFingerprint,
            outputFormat,
            webhookUrl,
            maxAttempts,
            now);

        for (int i = 0; i < items.Count; i++)
        {
            job._items.Add(DocumentJobItem.Create(job.Id, i, items[i].PayloadJson, items[i].DocumentRef, now));
        }

        return job;
    }

    public void Claim(string workerId, DateTimeOffset leaseUntil, DateTimeOffset now)
    {
        var normalizedWorker = Guard.NotBlank(workerId, nameof(workerId));
        if (leaseUntil <= now)
        {
            throw new DomainValidationException("Lease expiration must be in the future.");
        }

        if (Status != DocumentJobStatus.Queued &&
            !(Status == DocumentJobStatus.Running && LockedUntil.HasValue && LockedUntil.Value <= now))
        {
            throw new BusinessRuleViolationException(
                $"Cannot claim document job '{Id}' in status '{Status.Name}'.",
                ErrorCodes.ResourceConflict);
        }

        Status = DocumentJobStatus.Running;
        Attempt++;
        LockedBy = normalizedWorker;
        LockedUntil = leaseUntil;
        StartedAt ??= now;
        SetUpdated(now);
    }

    public void RenewLease(string workerId, DateTimeOffset leaseUntil, DateTimeOffset now)
    {
        if (Status != DocumentJobStatus.Running || !string.Equals(LockedBy, workerId, StringComparison.Ordinal))
        {
            throw new BusinessRuleViolationException(
                $"Cannot renew lease for document job '{Id}' held by '{LockedBy}'.",
                ErrorCodes.ResourceConflict);
        }

        if (leaseUntil <= now)
        {
            throw new DomainValidationException("Lease expiration must be in the future.");
        }

        LockedUntil = leaseUntil;
        SetUpdated(now);
    }

    public void MarkSucceeded(string? resultStorageKey, Guid? resultGenerationLogId, DateTimeOffset now)
    {
        if (Status != DocumentJobStatus.Running && Status != DocumentJobStatus.Queued)
        {
            throw new BusinessRuleViolationException(
                $"Cannot mark document job '{Id}' as Succeeded from '{Status.Name}'.",
                ErrorCodes.ResourceConflict);
        }

        Status = DocumentJobStatus.Succeeded;
        ResultStorageKey = string.IsNullOrWhiteSpace(resultStorageKey) ? null : resultStorageKey.Trim();
        ResultGenerationLogId = resultGenerationLogId;
        LockedUntil = null;
        LockedBy = null;
        LastErrorCode = null;
        LastErrorMessage = null;
        CompletedAt = now;
        SetUpdated(now);

        AddDomainEvent(new DocumentJobSucceededDomainEvent(
            Id,
            ProjectId,
            TemplateSlug,
            OutputFormat.Extension,
            WebhookUrl,
            ResultStorageKey,
            ResultGenerationLogId,
            now));
    }

    public void ScheduleRetry(string errorCode, string errorMessage, DateTimeOffset nextAvailableAt, DateTimeOffset now)
    {
        if (Attempt >= MaxAttempts)
        {
            MarkFailed(errorCode, errorMessage, now);
            return;
        }

        Status = DocumentJobStatus.Queued;
        AvailableAt = nextAvailableAt;
        LockedUntil = null;
        LockedBy = null;
        LastErrorCode = Truncate(errorCode, MaxErrorCodeLength);
        LastErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
        SetUpdated(now);
    }

    public void MarkFailed(string errorCode, string errorMessage, DateTimeOffset now)
    {
        if (Status == DocumentJobStatus.Succeeded || Status == DocumentJobStatus.Canceled)
        {
            return;
        }

        Status = DocumentJobStatus.Failed;
        LockedUntil = null;
        LockedBy = null;
        LastErrorCode = Truncate(errorCode, MaxErrorCodeLength);
        LastErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
        CompletedAt = now;
        SetUpdated(now);

        AddDomainEvent(new DocumentJobFailedDomainEvent(
            Id,
            ProjectId,
            TemplateSlug,
            OutputFormat.Extension,
            WebhookUrl,
            LastErrorCode ?? ErrorCodes.RenderFailed,
            LastErrorMessage ?? "Render failed.",
            now));
    }

    public void RequestCancel(DateTimeOffset now)
    {
        if (Status.IsTerminal)
        {
            throw new BusinessRuleViolationException(
                $"Cannot cancel document job '{Id}' because it is already in terminal state '{Status.Name}'.",
                ErrorCodes.ResourceConflict);
        }

        if (Status == DocumentJobStatus.Queued)
        {
            MarkCanceled(now);
            return;
        }

        if (CancelRequested)
        {
            return;
        }

        CancelRequested = true;
        SetUpdated(now);
    }

    public void MarkCanceled(DateTimeOffset now)
    {
        if (Status == DocumentJobStatus.Canceled)
        {
            return;
        }

        if (Status == DocumentJobStatus.Succeeded)
        {
            throw new BusinessRuleViolationException(
                $"Cannot cancel already succeeded document job '{Id}'.",
                ErrorCodes.ResourceConflict);
        }

        Status = DocumentJobStatus.Canceled;
        CancelRequested = true;
        LockedUntil = null;
        LockedBy = null;
        CompletedAt = now;
        foreach (var item in _items)
        {
            item.MarkCanceled(now);
        }

        SetUpdated(now);
        AddDomainEvent(new DocumentJobCanceledDomainEvent(Id, ProjectId, WebhookUrl, now));
    }

    public void RequestRetry(DateTimeOffset now)
    {
        if (Status != DocumentJobStatus.Failed && Status != DocumentJobStatus.Canceled)
        {
            throw new BusinessRuleViolationException(
                $"Only Failed or Canceled document jobs can be retried; current status is '{Status.Name}'.",
                ErrorCodes.ResourceConflict);
        }

        Status = DocumentJobStatus.Queued;
        Attempt = 0;
        AvailableAt = now;
        LockedUntil = null;
        LockedBy = null;
        CancelRequested = false;
        LastErrorCode = null;
        LastErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;

        foreach (var item in _items)
        {
            item.ResetForRetry(now);
        }

        SetUpdated(now);
    }

    private static string Truncate(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
