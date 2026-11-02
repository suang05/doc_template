namespace SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

/// <summary>
/// DTO representing an individual template placeholder-to-datasource mapping rule.
/// </summary>
public sealed record FieldMappingDto(
    Guid Id,
    Guid TemplateId,
    string Placeholder,
    string SourcePath,
    string Label,
    bool Required,
    string? DefaultValue,
    string? Transform,
    int SortOrder,
    string DataSourceType,
    string? DatasetAlias,
    string? ResultPath,
    string? MathExpression
);

/// <summary>
/// Item DTO for saving/updating field mapping configurations.
/// </summary>
public sealed record SaveFieldMappingItemDto(
    string Placeholder,
    string SourcePath,
    string Label,
    bool Required,
    string? DefaultValue,
    string? Transform,
    int SortOrder,
    string DataSourceType = "json",
    string? DatasetAlias = null,
    string? ResultPath = null,
    string? MathExpression = null
);

/// <summary>
/// DTO representing a dataset attached to a template with an alias.
/// </summary>
public sealed record TemplateDatasetDto(
    Guid Id,
    Guid TemplateId,
    Guid DatasetId,
    string DatasetName,
    string Alias,
    int SortOrder
);

/// <summary>
/// Item DTO for linking datasets to templates.
/// </summary>
public sealed record SaveTemplateDatasetItemDto(
    Guid DatasetId,
    string Alias,
    int SortOrder
);

/// <summary>
/// Pre-resolved dataset execution context passed to FieldMappingApplicatorService.
/// </summary>
public sealed record ResolvedDatasetContext(string Provider, string ConnectionString, string SqlQuery);
