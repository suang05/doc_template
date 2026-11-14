using System.Diagnostics.CodeAnalysis;
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

