using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a mapping between template placeholders and data source paths.
/// </summary>
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

    // For EF Core materialization
    private FieldMapping() { }

    public FieldMapping(
        Guid templateId, 
        string placeholder, 
        string sourcePath, 
        string label, 
        bool required, 
        int sortOrder, 
        DataSourceType? dataSourceType = null,
        Guid? id = null) : base(id)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainValidationException("TemplateId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(placeholder))
        {
            throw new DomainValidationException("Placeholder cannot be empty or whitespace.");
        }

        TemplateId = templateId;
        Placeholder = placeholder.Trim();
        SourcePath = (sourcePath ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        Required = required;
        SortOrder = sortOrder;
        DataSourceType = dataSourceType ?? DataSourceType.Json;
    }

    public void UpdateMappingDetails(string sourcePath, string label, bool required, string? defaultValue, string? transform, int sortOrder)
    {
        SourcePath = (sourcePath ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        Required = required;
        DefaultValue = defaultValue;
        Transform = transform;
        SortOrder = sortOrder;
        SetUpdated();
    }
    
    public void ConfigureDataSource(DataSourceType type, string? alias, string? resultPath, string? mathExpression)
    {
        DataSourceType = type ?? DataSourceType.Json;
        DatasetAlias = alias;
        ResultPath = resultPath;
        MathExpression = mathExpression;
        SetUpdated();
    }
}
