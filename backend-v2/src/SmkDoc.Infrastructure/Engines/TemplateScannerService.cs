using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Engines.Placeholders;

namespace SmkDoc.Infrastructure.Engines;

public sealed class TemplateScannerService : ITemplateScannerService
{
    public async Task<List<string>> ScanPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct = default)
    {
        var detailed = await ScanDetailedPlaceholdersAsync(stream, fileExtension, ct);
        return detailed
            .Where(p => p.Category != PlaceholderCategory.System)
            .Select(p => p.Name)
            .Distinct()
            .ToList();
    }

    public async Task<List<TemplatePlaceholder>> ScanDetailedPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct = default)
    {
        string ext = fileExtension.ToLowerInvariant();

        return ext switch
        {
            ".docx" => await ScanDocxDetailedPlaceholdersAsync(stream, ct),
            ".xlsx" => await ScanXlsxDetailedPlaceholdersAsync(stream, ct),
            _       => await ScanHtmlDetailedPlaceholdersAsync(stream, ct)
        };
    }

    private static async Task<List<TemplatePlaceholder>> ScanHtmlDetailedPlaceholdersAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        string html = await reader.ReadToEndAsync(ct);
        return HandlebarsPlaceholderScanner.Scan(html);
    }

    private static async Task<List<TemplatePlaceholder>> ScanDocxDetailedPlaceholdersAsync(Stream stream, CancellationToken ct)
    {
        await using var spool = await HybridSpoolStream.SpoolFromAsync(stream, ct: ct);
        using var doc = WordprocessingDocument.Open(spool, false);
        var results = new List<TemplatePlaceholder>();

        var body = doc.MainDocumentPart?.Document.Body;
        if (body != null)
        {
            // 1. Table rows => Tabular placeholders
            foreach (var table in body.Descendants<Table>())
            {
                foreach (var row in table.Descendants<TableRow>())
                {
                    foreach (var cell in row.Descendants<TableCell>())
                    {
                        foreach (var p in cell.Descendants<Paragraph>())
                        {
                            ExtractAndCategorizeFromText(p.InnerText, "items", results);
                        }
                    }
                }
            }

            // 2. Standalone paragraphs outside tables => Scalar or Object
            foreach (var p in body.Elements<Paragraph>())
            {
                ExtractAndCategorizeFromText(p.InnerText, null, results);
            }
        }

        // 3. Headers and Footers => Scalar or System
        foreach (var hdr in doc.MainDocumentPart?.HeaderParts ?? [])
        {
            foreach (var p in hdr.Header.Descendants<Paragraph>())
            {
                ExtractAndCategorizeFromText(p.InnerText, null, results);
            }
        }

        foreach (var ftr in doc.MainDocumentPart?.FooterParts ?? [])
        {
            foreach (var p in ftr.Footer.Descendants<Paragraph>())
            {
                ExtractAndCategorizeFromText(p.InnerText, null, results);
            }
        }

        return DeduplicatePlaceholders(results);
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
                    if (val.Contains("{{"))
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

        var matches = PlaceholderHelper.Pattern.Matches(text);
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
