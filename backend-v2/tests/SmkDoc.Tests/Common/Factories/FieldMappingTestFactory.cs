using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

public static class FieldMappingTestFactory
{
    public static FieldMapping Create(
        Guid? id = null,
        Guid? templateId = null,
        string placeholder = "placeholder",
        string sourcePath = "source.path",
        string label = "Label",
        bool required = false,
        int sortOrder = 1,
        DataSourceType? dataSourceType = null,
        string? defaultValue = null,
        string? transform = null,
        DateTimeOffset? now = null)
    {
        return new FieldMapping(
            id ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            placeholder,
            sourcePath,
            label,
            required,
            sortOrder,
            dataSourceType ?? DataSourceType.Json,
            defaultValue,
            transform,
            now ?? TestConstants.BaselineTime);
    }
}
