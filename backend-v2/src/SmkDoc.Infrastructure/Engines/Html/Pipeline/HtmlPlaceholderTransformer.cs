using SmkDoc.Application.Modules.Authoring.Templates.Parsing;

namespace SmkDoc.Infrastructure.Engines.Html.Pipeline;

public class HtmlPlaceholderTransformer
{
    /// <summary>
    /// Normalizes template syntax: converts legacy {{key:transform}}, {{qr:key}}, {{barcode:key}}
    /// into standard Handlebars helper syntax.
    /// </summary>
    public string Transform(string html)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("{{", StringComparison.Ordinal))
            return html;

        return TemplatePlaceholderParser.Pattern.Replace(html, match =>
        {
            string fullPath = match.Groups[1].Value.Trim();

            // Skip Handlebars block helpers and expressions with arguments
            if (fullPath.StartsWith('#') || fullPath.StartsWith('/') ||
                fullPath.StartsWith("else", StringComparison.Ordinal) || fullPath.StartsWith('^') ||
                fullPath.Contains(' ', StringComparison.Ordinal))
            {
                return match.Value;
            }

            var parsed = TemplatePlaceholderParser.Parse(fullPath);

            return parsed.Kind switch
            {
                PlaceholderKind.Transform => $"{{{{thaitransform {parsed.Key} \"{parsed.Extra}\"}}}}",
                PlaceholderKind.Qr        => $"{{{{qr {parsed.Key}}}}}",
                PlaceholderKind.Barcode   => $"{{{{barcode {parsed.Key}}}}}",
                _                         => match.Value
            };
        });
    }
}
