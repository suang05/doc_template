using System.Text.RegularExpressions;

namespace SmkDoc.Infrastructure.Engines.Html.Pipeline;

public record HtmlLayoutResult(string BodyHtml, string? HeaderHtml, string? FooterHtml);

public class HtmlLayoutProcessor
{
    private const string FontHead = "<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Sarabun:wght@300;400;600;700&display=swap\"><style>body { font-family: 'Sarabun', sans-serif; }</style>";

    public HtmlLayoutResult Process(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new HtmlLayoutResult(string.Empty, null, null);

        string processedHtml = html;

        // 1. Ensure Thai Sarabun font link is present in <head>
        if (!processedHtml.Contains("fonts.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
            !processedHtml.Contains("font-family", StringComparison.OrdinalIgnoreCase))
        {
            processedHtml = processedHtml.Contains("<head>", StringComparison.OrdinalIgnoreCase)
                ? processedHtml.Replace("<head>", $"<head>\n{FontHead}", StringComparison.OrdinalIgnoreCase)
                : $"{FontHead}\n{processedHtml}";
        }

        // 2. Extract header template if present
        string? headerHtml = null;
        var headerMatch = Regex.Match(processedHtml, @"<template\s+id=[""']header[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (headerMatch.Success)
        {
            headerHtml = headerMatch.Groups[1].Value;
            processedHtml = processedHtml.Replace(headerMatch.Value, string.Empty);
        }

        // 3. Extract footer template if present
        string? footerHtml = null;
        var footerMatch = Regex.Match(processedHtml, @"<template\s+id=[""']footer[""'][^>]*>(.*?)</template>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (footerMatch.Success)
        {
            footerHtml = footerMatch.Groups[1].Value;
            processedHtml = processedHtml.Replace(footerMatch.Value, string.Empty);
        }

        return new HtmlLayoutResult(processedHtml, headerHtml, footerHtml);
    }
}
