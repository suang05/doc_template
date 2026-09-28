using SmkDoc.Domain.Enums;
using SmkDoc.Application.DTOs.FieldMappings;

namespace SmkDoc.Application.DTOs.Templates;

/// <summary>
/// Summary DTO for templates.
/// </summary>
public record TemplateDto(
    Guid Id,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    Guid? CurrentVersionId,
    TemplateFormat? FileFormat,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

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
/// Command for creating a new template.
/// </summary>
public record CreateTemplateCommand(
    string Name,
    string Slug,
    string? Category,
    string? HtmlContent
);

/// <summary>
/// Command for updating template metadata.
/// </summary>
public record UpdateTemplateMetadataCommand(
    string? Name,
    string? Category,
    bool? IsActive
);

/// <summary>
/// Command for persisting updated HTML code from Monaco Studio.
/// </summary>
public record SaveTemplateHtmlCommand(
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null
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
/// In-memory cache entry for uploaded template drafts.
/// </summary>
public record TemplateDraftEntry(
    byte[] FileBytes,
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

/// <summary>
/// Query for previewing a draft template with test JSON data.
/// </summary>
public record PreviewDraftQuery(string DataJson);

/// <summary>
/// Command for committing a draft template to persistent storage and database.
/// </summary>
public record CommitDraftCommand(
    string Name,
    string Slug,
    string? Category,
    IReadOnlyList<SaveFieldMappingItemDto> Mappings
);
