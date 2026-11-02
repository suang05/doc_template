using DocumentFormat.OpenXml.Packaging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.Infrastructure.Engines.Word;

public sealed class DocxTemplateEngine(
    IPdfRenderer pdfRenderer,
    WordMediaInjector mediaInjector,
    IJsonTableParser? jsonTableParser = null) : IRenderEngine
{
    private readonly WordTextReplacer _textReplacer = new();
    private readonly WordTableExpander _tableExpander = new();
    private readonly IJsonTableParser _jsonTableParser = jsonTableParser ?? new JsonDataParser();

    public RenderEngineType EngineType => RenderEngineType.Docx;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        var (replacements, tables) = _jsonTableParser.Flatten(inputDataJson);

        var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using (var doc = WordprocessingDocument.Open(memoryStream, true))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new RenderException("document.docx", "OpenXml", "Template has no main document part.");

            // 1. Inject QR / Barcode images (reads prefix from template, value from data)
            mediaInjector.InjectMedia(mainPart, replacements);

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
                var pdfStream = await pdfRenderer.RenderOfficeToPdfStreamAsync(memoryStream, "document.docx", ct);
                if (pdfStream is null)
                {
                    var bytes = await pdfRenderer.RenderOfficeToPdfAsync(memoryStream, "document.docx", ct);
                    return new MemoryStream(bytes);
                }
                return pdfStream;
            }
        }

        return memoryStream;
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
