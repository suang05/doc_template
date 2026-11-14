using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmkDoc.Application.Modules.DataBinding.Helpers;

/// <summary>
/// Stateless utility for extracting scalar and nested JSON values using dot-notation and array indices.
/// </summary>
public static partial class JsonPathEvaluator
{
    [GeneratedRegex(@"^([a-zA-Z0-9_-]+)\[(\d+)\]$", RegexOptions.None, 500)]
    private static partial Regex JsonPathArrayPattern();

    public static string? ResolvePath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var parts = path.Split('.');
        var current = UnwrapInitialArray(root, parts[0]);

        foreach (var part in parts)
        {
            if (!TryStepSegment(current, part, out current))
            {
                return null;
            }
        }

        return FormatResolvedValue(current);
    }

    private static JsonElement UnwrapInitialArray(JsonElement root, string firstPart)
    {
        if (root.ValueKind == JsonValueKind.Array &&
            root.GetArrayLength() > 0 &&
            !int.TryParse(firstPart, out _) &&
            !JsonPathArrayPattern().IsMatch(firstPart))
        {
            return root[0];
        }

        return root;
    }

    private static bool TryStepSegment(JsonElement current, string part, out JsonElement next)
    {
        var match = JsonPathArrayPattern().Match(part);
        if (match.Success)
        {
            return TryStepIndexedProperty(current, match, out next);
        }

        if (current.ValueKind == JsonValueKind.Array &&
            int.TryParse(part, out var idx) &&
            idx >= 0 &&
            idx < current.GetArrayLength())
        {
            next = current[idx];
            return true;
        }

        if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var prop))
        {
            next = prop;
            return true;
        }

        next = default;
        return false;
    }

    private static bool TryStepIndexedProperty(JsonElement current, Match match, out JsonElement next)
    {
        var propName = match.Groups[1].Value;
        if (current.ValueKind == JsonValueKind.Object &&
            current.TryGetProperty(propName, out var prop) &&
            prop.ValueKind == JsonValueKind.Array &&
            int.TryParse(match.Groups[2].Value, out int index) &&
            index >= 0 &&
            index < prop.GetArrayLength())
        {
            next = prop[index];
            return true;
        }

        next = default;
        return false;
    }

    private static string? FormatResolvedValue(JsonElement current) => current.ValueKind switch
    {
        JsonValueKind.String => current.GetString(),
        JsonValueKind.Number => current.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => current.GetRawText()
    };

    public static string? ExtractFirstScalar(JsonElement el)
    {
        var target = el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0
            ? el[0]
            : el;

        if (target.ValueKind == JsonValueKind.Object)
        {
            var first = target.EnumerateObject().FirstOrDefault();
            return first.Value.ValueKind switch
            {
                JsonValueKind.String => first.Value.GetString(),
                JsonValueKind.Undefined => null,
                _ => first.Value.GetRawText()
            };
        }

        return target.ValueKind switch
        {
            JsonValueKind.String => target.GetString(),
            JsonValueKind.Number => target.GetRawText(),
            _ => null
        };
    }
}
