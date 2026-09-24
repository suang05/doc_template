using System.Text.RegularExpressions;

namespace SmkDoc.Application.Common.Helpers;

/// <summary>
/// SSoT for template placeholder parsing.
/// Handles: {{field}}, {{field:transform}}, {{qr:field}}, {{barcode:field}}, {{image:field}}
/// </summary>
public static class PlaceholderHelper
{
    public static readonly Regex Pattern = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

    /// <summary>Matches dot-notation array access like arrayKey.field inside a placeholder.</summary>
    public static readonly Regex DotNotationPattern = new(@"^([a-zA-Z0-9_]+)\.([a-zA-Z0-9_.]+)$", RegexOptions.Compiled);

    public enum PlaceholderKind { Text, Transform, Qr, Barcode, Image }

    public record ParsedPlaceholder(PlaceholderKind Kind, string Key, string? Extra = null);

    public static ParsedPlaceholder Parse(string content)
    {
        var trimmed = content.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0) return new(PlaceholderKind.Text, trimmed);

        var left  = trimmed[..colonIdx].Trim();
        var right = trimmed[(colonIdx + 1)..].Trim();

        return left.ToLowerInvariant() switch
        {
            "qr" or "qrcode" => new(PlaceholderKind.Qr,      right),
            "barcode"        => new(PlaceholderKind.Barcode,  right),
            "image"          => new(PlaceholderKind.Image,    right),
            _                => new(PlaceholderKind.Transform, left, right)  // {{key:transform}}
        };
    }
}
