using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

/// <summary>
/// DTO representing the state of an asynchronous document generation job.
/// </summary>
public sealed record DocumentJobDto(
    Guid JobId,
    string Slug,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CheckStatusUrl,
    string? DownloadUrl,
    string? Error,
    GenerateDocumentResultDto? Result)
{
    public static DocumentJobDto FromJob(DocumentJob job)
    {
        return new DocumentJobDto(
            job.JobId,
            job.Slug,
            job.Status.ToString(),
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            $"/api/v1/documents/jobs/{job.JobId}",
            job.Result?.Url,
            job.ErrorMessage,
            job.Result);
    }
}

