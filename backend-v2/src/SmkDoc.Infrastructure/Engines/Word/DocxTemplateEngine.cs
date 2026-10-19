using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Engines.Word;

public class DocxTemplateEngine(
    IPdfRenderer pdfRenderer,
    WordMediaInjector mediaInjector,
    IJsonDataParser? jsonDataParser = null) : IRenderEngine
{
    private readonly IPdfRenderer _pdfRenderer = pdfRenderer;
    private readonly WordTextReplacer _textReplacer = new();
    private readonly WordTableExpander _tableExpander = new();
    private readonly WordMediaInjector _mediaInjector = mediaInjector;
    private readonly IJsonDataParser _jsonDataParser = jsonDataParser ?? new SmkDoc.Infrastructure.Parsing.JsonDataParser();

    public RenderEngineType EngineType => RenderEngineType.Docx;

    public async Task<Stream> RenderStreamAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default)
    {
        var (replacements, tables) = _jsonDataParser.Flatten(inputDataJson);

        var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using (var doc = WordprocessingDocument.Open(memoryStream, true))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new RenderException("document.docx", "OpenXml", "Template has no main document part.");

            // 1. Inject QR / Barcode images (reads prefix from template, value from data)
            _mediaInjector.InjectMedia(mainPart, replacements);

            // 2. Expand dynamic table rows
            _tableExpander.ExpandTables(mainPart, tables);

            // 3. Replace text placeholders and Thai transforms
            _textReplacer.ReplaceTexts(mainPart, replacements);

            mainPart.Document.Save();
        }

        memoryStream.Position = 0;

        if (outputFormat == OutputFormat.Pdf)
        {
            await using (memoryStream)
            {
                var pdfStream = await _pdfRenderer.RenderOfficeToPdfStreamAsync(memoryStream, "document.docx", ct);
                if (pdfStream == null)
                {
                    var bytes = await _pdfRenderer.RenderOfficeToPdfAsync(memoryStream, "document.docx", ct);
                    return new MemoryStream(bytes);
                }
                return pdfStream;
            }
        }

        return memoryStream;
    }

    public async Task<byte[]> RenderAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default)
    {
        await using var stream = await RenderStreamAsync(templateStream, inputDataJson, outputFormat, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
