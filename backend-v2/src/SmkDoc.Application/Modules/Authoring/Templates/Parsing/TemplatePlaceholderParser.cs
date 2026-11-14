using System.Text.RegularExpressions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Parsing;

/// <summary>
/// Single source of truth for template placeholder parsing.
/// Handles <c>{{field}}</c>, <c>{{field:transform}}</c>, <c>{{qr:field}}</c>, <c>{{barcode:field}}</c>, and <c>{{image:field}}</c>.
/// </summary>
public static partial class TemplatePlaceholderParser
{
    private const int RegexTimeoutMilliseconds = 250;

    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^([a-zA-Z0-9_]+)\.([a-zA-Z0-9_.]+)$", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex DotNotationRegex();

    public static Regex Pattern => PlaceholderRegex();

    /// <summary>Matches dot-notation array access like <c>arrayKey.field</c> inside a placeholder.</summary>
    public static Regex DotNotationPattern => DotNotationRegex();

    public static ParsedPlaceholder Parse(string content)
    {
        var trimmed = content.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0)
        {
            return new(PlaceholderKind.Text, trimmed);
        }

        var left = trimmed[..colonIdx].Trim();
        var right = trimmed[(colonIdx + 1)..].Trim();

        return left.ToUpperInvariant() switch
        {
            "QR" or "QRCODE" => new(PlaceholderKind.Qr, right),
            "BARCODE" => new(PlaceholderKind.Barcode, right),
            "IMAGE" => new(PlaceholderKind.Image, right),
            _ => new(PlaceholderKind.Transform, left, right)
        };
    }
}
