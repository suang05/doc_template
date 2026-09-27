using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Parsing;

public class JsonDataParser : IJsonDataParser, IJsonHierarchyParser, IJsonNamedArrayParser
{
    public Dictionary<string, object> ToHierarchy(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseElement(document.RootElement) as Dictionary<string, object>
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public (Dictionary<string, string> Replacements, List<List<Dictionary<string, string>>> Tables) Flatten(string json)
    {
        var (replacements, namedArrays) = FlattenNamed(json);
        var tables = new List<List<Dictionary<string, string>>>(namedArrays.Values);
        return (replacements, tables);
    }

    public (Dictionary<string, string> Replacements, Dictionary<string, List<Dictionary<string, string>>> Arrays) FlattenNamed(string json)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var arrays       = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
            return (replacements, arrays);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (replacements, arrays);

            foreach (var prop in root.EnumerateObject())
            {
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.Array:
                        var rows = new List<Dictionary<string, string>>();
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var cell in item.EnumerateObject())
                                row[cell.Name] = cell.Value.ToString();
                            rows.Add(row);
                        }
                        arrays[prop.Name] = rows;
                        break;

                    case JsonValueKind.Object:
                        foreach (var sub in prop.Value.EnumerateObject())
                            replacements[$"{prop.Name}.{sub.Name}"] = sub.Value.ToString();
                        break;

                    default:
                        replacements[prop.Name] = prop.Value.ToString();
                        break;
                }
            }
        }
        catch
        {
            // Return empty structures on malformed JSON
        }

        return (replacements, arrays);
    }

    private static object? ParseElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ParseElement(property.Value) ?? string.Empty;
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    var parsedItem = ParseElement(item);
                    if (parsedItem != null)
                        list.Add(parsedItem);
                }
                return list;

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }
}
