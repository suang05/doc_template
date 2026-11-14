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
    ExcelTableExpander tableExpander,
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
            if (worksheet.PageSetup.PaperSize == XLPaperSize.LetterPaper)
            {
                worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            }

            if (worksheet.PageSetup.PagesWide == 0 && worksheet.PageSetup.PagesTall == 0)
            {
                worksheet.PageSetup.PagesWide = 1;
                worksheet.PageSetup.PagesTall = 0;
            }

            mediaInjector.InjectMedia(worksheet, replacements);
            tableExpander.ExpandTables(worksheet, arrays);

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
                        _ => m.Value
                    };
                });

                if (updated != val)
                    cell.SetValue(updated);
            }
        }

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
