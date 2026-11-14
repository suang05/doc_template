using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.Parsing;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Engines.Placeholders;

namespace SmkDoc.Infrastructure.Engines;

public sealed class TemplateScannerService : ITemplateScannerService
{
    public async Task<List<string>> ScanPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct)
    {
        var detailed = await ScanDetailedPlaceholdersAsync(stream, fileExtension, ct);
        return detailed
            .Where(p => p.Category != PlaceholderCategory.System)
            .Select(p => p.Name)
            .Distinct()
            .ToList();
    }

    public async Task<List<TemplatePlaceholder>> ScanDetailedPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct)
    {
        return fileExtension.Trim().TrimStart('.').ToUpperInvariant() switch
        {
            "DOCX" => await ScanDocxDetailedPlaceholdersAsync(stream, ct),
            "XLSX" => await ScanXlsxDetailedPlaceholdersAsync(stream, ct),
            _ => await ScanHtmlDetailedPlaceholdersAsync(stream, ct)
        };
    }

    private static async Task<List<TemplatePlaceholder>> ScanHtmlDetailedPlaceholdersAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        string html = await reader.ReadToEndAsync(ct);
        return [.. HandlebarsPlaceholderScanner.Scan(html)];
    }

    private static async Task<List<TemplatePlaceholder>> ScanDocxDetailedPlaceholdersAsync(Stream stream, CancellationToken ct)
    {
        await using var spool = await HybridSpoolStream.SpoolFromAsync(stream, ct: ct);
        using var doc = WordprocessingDocument.Open(spool, false);
        var results = new List<TemplatePlaceholder>();

        var body = doc.MainDocumentPart?.Document.Body;
        if (body != null)
        {
            ScanDocxBodyPlaceholders(body, results);
        }

        ScanDocxHeaderAndFooterPlaceholders(doc.MainDocumentPart, results);
        return DeduplicatePlaceholders(results);
    }

    private static void ScanDocxBodyPlaceholders(
        Body body,
        List<TemplatePlaceholder> results)
    {
        foreach (var table in body.Descendants<Table>())
        {
            ScanDocxTablePlaceholders(table, results);
        }

        ScanParagraphPlaceholders(body.Elements<Paragraph>(), null, results);
    }

    private static void ScanDocxTablePlaceholders(Table table, List<TemplatePlaceholder> results)
    {
        foreach (var row in table.Descendants<TableRow>())
        {
            foreach (var cell in row.Descendants<TableCell>())
            {
                ScanParagraphPlaceholders(cell.Descendants<Paragraph>(), "items", results);
            }
        }
    }

    private static void ScanDocxHeaderAndFooterPlaceholders(
        MainDocumentPart? mainPart,
        List<TemplatePlaceholder> results)
    {
        foreach (var hdr in mainPart?.HeaderParts ?? [])
        {
            ScanParagraphPlaceholders(hdr.Header.Descendants<Paragraph>(), null, results);
        }

        foreach (var ftr in mainPart?.FooterParts ?? [])
        {
            ScanParagraphPlaceholders(ftr.Footer.Descendants<Paragraph>(), null, results);
        }
    }

    private static void ScanParagraphPlaceholders(
        IEnumerable<Paragraph> paragraphs,
        string? collection,
        List<TemplatePlaceholder> results)
    {
        foreach (var paragraph in paragraphs)
        {
            ExtractAndCategorizeFromText(paragraph.InnerText, collection, results);
        }
    }

    private static async Task<List<TemplatePlaceholder>> ScanXlsxDetailedPlaceholdersAsync(Stream stream, CancellationToken ct)
    {
        await using var spool = await HybridSpoolStream.SpoolFromAsync(stream, ct: ct);
        using var wb = new XLWorkbook(spool);
        var results = new List<TemplatePlaceholder>();

        foreach (var ws in wb.Worksheets)
        {
            foreach (var row in ws.RowsUsed())
            {
                bool isTableRow = row.RowNumber() > 1;
                string? collection = isTableRow ? "items" : null;

                foreach (var cell in row.CellsUsed())
                {
                    string val = cell.GetString();
                    if (val.Contains("{{", StringComparison.Ordinal))
                    {
                        ExtractAndCategorizeFromText(val, collection, results);
                    }
                }
            }
        }

        return DeduplicatePlaceholders(results);
    }

    private static void ExtractAndCategorizeFromText(
        string text,
        string? currentCollection,
        List<TemplatePlaceholder> list)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var matches = TemplatePlaceholderParser.Pattern.Matches(text);
        foreach (Match match in matches)
        {
            string raw = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(raw) ||
                raw.StartsWith('#') || raw.StartsWith('/') || raw.StartsWith('^') || raw.StartsWith('!'))
            {
                continue;
            }

            var placeholder = PlaceholderCategorizer.Categorize(raw, currentCollection);
            if (!string.IsNullOrWhiteSpace(placeholder.Name) && !PlaceholderCategorizer.IsControlKeyword(placeholder.Name))
            {
                list.Add(placeholder);
            }
        }
    }

    private static List<TemplatePlaceholder> DeduplicatePlaceholders(IEnumerable<TemplatePlaceholder> placeholders)
    {
        return placeholders
            .GroupBy(p => (p.Name, p.Category, p.ParentCollection))
            .Select(g => g.First())
            .ToList();
    }
}
