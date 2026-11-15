using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

internal static class DocumentJobResultMapper
{
    internal static DocumentJobResultDto ToDto(DocumentJob job)
    {
        int totalItems = job.Items.Count;
        int succeededItems = job.Items.Count(i => i.Status == Domain.Enums.DocumentJobStatus.Succeeded);
        int failedItems = job.Items.Count(i => i.Status == Domain.Enums.DocumentJobStatus.Failed);
        string? downloadUrl = job.Status == Domain.Enums.DocumentJobStatus.Succeeded && !string.IsNullOrEmpty(job.ResultStorageKey)
            ? $"/api/v1/document-jobs/{job.Id.Value}/result"
            : null;

        return new DocumentJobResultDto(
            job.Id.Value,
            job.TemplateSlug,
            job.TemplateId.Value,
            job.TemplateVersionId,
            job.Status.Name,
            job.OutputFormat.Extension,
            job.Attempt,
            job.MaxAttempts,
            job.CancelRequested,
            totalItems,
            succeededItems,
            failedItems,
            job.ResultGenerationLogId,
            downloadUrl,
            job.LastErrorCode,
            job.LastErrorMessage,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.Version);
    }
}
