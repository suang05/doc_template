using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class FieldMappingBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _templateId = Guid.NewGuid();
    private string _placeholder = "placeholder";
    private string _sourcePath = "source.path";
    private string _label = "Label";
    private bool _required = false;
    private int _sortOrder = 1;
    private DataSourceType _dataSourceType = DataSourceType.Json;
    private string? _defaultValue;
    private string? _transform;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static FieldMappingBuilder AMapping() => new();

    public static FieldMapping Default() => AMapping().Build();

    public FieldMappingBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public FieldMappingBuilder WithTemplateId(Guid templateId)
    {
        _templateId = templateId;
        return this;
    }

    public FieldMappingBuilder WithPlaceholder(string placeholder)
    {
        _placeholder = placeholder;
        return this;
    }

    public FieldMappingBuilder WithSourcePath(string sourcePath)
    {
        _sourcePath = sourcePath;
        return this;
    }

    public FieldMappingBuilder WithLabel(string label)
    {
        _label = label;
        return this;
    }

    public FieldMappingBuilder WithRequired(bool required = true)
    {
        _required = required;
        return this;
    }

    public FieldMappingBuilder WithSortOrder(int sortOrder)
    {
        _sortOrder = sortOrder;
        return this;
    }

    public FieldMappingBuilder WithDataSourceType(DataSourceType dataSourceType)
    {
        _dataSourceType = dataSourceType;
        return this;
    }

    public FieldMappingBuilder WithDefaultValue(string? defaultValue)
    {
        _defaultValue = defaultValue;
        return this;
    }

    public FieldMappingBuilder WithTransform(string? transform)
    {
        _transform = transform;
        return this;
    }

    public FieldMappingBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public FieldMapping Build()
    {
        return new FieldMapping(
            _id,
            _templateId,
            _placeholder,
            _sourcePath,
            _label,
            _required,
            _sortOrder,
            _dataSourceType,
            _defaultValue,
            _transform,
            _now);
    }
}
