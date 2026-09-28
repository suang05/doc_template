using System.Text.Json;
using ClosedXML.Excel;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Excel;

public class ExcelTemplateEngine : IRenderEngine
{
    private readonly IPdfRenderer        _pdfRenderer;
    private readonly ExcelMediaInjector    _mediaInjector;
    private readonly ExcelTableExpander    _tableExpander;
    private readonly IJsonDataParser       _jsonDataParser;

    public ExcelTemplateEngine(IPdfRenderer pdfRenderer, ExcelMediaInjector mediaInjector, IJsonDataParser? jsonDataParser = null)
    {
        _pdfRenderer    = pdfRenderer;
        _mediaInjector  = mediaInjector;
        _tableExpander  = new ExcelTableExpander();
        _jsonDataParser = jsonDataParser ?? new SmkDoc.Infrastructure.Parsing.JsonDataParser();
    }

    public RenderEngineType EngineType => RenderEngineType.Excel;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        var (replacements, arrays) = _jsonDataParser.FlattenNamed(inputDataJson);

        using var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using var workbook = new XLWorkbook(memoryStream);

        foreach (var worksheet in workbook.Worksheets)
        {
            // Standardize to A4 paper size if left at Excel default (Letter)
            if (worksheet.PageSetup.PaperSize == XLPaperSize.LetterPaper)
            {
                worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            }

            // Only apply 1-page-wide fit if not already customized by the template designer
            if (worksheet.PageSetup.PagesWide == 0 && worksheet.PageSetup.PagesTall == 0)
            {
                worksheet.PageSetup.PagesWide = 1;
                worksheet.PageSetup.PagesTall = 0;
            }

            // 1. Inject QR / Barcode images
            _mediaInjector.InjectMedia(worksheet, replacements);

            // 2. Expand array template rows
            _tableExpander.ExpandTables(worksheet, arrays);

            // 3. Replace flat text placeholders
            foreach (var cell in worksheet.CellsUsed())
            {
                if (cell.HasFormula) continue;
                string val = cell.GetString();
                if (string.IsNullOrWhiteSpace(val) || !val.Contains("{{")) continue;

                string updated = Pattern.Replace(val, m =>
                {
                    var parsed = Parse(m.Groups[1].Value);
                    return parsed.Kind switch
                    {
                        PlaceholderKind.Text =>
                            replacements.TryGetValue(parsed.Key, out var r) ? r : m.Value,
                        PlaceholderKind.Transform =>
                            replacements.TryGetValue(parsed.Key, out var r)
                                ? ThaiDataTransformer.Transform(r, parsed.Extra!)
                                : m.Value,
                        _ => m.Value // QR / Barcode / Image already handled
                    };
                });

                if (updated != val)
                    cell.SetValue(updated);
            }
        }

        var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);
        outputStream.Position = 0;

        if (outputFormat == OutputFormat.Pdf)
        {
            var pdfStream = await _pdfRenderer.RenderOfficeToPdfStreamAsync(outputStream, "spreadsheet.xlsx", ct);
            if (pdfStream == null)
            {
                var bytes = await _pdfRenderer.RenderOfficeToPdfAsync(outputStream, "spreadsheet.xlsx", ct);
                return new MemoryStream(bytes);
            }
            return pdfStream;
        }

        return outputStream;
    }

    public async Task<byte[]> RenderAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        await using var stream = await RenderStreamAsync(templateStream, inputDataJson, outputFormat, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
