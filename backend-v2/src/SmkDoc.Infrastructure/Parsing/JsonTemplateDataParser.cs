using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Parsing;

public sealed class JsonTemplateDataParser : ITemplateDataParser
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
        catch (JsonException)
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
        var arrays = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
            return (replacements, arrays);

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                PopulateFromRootObject(document.RootElement, replacements, arrays);
            }
        }
        catch (JsonException)
        {
            // Return empty structures on malformed JSON
        }

        return (replacements, arrays);
    }

    private static void PopulateFromRootObject(
        JsonElement root,
        Dictionary<string, string> replacements,
        Dictionary<string, List<Dictionary<string, string>>> arrays)
    {
        foreach (var prop in root.EnumerateObject())
        {
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    arrays[prop.Name] = ExtractArrayRows(prop.Value);
                    break;
                case JsonValueKind.Object:
                    FlattenObjectProperty(prop.Name, prop.Value, replacements);
                    break;
                default:
                    replacements[prop.Name] = prop.Value.ToString();
                    break;
            }
        }
    }

    private static List<Dictionary<string, string>> ExtractArrayRows(JsonElement arrayElement)
    {
        var rows = new List<Dictionary<string, string>>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in item.EnumerateObject())
            {
                row[cell.Name] = cell.Value.ToString();
            }
            rows.Add(row);
        }

        return rows;
    }

    private static void FlattenObjectProperty(
        string prefix,
        JsonElement objectElement,
        Dictionary<string, string> replacements)
    {
        foreach (var sub in objectElement.EnumerateObject())
        {
            replacements[$"{prefix}.{sub.Name}"] = sub.Value.ToString();
        }
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

