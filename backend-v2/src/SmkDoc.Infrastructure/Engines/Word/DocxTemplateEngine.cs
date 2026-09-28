using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Engines.Word;

public class DocxTemplateEngine : IRenderEngine
{
    private readonly IPdfRenderer      _pdfRenderer;
    private readonly WordTextReplacer  _textReplacer;
    private readonly WordTableExpander _tableExpander;
    private readonly WordMediaInjector _mediaInjector;
    private readonly IJsonDataParser   _jsonDataParser;

    public DocxTemplateEngine(IPdfRenderer pdfRenderer, WordMediaInjector mediaInjector, IJsonDataParser? jsonDataParser = null)
    {
        _pdfRenderer    = pdfRenderer;
        _textReplacer   = new WordTextReplacer();
        _tableExpander  = new WordTableExpander();
        _mediaInjector  = mediaInjector;
        _jsonDataParser = jsonDataParser ?? new SmkDoc.Infrastructure.Parsing.JsonDataParser();
    }

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
                ?? throw new InvalidOperationException("Template has no main document part.");

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
            var pdfStream = await _pdfRenderer.RenderOfficeToPdfStreamAsync(memoryStream, "document.docx", ct);
            if (pdfStream == null)
            {
                var bytes = await _pdfRenderer.RenderOfficeToPdfAsync(memoryStream, "document.docx", ct);
                return new MemoryStream(bytes);
            }
            return pdfStream;
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
