using System.Text.Json;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;

public static class FieldMappingSnapshotSerializer
{
    private sealed record SnapshotItem(
        string Placeholder,
        string SourcePath,
        string? Label,
        bool Required,
        int SortOrder,
        string DataSourceType,
        string? DefaultValue,
        string? Transform,
        string? DatasetAlias,
        string? ResultPath,
        string? MathExpression);

    public static string Serialize(IEnumerable<FieldMapping> mappings)
    {
        var items = mappings
            .OrderBy(m => m.SortOrder)
            .Select(m => new SnapshotItem(
                m.Placeholder,
                m.SourcePath,
                m.Label,
                m.Required,
                m.SortOrder,
                m.DataSourceType.Name,
                m.DefaultValue,
                m.Transform,
                m.DatasetAlias?.Value,
                m.ResultPath,
                m.MathExpression))
            .ToList();

        return JsonSerializer.Serialize(items);
    }

    public static List<FieldMapping> Deserialize(TemplateId templateId, string? snapshotJson, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return [];

        var items = JsonSerializer.Deserialize<List<SnapshotItem>>(snapshotJson) ?? [];
        var restored = new List<FieldMapping>(items.Count);

        foreach (var item in items)
        {
            var dsType = DataSourceType.FromString(item.DataSourceType);
            var mapping = FieldMapping.Create(
                templateId,
                item.Placeholder,
                item.SourcePath,
                item.Label ?? string.Empty,
                item.Required,
                item.SortOrder,
                now,
                dsType,
                item.DefaultValue,
                item.Transform);

            if (!string.IsNullOrWhiteSpace(item.DatasetAlias) ||
                !string.IsNullOrWhiteSpace(item.ResultPath) ||
                !string.IsNullOrWhiteSpace(item.MathExpression))
            {
                var aliasVo = !string.IsNullOrWhiteSpace(item.DatasetAlias)
                    ? DatasetAlias.Create(item.DatasetAlias)
                    : null;
                mapping.ConfigureDataSource(dsType, aliasVo, item.ResultPath, item.MathExpression, now);
            }

            restored.Add(mapping);
        }

        return restored;
    }
}
