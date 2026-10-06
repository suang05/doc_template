using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a mapping between template placeholders and data source paths.
/// </summary>
public sealed class FieldMapping : BaseEntity
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
    public DatasetAlias? DatasetAlias { get; private set; }
    public string? ResultPath { get; private set; }
    public string? MathExpression { get; private set; }

    // Navigation property for EF Core materialization
    public Template? Template { get; private set; }

    // For EF Core materialization only
    private FieldMapping() { }

    internal FieldMapping(
        Guid? id,
        Guid templateId,
        string placeholder,
        string sourcePath,
        string label,
        bool required,
        int sortOrder,
        DataSourceType? dataSourceType,
        string? defaultValue,
        string? transform,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        TemplateId = Guard.NotEmpty(templateId, nameof(TemplateId));
        Placeholder = Guard.NotBlank(placeholder, nameof(Placeholder), 100);
        SourcePath = (sourcePath ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        Required = required;
        SortOrder = sortOrder;
        DataSourceType = dataSourceType ?? DataSourceType.Json;
        DefaultValue = defaultValue;
        Transform = transform;
    }

    public static FieldMapping Create(
        Guid templateId,
        string placeholder,
        string sourcePath,
        string label,
        bool required,
        int sortOrder,
        DateTimeOffset now,
        DataSourceType? dataSourceType = null,
        string? defaultValue = null,
        string? transform = null) =>
        new(null, templateId, placeholder, sourcePath, label, required, sortOrder, dataSourceType, defaultValue, transform, now);

    public void UpdateMappingDetails(
        string sourcePath,
        string label,
        bool required,
        string? defaultValue,
        string? transform,
        int sortOrder,
        DateTimeOffset now)
    {
        SourcePath = (sourcePath ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        Required = required;
        DefaultValue = defaultValue;
        Transform = transform;
        SortOrder = sortOrder;
        SetUpdated(now);
    }

    public void ConfigureDataSource(
        DataSourceType type,
        DatasetAlias? alias,
        string? resultPath,
        string? mathExpression,
        DateTimeOffset now)
    {
        DataSourceType = type ?? DataSourceType.Json;
        DatasetAlias = alias;
        ResultPath = resultPath;
        MathExpression = mathExpression;
        SetUpdated(now);
    }
}
