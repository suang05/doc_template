using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;

namespace SmkDoc.Infrastructure.Engines;

public sealed class TemplateScannerService : ITemplateScannerService
{
    public async Task<List<string>> ScanPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct = default)
    {
        string ext = fileExtension.ToLowerInvariant();

        string allText = ext switch
        {
            ".docx" => await ExtractDocxTextAsync(stream, ct),
            ".xlsx" => await ExtractXlsxTextAsync(stream, ct),
            _       => await new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true).ReadToEndAsync(ct)
        };

        return PlaceholderHelper.Pattern
            .Matches(allText)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Where(k => !k.StartsWith('#') && !k.StartsWith('/') && !k.StartsWith('@') && !k.StartsWith('^') && !k.StartsWith('!'))
            .Where(k => !k.Equals("else", StringComparison.OrdinalIgnoreCase) && !k.Equals("this", StringComparison.OrdinalIgnoreCase))
            .Where(k => !k.Contains('(') && !k.Contains(')'))
            .Where(k => !k.Contains(' '))
            .Distinct()
            .ToList();
    }

    private static async Task<string> ExtractDocxTextAsync(Stream stream, CancellationToken ct)
    {
        await using var spool = await HybridSpoolStream.SpoolFromAsync(stream, ct: ct);

        using var doc = WordprocessingDocument.Open(spool, false);
        var sb = new StringBuilder();

        var body = doc.MainDocumentPart?.Document.Body;
        if (body != null)
            foreach (var p in body.Descendants<Paragraph>())
                sb.AppendLine(p.InnerText);

        foreach (var hdr in doc.MainDocumentPart?.HeaderParts ?? [])
            foreach (var p in hdr.Header.Descendants<Paragraph>())
                sb.AppendLine(p.InnerText);

        foreach (var ftr in doc.MainDocumentPart?.FooterParts ?? [])
            foreach (var p in ftr.Footer.Descendants<Paragraph>())
                sb.AppendLine(p.InnerText);

        return sb.ToString();
    }

    private static async Task<string> ExtractXlsxTextAsync(Stream stream, CancellationToken ct)
    {
        await using var spool = await HybridSpoolStream.SpoolFromAsync(stream, ct: ct);

        using var wb = new XLWorkbook(spool);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
            foreach (var row in ws.RowsUsed())
                foreach (var cell in row.CellsUsed())
                {
                    string val = cell.GetString();
                    if (val.Contains("{{"))
                        sb.AppendLine(val);
                }

        return sb.ToString();
    }
}
