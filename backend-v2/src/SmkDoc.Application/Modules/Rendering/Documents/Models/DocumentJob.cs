using SmkDoc.Application.Modules.Rendering.Documents.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Documents.Models;

public sealed class DocumentJob
{
    public Guid JobId { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public string CallerIdentity { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string OutputFormat { get; set; } = "pdf";
    public string? DocumentRef { get; set; }
    public string? ChangeNote { get; set; }
    public bool SkipValidation { get; set; }
    public DocumentJobStatus Status { get; set; } = DocumentJobStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public GenerateDocumentResultDto? Result { get; set; }

    public DocumentJob() { }

    public DocumentJob(
        Guid jobId,
        Guid projectId,
        string callerIdentity,
        string slug,
        string payloadJson,
        string outputFormat,
        string? documentRef,
        string? changeNote,
        bool skipValidation,
        DateTimeOffset createdAt)
    {
        JobId = jobId;
        ProjectId = projectId;
        CallerIdentity = callerIdentity;
        Slug = slug;
        PayloadJson = payloadJson;
        OutputFormat = outputFormat;
        DocumentRef = documentRef;
        ChangeNote = changeNote;
        SkipValidation = skipValidation;
        Status = DocumentJobStatus.Queued;
        CreatedAt = createdAt;
    }

    public void MarkProcessing(DateTimeOffset startedAt)
    {
        Status = DocumentJobStatus.Processing;
        StartedAt = startedAt;
    }

    public void MarkCompleted(GenerateDocumentResultDto result, DateTimeOffset completedAt)
    {
        Status = DocumentJobStatus.Completed;
        Result = result;
        CompletedAt = completedAt;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset completedAt)
    {
        Status = DocumentJobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = completedAt;
    }
}
