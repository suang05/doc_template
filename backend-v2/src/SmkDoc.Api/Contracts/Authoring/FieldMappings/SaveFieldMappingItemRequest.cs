namespace SmkDoc.Api.Contracts.Authoring.FieldMappings;

public record SaveFieldMappingItemRequest(
    string Placeholder,
    string SourcePath,
    string Label,
    bool Required = false,
    string? DefaultValue = null,
    string? Transform = null,
    int SortOrder = 0,
    string DataSourceType = "json",
    string? DatasetAlias = null,
    string? ResultPath = null,
    string? MathExpression = null
);
