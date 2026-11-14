using ClosedXML.Excel;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.Parsing;
using SmkDoc.Domain.Enums;
using static SmkDoc.Application.Modules.Authoring.Templates.Parsing.TemplatePlaceholderParser;

namespace SmkDoc.Infrastructure.Engines.Excel;

public sealed class ExcelTemplateEngine(
    IPdfRenderer pdfRenderer,
    ExcelMediaInjector mediaInjector,
    ITemplateDataParser dataParser) : IRenderEngine
{
    public RenderEngineType EngineType => RenderEngineType.Excel;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct)
    {
        var (replacements, arrays) = dataParser.FlattenNamed(inputDataJson);

        await using var inputSpool = await HybridSpoolStream.SpoolFromAsync(templateStream, ct: ct);
        using var workbook = new XLWorkbook(inputSpool);

        foreach (var worksheet in workbook.Worksheets)
        {
            ProcessWorksheet(worksheet, replacements, arrays);
        }

        return await SaveAndExportWorkbookAsync(workbook, outputFormat, ct);
    }

    private void ProcessWorksheet(
        IXLWorksheet worksheet,
        Dictionary<string, string> replacements,
        Dictionary<string, List<Dictionary<string, string>>> arrays)
    {
        ConfigurePageSetup(worksheet);
        mediaInjector.InjectMedia(worksheet, replacements);
        ExcelTableExpander.ExpandTables(worksheet, arrays);
        ReplaceWorksheetPlaceholders(worksheet, replacements);
    }

    private static void ConfigurePageSetup(IXLWorksheet worksheet)
    {
        if (worksheet.PageSetup.PaperSize == XLPaperSize.LetterPaper)
        {
            worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        }

        if (worksheet.PageSetup.PagesWide == 0 && worksheet.PageSetup.PagesTall == 0)
        {
            worksheet.PageSetup.PagesWide = 1;
            worksheet.PageSetup.PagesTall = 0;
        }
    }

    private static void ReplaceWorksheetPlaceholders(
        IXLWorksheet worksheet,
        Dictionary<string, string> replacements)
    {
        foreach (var cell in worksheet.CellsUsed())
        {
            if (cell.HasFormula) continue;
            string val = cell.GetString();
            if (string.IsNullOrWhiteSpace(val) || !val.Contains("{{", StringComparison.Ordinal)) continue;

            string updated = Pattern.Replace(val, m => ResolvePlaceholderMatch(m.Groups[1].Value, m.Value, replacements));
            if (updated != val)
            {
                cell.SetValue(updated);
            }
        }
    }

    private static string ResolvePlaceholderMatch(
        string token,
        string rawMatch,
        Dictionary<string, string> replacements)
    {
        var parsed = Parse(token);
        return parsed.Kind switch
        {
            PlaceholderKind.Text =>
                replacements.TryGetValue(parsed.Key, out var r) ? r : rawMatch,
            PlaceholderKind.Transform =>
                replacements.TryGetValue(parsed.Key, out var r)
                    ? ThaiDataTransformer.Transform(r, parsed.Extra!)
                    : rawMatch,
            _ => rawMatch
        };
    }

    private async Task<Stream> SaveAndExportWorkbookAsync(
        XLWorkbook workbook,
        OutputFormat outputFormat,
        CancellationToken ct)
    {
        var outputSpool = new HybridSpoolStream();
        try
        {
            workbook.SaveAs(outputSpool);
            outputSpool.Position = 0;

            if (outputFormat == OutputFormat.Pdf)
            {
                await using (outputSpool)
                {
                    return await pdfRenderer.RenderOfficeToPdfStreamAsync(outputSpool, "spreadsheet.xlsx", ct);
                }
            }

            return outputSpool;
        }
        catch
        {
            await outputSpool.DisposeAsync();
            throw;
        }
    }
}
