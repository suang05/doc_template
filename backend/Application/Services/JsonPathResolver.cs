using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Service to extract values from nested JSON structures using dot-notation paths (e.g. "customer.fullName", "property.price", "items[0].name").
/// </summary>
public static class JsonPathResolver
{
    private static readonly Regex ArrayIndexerRegex = new Regex(@"^(?<prop>[^\[]+)\[(?<index>\d+)\]$", RegexOptions.Compiled);

    /// <summary>
    /// Evaluates a dot-notated path against a JsonElement.
    /// Returns the string representation of the found element, or null if not found.
    /// </summary>
    public static string? ResolveValue(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var parts = path.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonElement current = root;

        foreach (var part in parts)
        {
            var match = ArrayIndexerRegex.Match(part);
            if (match.Success)
            {
                string propName = match.Groups["prop"].Value;
                int index = int.Parse(match.Groups["index"].Value);

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propName, out var arrayElem))
                {
                    return null;
                }

                if (arrayElem.ValueKind != JsonValueKind.Array || index >= arrayElem.GetArrayLength())
                {
                    return null;
                }

                current = arrayElem[index];
            }
            else
            {
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!current.TryGetProperty(part, out var nextElem))
                {
                    // Try case-insensitive fallback search
                    bool found = false;
                    foreach (var prop in current.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, part, StringComparison.OrdinalIgnoreCase))
                        {
                            current = prop.Value;
                            found = true;
                            break;
                        }
                    }

                    if (!found) return null;
                }
                else
                {
                    current = nextElem;
                }
            }
        }

        return FormatElementValue(current);
    }

    /// <summary>
    /// Extracts an array of row objects from a path (e.g. "items" or "order.details") for table generation.
    /// </summary>
    public static List<Dictionary<string, string>>? ResolveArray(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var parts = path.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonElement current = root;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var nextElem))
            {
                bool found = false;
                if (current.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in current.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, part, StringComparison.OrdinalIgnoreCase))
                        {
                            current = prop.Value;
                            found = true;
                            break;
                        }
                    }
                }
                if (!found) return null;
            }
            else
            {
                current = nextElem;
            }
        }

        if (current.ValueKind != JsonValueKind.Array) return null;

        var result = new List<Dictionary<string, string>>();
        foreach (var item in current.EnumerateArray())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in item.EnumerateObject())
                {
                    row[prop.Name] = FormatElementValue(prop.Value) ?? string.Empty;
                }
            }
            result.Add(row);
        }

        return result;
    }

    private static string? FormatElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }
}
