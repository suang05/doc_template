using System.Text;
using HandlebarsDotNet;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Engines;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Parsing;

namespace SmkDoc.Infrastructure.Engines.Html;

public class HtmlTemplateEngine : IRenderEngine
{
    private readonly IPdfRenderer              _pdfRenderer;
    private readonly IJsonDataParser           _jsonDataParser;
    private readonly IHtmlHelperRegistry       _helperRegistry;
    private readonly HtmlPlaceholderTransformer _placeholderTransformer;
    private readonly HtmlLayoutProcessor        _layoutProcessor;
    private readonly IHandlebars               _handlebars;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public HtmlTemplateEngine(
        IPdfRenderer pdfRenderer,
        IHtmlHelperRegistry helperRegistry,
        IJsonDataParser? jsonDataParser = null,
        HtmlPlaceholderTransformer? placeholderTransformer = null,
        HtmlLayoutProcessor? layoutProcessor = null)
    {
        _pdfRenderer            = pdfRenderer;
        _helperRegistry         = helperRegistry;
        _jsonDataParser         = jsonDataParser ?? new JsonDataParser();
        _placeholderTransformer = placeholderTransformer ?? new HtmlPlaceholderTransformer();
        _layoutProcessor        = layoutProcessor ?? new HtmlLayoutProcessor();

        _handlebars = Handlebars.Create();
        _helperRegistry.RegisterHelpers(_handlebars);
    }

    public RenderEngineType EngineType => RenderEngineType.Html;

    public async Task<byte[]> RenderAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(templateStream, Encoding.UTF8);
        string rawHtml = await reader.ReadToEndAsync(ct);

        // 1. Parse JSON input into object hierarchy for Handlebars
        var dataHierarchy = _jsonDataParser.ToHierarchy(inputDataJson);

        // 2. Normalize template placeholders and media markers into Handlebars expressions
        string normalizedHtml = _placeholderTransformer.Transform(rawHtml);

        // 3. Compile and evaluate Handlebars template
        var compiledTemplate = _handlebars.Compile(normalizedHtml);
        string renderedHtml = compiledTemplate(dataHierarchy);

        // 4. Process layout: font injection and Gotenberg header/footer extraction
        var layoutResult = _layoutProcessor.Process(renderedHtml);

        // 5. Render output format
        if (outputFormat == OutputFormat.Pdf)
        {
            return await _pdfRenderer.RenderHtmlToPdfAsync(
                layoutResult.BodyHtml,
                layoutResult.HeaderHtml,
                layoutResult.FooterHtml,
                ct);
        }

        return Encoding.UTF8.GetBytes(layoutResult.BodyHtml);
    }
}
