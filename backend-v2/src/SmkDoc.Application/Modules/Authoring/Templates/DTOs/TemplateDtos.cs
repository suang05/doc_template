using System.Globalization;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

/// <summary>
/// DTO representing an immutable version of a template file and its configuration snapshot.
/// </summary>
public sealed record TemplateVersionDto(
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
public sealed record TemplateStudioDto(
    string Html,
    string? SamplePayload,
    string? DataSchema,
    int Version,
    TemplatePageSettings? PageSettings = null
);

/// <summary>
/// Schema contract DTO for external API consumers and form builders.
/// </summary>
public sealed record TemplateSchemaDto(
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
public sealed record TemplateValidationResultDto(
    bool Valid,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> Errors
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
public sealed record ParseDraftResultDto(
    string DraftId,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<TemplatePlaceholder>? DetailedPlaceholders = null
);

/// <summary>
/// Result returned after scanning placeholders from a template stream or stored template.
/// </summary>
public sealed record ScanFieldsResultDto(
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<TemplatePlaceholder>? DetailedPlaceholders = null
);

/// <summary>
/// Result returned after committing a template draft to permanent storage.
/// </summary>
public sealed record CommitDraftResultDto(
    Guid TemplateId
);

/// <summary>
/// Result returned after saving HTML in Monaco Studio.
/// </summary>
public sealed record SaveHtmlResultDto(
    int Version
) : IHasConcurrencyToken
{
    string? IHasConcurrencyToken.ConcurrencyToken => Version.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Result returned after rolling back a template version.
/// </summary>
public sealed record RollbackVersionResultDto(
    int Version
);
