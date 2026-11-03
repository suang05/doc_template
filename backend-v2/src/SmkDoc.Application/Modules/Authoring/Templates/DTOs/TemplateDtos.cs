using SmkDoc.Domain.Enums;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

/// <summary>
/// DTO representing an immutable version of a template file and its configuration snapshot.
/// </summary>
public record TemplateVersionDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string StorageKey,
    string Status,
    string? FileFormat,
    string? DataSchema,
    string? SamplePayload,
    string? MappingsSnapshot,
    string? CommitMessage,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

/// <summary>
/// Bundle DTO loaded by the Monaco Web Studio editor.
/// </summary>
public record TemplateStudioDto(
    string Html,
    string? SamplePayload,
    string? DataSchema,
    int Version
);

/// <summary>
/// Schema contract DTO for external API consumers and form builders.
/// </summary>
public record TemplateSchemaDto(
    Guid TemplateId,
    string Slug,
    TemplateFormat? Format,
    int Version,
    string? DataSchema,
    string? SamplePayload
);

/// <summary>
/// Syntax validation and placeholder extraction result.
/// </summary>
public record TemplateValidationResultDto(
    bool Valid,
    List<string> Fields,
    List<string> Errors
);

/// <summary>
/// Tenant-scoped metadata entry for an uploaded template draft whose binary payload is stored in StorageBuckets.Drafts.
/// Contains zero byte[] fields to guarantee zero Large Object Heap allocations ([TRAP-LOH-001]).
/// </summary>
public sealed record TemplateDraftEntry(
    string DraftId,
    Guid ProjectId,
    string StorageKey,
    string FileName,
    string FileExtension,
    IReadOnlyList<string> Placeholders
);

/// <summary>
/// Result returned after step 1 (parsing) of draft upload.
/// </summary>
public record ParseDraftResultDto(
    string DraftId,
    IReadOnlyList<string> Placeholders
);
