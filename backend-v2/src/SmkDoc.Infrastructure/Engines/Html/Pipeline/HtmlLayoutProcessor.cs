using System.Text.RegularExpressions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.Html.Pipeline;

public record HtmlLayoutResult(string BodyHtml, string? HeaderHtml, string? FooterHtml);

public static partial class HtmlLayoutProcessor
{
    private const string FontHead = "<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Sarabun:wght@300;400;600;700&display=swap\"><style>body { font-family: 'Sarabun', sans-serif; }</style>";

    [GeneratedRegex(@"<template\s+id=[""']header[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderTemplateRegex();

    [GeneratedRegex(@"<template\s+id=[""']footer[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FooterTemplateRegex();

    public static HtmlLayoutResult Process(string html, TemplatePageSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new HtmlLayoutResult(string.Empty, settings?.HeaderHtml, settings?.FooterHtml);

        var effectiveSettings = settings ?? TemplatePageSettings.Default;
        string processedHtml = InjectAtPageRuleIfMissing(html, effectiveSettings);
        processedHtml = InjectThaiFontIfMissing(processedHtml);
        var (finalHtml, headerHtml, footerHtml) = ExtractHeaderAndFooter(processedHtml, effectiveSettings);

        return new HtmlLayoutResult(finalHtml, headerHtml, footerHtml);
    }

    private static string InjectAtPageRuleIfMissing(string html, TemplatePageSettings settings)
    {
        if (html.Contains("@page", StringComparison.OrdinalIgnoreCase))
            return html;

        string pageCss = $"<style>{settings.ToCssAtPageRule()}</style>";
        return InsertIntoHeadOrPrepend(html, pageCss);
    }

    private static string InjectThaiFontIfMissing(string html)
    {
        if (html.Contains("fonts.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("font-family", StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return InsertIntoHeadOrPrepend(html, FontHead);
    }

    private static string InsertIntoHeadOrPrepend(string html, string snippet) =>
        html.Contains("<head>", StringComparison.OrdinalIgnoreCase)
            ? html.Replace("<head>", $"<head>\n{snippet}", StringComparison.OrdinalIgnoreCase)
            : $"{snippet}\n{html}";

    private static (string Html, string? HeaderHtml, string? FooterHtml) ExtractHeaderAndFooter(
        string html,
        TemplatePageSettings settings)
    {
        if (!html.Contains("<template", StringComparison.OrdinalIgnoreCase))
            return (html, settings.HeaderHtml, settings.FooterHtml);

        var (afterHeaderHtml, headerHtml) = ExtractTemplateSection(html, HeaderTemplateRegex(), settings.HeaderHtml);
        var (afterFooterHtml, footerHtml) = ExtractTemplateSection(afterHeaderHtml, FooterTemplateRegex(), settings.FooterHtml);
        return (afterFooterHtml, headerHtml, footerHtml);
    }

    private static (string Html, string? SectionHtml) ExtractTemplateSection(
        string html,
        Regex regex,
        string? fallback)
    {
        var match = regex.Match(html);
        if (match.Success)
            return (html.Remove(match.Index, match.Length), match.Groups[1].Value);

        return (html, string.IsNullOrWhiteSpace(fallback) ? null : fallback);
    }

    public static string AssembleFullHtmlDocument(string bodyHtml, TemplatePageSettings? settings = null)
    {
        var effectiveSettings = settings ?? TemplatePageSettings.Default;
        var headerTag = !string.IsNullOrWhiteSpace(effectiveSettings.HeaderHtml)
            ? $"<template id=\"header\">{effectiveSettings.HeaderHtml}</template>\n"
            : string.Empty;
        var footerTag = !string.IsNullOrWhiteSpace(effectiveSettings.FooterHtml)
            ? $"<template id=\"footer\">{effectiveSettings.FooterHtml}</template>\n"
            : string.Empty;

        if (bodyHtml.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            bodyHtml.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return bodyHtml;
        }

        return $"""
<!DOCTYPE html>
<html lang="th">
<head>
  <meta charset="utf-8" />
  {FontHead}
  <style>{effectiveSettings.ToCssAtPageRule()}</style>
</head>
<body>
{headerTag}{bodyHtml}
{footerTag}</body>
</html>
""";
    }

    public static string InjectCustomFontFace(string html, string fontFamily, string base64FontData, string format = "truetype")
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(base64FontData))
            return html;

        string fontFaceCss = $"<style>\n@font-face {{\n  font-family: '{fontFamily}';\n  src: url('data:font/{format};charset=utf-8;base64,{base64FontData}') format('{format}');\n}}\n</style>";

        return html.Contains("<head>", StringComparison.OrdinalIgnoreCase)
            ? html.Replace("<head>", $"<head>\n{fontFaceCss}", StringComparison.OrdinalIgnoreCase)
            : $"{fontFaceCss}\n{html}";
    }
}
