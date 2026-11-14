using HandlebarsDotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Engines;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Engines.Fonts;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Observability;
using SmkDoc.Infrastructure.Parsing;
using SmkDoc.Infrastructure.Pdf;
using SmkDoc.Infrastructure.Schema;

namespace SmkDoc.Infrastructure.Extensions;

public static class RenderingEngineServiceExtensions
{
    public static IServiceCollection AddRenderingEnginesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. PDF Converter & Resilience (Gotenberg 8)
        var gotenbergUrl = configuration["GotenbergUrl"] ?? configuration["GOTENBERG_URL"];
        var gotenbergSettings = new GotenbergSettings
        {
            Url = Uri.TryCreate(gotenbergUrl, UriKind.Absolute, out var parsedGotenbergUri)
                ? parsedGotenbergUri
                : null
        };
        services.Configure<GotenbergSettings>(opts => opts.Url = gotenbergSettings.Url);

        services.AddHttpClient<IPdfRenderer, GotenbergPdfRenderer>(client =>
        {
            if (gotenbergSettings.Url is not null)
                client.BaseAddress = gotenbergSettings.Url;
        })
        .AddResilienceHandler("gotenberg-resilience", builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(60));

            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args =>
                {
                    if (args.Outcome.Result is HttpResponseMessage response)
                    {
                        int statusCode = (int)response.StatusCode;
                        return ValueTask.FromResult(statusCode == 503 || statusCode == 504);
                    }

                    return ValueTask.FromResult(args.Outcome.Exception is HttpRequestException or Polly.Timeout.TimeoutRejectedException);
                }
            });

            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = args =>
                {
                    if (args.Outcome.Result is HttpResponseMessage response)
                    {
                        int statusCode = (int)response.StatusCode;
                        return ValueTask.FromResult(statusCode == 503 || statusCode == 504);
                    }

                    return ValueTask.FromResult(args.Outcome.Exception is HttpRequestException or Polly.Timeout.TimeoutRejectedException);
                }
            });

            builder.AddTimeout(TimeSpan.FromSeconds(30));
        });

        services.AddHttpClient("gotenberg-health", client =>
        {
            if (gotenbergSettings.Url is not null)
                client.BaseAddress = gotenbergSettings.Url;
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // 2. Imaging, Parsers & Shared Services
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IImageOptimizer, ImageOptimizerService>();
        services.AddSingleton<IMediaGenerationService, MediaGenerationService>();
        services.AddSingleton<ITemplateDataParser, JsonTemplateDataParser>();
        services.AddSingleton<ITemplateScannerService, TemplateScannerService>();
        services.AddScoped<IFontCatalogPort, FontCatalogAdapter>();
        services.AddSingleton<ISchemaInferenceService, SchemaInferenceService>();
        services.AddSingleton<IJsonSchemaValidationService, JsonSchemaValidationService>();
        services.AddMemoryCache();
        services.AddSingleton<ICompiledTemplateCache, MemoryCompiledTemplateCache>();
        services.AddSingleton<IDocumentMetrics, DocumentMetrics>();

        // 3. Render Engines (HTML, DOCX, XLSX)
        services.AddSingleton<WordTextReplacer>();
        services.AddSingleton<WordTableExpander>();
        services.AddSingleton<ExcelTableExpander>();
        services.AddScoped<WordMediaInjector>();
        services.AddScoped<ExcelMediaInjector>();
        services.AddSingleton<IHtmlHelperRegistry, HtmlHelperRegistry>();
        services.AddSingleton<IHandlebars>(sp =>
        {
            var handlebars = Handlebars.Create();
            var helperRegistry = sp.GetRequiredService<IHtmlHelperRegistry>();
            helperRegistry.RegisterHelpers(handlebars);
            return handlebars;
        });
        services.AddSingleton<HtmlPlaceholderTransformer>();
        services.AddSingleton<HtmlLayoutProcessor>();
        services.AddScoped<IRenderEngine, HtmlTemplateEngine>();
        services.AddScoped<IRenderEngine, DocxTemplateEngine>();
        services.AddScoped<IRenderEngine, ExcelTemplateEngine>();
        services.AddScoped<IRenderEngineFactory, RenderEngineFactory>();

        return services;
    }
}
