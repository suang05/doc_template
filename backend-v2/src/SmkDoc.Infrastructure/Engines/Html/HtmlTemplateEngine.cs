using System.Security.Cryptography;
using System.Text;
using HandlebarsDotNet;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;

namespace SmkDoc.Infrastructure.Engines.Html;

public sealed class HtmlTemplateEngine(
    IPdfRenderer pdfRenderer,
    IHandlebars handlebars,
    ITemplateDataParser dataParser,
    HtmlPlaceholderTransformer placeholderTransformer,
    HtmlLayoutProcessor layoutProcessor,
    ICompiledTemplateCache? templateCache = null) : IRenderEngine
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public RenderEngineType EngineType => RenderEngineType.Html;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct)
    {
        using var reader = new StreamReader(templateStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        string rawHtml = await reader.ReadToEndAsync(ct);

        // 1. Parse JSON input into object hierarchy for Handlebars
        var dataHierarchy = dataParser.ToHierarchy(inputDataJson);

        // 2. Normalize template placeholders and media markers into Handlebars expressions
        string normalizedHtml = placeholderTransformer.Transform(rawHtml);

        // 3. Compile and evaluate Handlebars template (using cache if available)
        Func<object, string> evaluate;
        if (templateCache is not null)
        {
            string cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHtml)));
            evaluate = templateCache.GetOrAdd(cacheKey, () =>
            {
                var compiled = handlebars.Compile(normalizedHtml);
                return data => compiled(data);
            });
        }
        else
        {
            var compiled = handlebars.Compile(normalizedHtml);
            evaluate = data => compiled(data);
        }

        string renderedHtml = evaluate(dataHierarchy);

        // 4. Process layout: font injection and Gotenberg header/footer extraction
        var layoutResult = layoutProcessor.Process(renderedHtml);

        // 5. Render output format
        if (outputFormat == OutputFormat.Pdf)
        {
            return await pdfRenderer.RenderHtmlToPdfStreamAsync(
                layoutResult.BodyHtml,
                layoutResult.HeaderHtml,
                layoutResult.FooterHtml,
                ct);
        }

        var htmlSpool = new HybridSpoolStream();
        try
        {
            await using (var writer = new StreamWriter(htmlSpool, Utf8NoBom, bufferSize: 8192, leaveOpen: true))
            {
                await writer.WriteAsync(layoutResult.BodyHtml.AsMemory(), ct);
                await writer.FlushAsync(ct);
            }

            htmlSpool.Position = 0;
            return htmlSpool;
        }
        catch
        {
            await htmlSpool.DisposeAsync();
            throw;
        }
    }
}
