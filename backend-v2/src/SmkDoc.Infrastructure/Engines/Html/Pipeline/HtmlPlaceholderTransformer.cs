using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Helpers;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Html.Pipeline;

public class HtmlPlaceholderTransformer
{
    /// <summary>
    /// Normalizes template syntax: converts legacy {{key:transform}}, {{qr:key}}, {{barcode:key}}
    /// into standard Handlebars helper syntax.
    /// </summary>
    public string Transform(string html)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("{{"))
            return html;

        return PlaceholderHelper.Pattern.Replace(html, match =>
        {
            string fullPath = match.Groups[1].Value.Trim();

            // Skip Handlebars block helpers and expressions with arguments
            if (fullPath.StartsWith("#") || fullPath.StartsWith("/") || 
                fullPath.StartsWith("else") || fullPath.StartsWith("^") || 
                fullPath.Contains(' '))
            {
                return match.Value;
            }

            var parsed = PlaceholderHelper.Parse(fullPath);

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
