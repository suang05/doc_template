namespace SmkDoc.Application.Common.Models;

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

public record SaveFieldMappingItem(
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

public record TemplateDatasetDto(
    Guid Id,
    Guid TemplateId,
    Guid DatasetId,
    string DatasetName,
    string Alias,
    int SortOrder
);

public record SaveTemplateDatasetItem(
    Guid DatasetId,
    string Alias,
    int SortOrder
);

/// <summary>Pre-resolved dataset context passed to FieldMappingApplicatorService.</summary>
public record ResolvedDataset(string Provider, string ConnectionString, string SqlQuery);
