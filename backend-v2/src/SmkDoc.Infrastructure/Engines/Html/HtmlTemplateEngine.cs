using System.Security.Cryptography;
using System.Text;
using HandlebarsDotNet;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.Infrastructure.Engines.Html;

public sealed class HtmlTemplateEngine : IRenderEngine
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IPdfRenderer _pdfRenderer;
    private readonly IJsonHierarchyParser _jsonHierarchyParser;
    private readonly HtmlPlaceholderTransformer _placeholderTransformer;
    private readonly HtmlLayoutProcessor _layoutProcessor;
    private readonly IHandlebars _handlebars;
    private readonly ICompiledTemplateCache? _templateCache;

    [ActivatorUtilitiesConstructor]
    public HtmlTemplateEngine(
        IPdfRenderer pdfRenderer,
        IHandlebars handlebars,
        ICompiledTemplateCache? templateCache = null,
        IJsonHierarchyParser? jsonHierarchyParser = null,
        HtmlPlaceholderTransformer? placeholderTransformer = null,
        HtmlLayoutProcessor? layoutProcessor = null)
    {
        _pdfRenderer = pdfRenderer;
        _handlebars = handlebars;
        _templateCache = templateCache;
        _jsonHierarchyParser = jsonHierarchyParser ?? new JsonDataParser();
        _placeholderTransformer = placeholderTransformer ?? new HtmlPlaceholderTransformer();
        _layoutProcessor = layoutProcessor ?? new HtmlLayoutProcessor();
    }

    public RenderEngineType EngineType => RenderEngineType.Html;

    public async Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(templateStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        string rawHtml = await reader.ReadToEndAsync(ct);

        // 1. Parse JSON input into object hierarchy for Handlebars
        var dataHierarchy = _jsonHierarchyParser.ToHierarchy(inputDataJson);

        // 2. Normalize template placeholders and media markers into Handlebars expressions
        string normalizedHtml = _placeholderTransformer.Transform(rawHtml);

        // 3. Compile and evaluate Handlebars template (using cache if available)
        Func<object, string> evaluate;
        if (_templateCache is not null)
        {
            string cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHtml)));
            evaluate = _templateCache.GetOrAdd(cacheKey, () =>
            {
                var compiled = _handlebars.Compile(normalizedHtml);
                return data => compiled(data);
            });
        }
        else
        {
            var compiled = _handlebars.Compile(normalizedHtml);
            evaluate = data => compiled(data);
        }

        string renderedHtml = evaluate(dataHierarchy);

        // 4. Process layout: font injection and Gotenberg header/footer extraction
        var layoutResult = _layoutProcessor.Process(renderedHtml);

        // 5. Render output format
        if (outputFormat == OutputFormat.Pdf)
        {
            return await _pdfRenderer.RenderHtmlToPdfStreamAsync(
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
