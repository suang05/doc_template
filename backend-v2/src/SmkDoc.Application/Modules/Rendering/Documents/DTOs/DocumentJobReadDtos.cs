using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

/// <summary>
/// Canonical Pipeline C document job projection implementing optimistic concurrency versioning (<see cref="IHasVersion"/>).
/// </summary>
public sealed record DocumentJobResultDto(
    Guid JobId,
    string TemplateSlug,
    Guid TemplateId,
    Guid? TemplateVersionId,
    string Status,
    string OutputFormat,
    int Attempt,
    int MaxAttempts,
    bool CancelRequested,
    int TotalItems,
    int SucceededItems,
    int FailedItems,
    Guid? ResultGenerationLogId,
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract supporting relative API download paths.")]
    string? DownloadUrl,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract supporting relative API download paths.")]
    public string? DownloadUrl { get; init; } = DownloadUrl;

    [JsonIgnore]
    public Guid ResourceId => JobId;
}

public sealed record DocumentJobItemDto(
    Guid Id,
    int ItemIndex,
    string? DocumentRef,
    string Status,
    Guid? GenerationLogId,
    string? OutputStorageKey,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DocumentJobListResult(
    IReadOnlyList<DocumentJobResultDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record DocumentJobArtifactDescriptor(
    Guid JobId,
    string StorageKey,
    string ContentType,
    string FileName);
