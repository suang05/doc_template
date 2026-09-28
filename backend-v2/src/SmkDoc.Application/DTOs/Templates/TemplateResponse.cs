namespace SmkDoc.Application.DTOs.Templates;

/// <summary>
/// Safe immutable Response DTO for templates, strictly isolating Domain Entities from Presentation.
/// </summary>
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
);
