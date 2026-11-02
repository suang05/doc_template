using ClosedXML.Excel;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Parsing;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Excel;

public sealed class ExcelTemplateEngine(
    IPdfRenderer pdfRenderer,
    ExcelMediaInjector mediaInjector,
    IJsonNamedArrayParser? jsonNamedArrayParser = null) : IRenderEngine
{
    private readonly ExcelTableExpander _tableExpander = new();
    private readonly IJsonNamedArrayParser _jsonNamedArrayParser = jsonNamedArrayParser ?? new JsonDataParser();

    public RenderEngineType EngineType => RenderEngineType.Excel;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        var (replacements, arrays) = _jsonNamedArrayParser.FlattenNamed(inputDataJson);

        using var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using var workbook = new XLWorkbook(memoryStream);

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
            _tableExpander.ExpandTables(worksheet, arrays);

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

        var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);
        outputStream.Position = 0;

        if (outputFormat == OutputFormat.Pdf)
        {
            await using (outputStream)
            {
                var pdfStream = await pdfRenderer.RenderOfficeToPdfStreamAsync(outputStream, "spreadsheet.xlsx", ct);
                if (pdfStream is null)
                {
                    var bytes = await pdfRenderer.RenderOfficeToPdfAsync(outputStream, "spreadsheet.xlsx", ct);
                    return new MemoryStream(bytes);
                }
                return pdfStream;
            }
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
