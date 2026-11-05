using System.Text.RegularExpressions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.Html.Pipeline;

public record HtmlLayoutResult(string BodyHtml, string? HeaderHtml, string? FooterHtml);

public class HtmlLayoutProcessor
{
    private const string FontHead = "<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Sarabun:wght@300;400;600;700&display=swap\"><style>body { font-family: 'Sarabun', sans-serif; }</style>";

    public HtmlLayoutResult Process(string html, TemplatePageSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new HtmlLayoutResult(string.Empty, settings?.HeaderHtml, settings?.FooterHtml);

        string processedHtml = html;
        var effectiveSettings = settings ?? TemplatePageSettings.Default;

        // 1. Ensure CSS @page geometry is present
        if (!processedHtml.Contains("@page", StringComparison.OrdinalIgnoreCase))
        {
            string pageCss = $"<style>{effectiveSettings.ToCssAtPageRule()}</style>";
            processedHtml = processedHtml.Contains("<head>", StringComparison.OrdinalIgnoreCase)
                ? processedHtml.Replace("<head>", $"<head>\n{pageCss}", StringComparison.OrdinalIgnoreCase)
                : $"{pageCss}\n{processedHtml}";
        }

        // 2. Ensure Thai Sarabun font link is present in <head>
        if (!processedHtml.Contains("fonts.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
            !processedHtml.Contains("font-family", StringComparison.OrdinalIgnoreCase))
        {
            processedHtml = processedHtml.Contains("<head>", StringComparison.OrdinalIgnoreCase)
                ? processedHtml.Replace("<head>", $"<head>\n{FontHead}", StringComparison.OrdinalIgnoreCase)
                : $"{FontHead}\n{processedHtml}";
        }

        // 3. Extract header template if present in HTML, or fallback to settings.HeaderHtml
        string? headerHtml = null;
        var headerMatch = Regex.Match(processedHtml, @"<template\s+id=[""']header[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (headerMatch.Success)
        {
            headerHtml = headerMatch.Groups[1].Value;
            processedHtml = processedHtml.Replace(headerMatch.Value, string.Empty);
        }
        else if (!string.IsNullOrWhiteSpace(effectiveSettings.HeaderHtml))
        {
            headerHtml = effectiveSettings.HeaderHtml;
        }

        // 4. Extract footer template if present in HTML, or fallback to settings.FooterHtml
        string? footerHtml = null;
        var footerMatch = Regex.Match(processedHtml, @"<template\s+id=[""']footer[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (footerMatch.Success)
        {
            footerHtml = footerMatch.Groups[1].Value;
            processedHtml = processedHtml.Replace(footerMatch.Value, string.Empty);
        }
        else if (!string.IsNullOrWhiteSpace(effectiveSettings.FooterHtml))
        {
            footerHtml = effectiveSettings.FooterHtml;
        }

        return new HtmlLayoutResult(processedHtml, headerHtml, footerHtml);
    }

    public string AssembleFullHtmlDocument(string bodyHtml, TemplatePageSettings? settings = null)
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
}
