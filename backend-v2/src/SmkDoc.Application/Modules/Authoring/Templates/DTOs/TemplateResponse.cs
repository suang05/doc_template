namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

/// <summary>
/// Legacy Response DTO alias for templates.
/// </summary>
[Obsolete("Use TemplateResultDto instead.")]
public sealed record TemplateResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    Guid? CurrentVersionId,
    string? FileFormat,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
) : TemplateResultDto(Id, ProjectId, Name, Slug, Category, IsActive, CurrentVersionId, FileFormat, CreatedAt, UpdatedAt);

