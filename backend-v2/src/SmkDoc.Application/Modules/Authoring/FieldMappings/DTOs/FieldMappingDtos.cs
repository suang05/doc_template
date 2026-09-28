using System.Text.Json;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

/// <summary>
/// DTO representing an individual template placeholder-to-datasource mapping rule.
/// </summary>
public record FieldMappingDto(
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
public record SaveFieldMappingItemDto(
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
public record TemplateDatasetDto(
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
public record SaveTemplateDatasetItemDto(
    Guid DatasetId,
    string Alias,
    int SortOrder
);

/// <summary>
/// Query for previewing field mappings with sample data.
/// </summary>
public record PreviewMappingsQuery(JsonElement SampleData);

/// <summary>
/// Pre-resolved dataset execution context passed to FieldMappingApplicatorService.
/// </summary>
public record ResolvedDatasetContext(string Provider, string ConnectionString, string SqlQuery);
