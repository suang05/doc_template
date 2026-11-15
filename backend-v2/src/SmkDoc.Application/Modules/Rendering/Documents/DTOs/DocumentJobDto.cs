using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

/// <summary>
/// Legacy DTO representing the state of an asynchronous document generation job (preserved for Phase 4 A1 coexistence).
/// </summary>
public sealed record DocumentJobDto(
    Guid JobId,
    string Slug,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    string? CheckStatusUrl,
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    string? DownloadUrl,
    string? Error,
    GenerateDocumentResultDto? Result)
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    public string? CheckStatusUrl { get; init; } = CheckStatusUrl;

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    public string? DownloadUrl { get; init; } = DownloadUrl;

    public static DocumentJobDto FromDomainJob(DocumentJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new DocumentJobDto(
            job.Id.Value,
            job.TemplateSlug,
            job.Status.Name,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            $"/api/v1/documents/jobs/{job.Id.Value}",
            job.Status == Domain.Enums.DocumentJobStatus.Succeeded && !string.IsNullOrEmpty(job.ResultStorageKey)
                ? $"/api/v1/document-jobs/{job.Id.Value}/result"
                : null,
            job.LastErrorMessage,
            null);
    }

    public static DocumentJobDto FromReadModel(DocumentJobResultDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new DocumentJobDto(
            dto.JobId,
            dto.TemplateSlug,
            dto.Status,
            dto.CreatedAt,
            dto.StartedAt,
            dto.CompletedAt,
            $"/api/v1/documents/jobs/{dto.JobId}",
            dto.DownloadUrl,
            dto.ErrorMessage,
            null);
    }
}
