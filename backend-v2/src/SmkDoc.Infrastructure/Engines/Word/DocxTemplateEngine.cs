using DocumentFormat.OpenXml.Packaging;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.Infrastructure.Engines.Word;

public sealed class DocxTemplateEngine(
    IPdfRenderer pdfRenderer,
    WordMediaInjector mediaInjector,
    WordTextReplacer textReplacer,
    WordTableExpander tableExpander,
    IJsonTableParser jsonTableParser) : IRenderEngine
{
    public RenderEngineType EngineType => RenderEngineType.Docx;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        var (replacements, tables) = jsonTableParser.Flatten(inputDataJson);

        var spoolStream = await HybridSpoolStream.SpoolFromAsync(templateStream, ct: ct);
        try
        {
            using (var doc = WordprocessingDocument.Open(spoolStream, true))
            {
                var mainPart = doc.MainDocumentPart
                    ?? throw new RenderException("document.docx", "OpenXml", "Template has no main document part.");

                // 1. Inject QR / Barcode images (reads prefix from template, value from data)
                mediaInjector.InjectMedia(mainPart, replacements);

                // 2. Expand dynamic table rows
                tableExpander.ExpandTables(mainPart, tables);

                // 3. Replace text placeholders and Thai transforms
                textReplacer.ReplaceTexts(mainPart, replacements);

                mainPart.Document.Save();
            }

            spoolStream.Position = 0;

            if (outputFormat == OutputFormat.Pdf)
            {
                await using (spoolStream)
                {
                    return await pdfRenderer.RenderOfficeToPdfStreamAsync(spoolStream, "document.docx", ct);
                }
            }

            return spoolStream;
        }
        catch
        {
            await spoolStream.DisposeAsync();
            throw;
        }
    }
}
