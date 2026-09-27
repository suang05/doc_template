using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

public class FieldMapping : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public string Placeholder { get; private set; } = string.Empty;
    public string SourcePath { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public bool Required { get; private set; }
    public string? DefaultValue { get; private set; }
    public string? Transform { get; private set; }
    public int SortOrder { get; private set; }
    
    public DataSourceType DataSourceType { get; private set; } = DataSourceType.Json;
    public string? DatasetAlias { get; private set; }
    public string? ResultPath { get; private set; }
    public string? MathExpression { get; private set; }

    // Navigation property
    public virtual Template? Template { get; private set; }

    private FieldMapping() { }

    public FieldMapping(Guid templateId, string placeholder, string sourcePath, string label, bool required, int sortOrder, DataSourceType? dataSourceType = null)
    {
        TemplateId = templateId;
        Placeholder = placeholder;
        SourcePath = sourcePath;
        Label = label;
        Required = required;
        SortOrder = sortOrder;
        DataSourceType = dataSourceType ?? DataSourceType.Json;
    }

    public void UpdateMappingDetails(string sourcePath, string label, bool required, string? defaultValue, string? transform, int sortOrder)
    {
        SourcePath = sourcePath;
        Label = label;
        Required = required;
        DefaultValue = defaultValue;
        Transform = transform;
        SortOrder = sortOrder;
        SetUpdated();
    }
    
    public void ConfigureDataSource(DataSourceType type, string? alias, string? resultPath, string? mathExpression)
    {
        DataSourceType = type;
        DatasetAlias = alias;
        ResultPath = resultPath;
        MathExpression = mathExpression;
        SetUpdated();
    }
}
