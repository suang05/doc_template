using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Data;
using SmkDoc.Infrastructure.Engines;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Observability;
using SmkDoc.Infrastructure.Parsing;
using SmkDoc.Infrastructure.Pdf;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.Infrastructure.Schema;
using SmkDoc.Infrastructure.Security;
using SmkDoc.Infrastructure.Storage;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 2. Execution Context
        services.AddScoped<ExecutionContextImpl>();
        services.AddScoped<IExecutionContext>(sp => sp.GetRequiredService<ExecutionContextImpl>());

        // 3. Database
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "Database connection string is required. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null)));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDataConnectionRepository, DataConnectionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProjectRoleRepository, UserProjectRoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IGenerationLogMetricsRepository, GenerationLogMetricsRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 4. Storage
        services.Configure<MinioSettings>(options =>
        {
            var minioSection = configuration.GetSection("Minio");
            options.Endpoint = minioSection["Endpoint"] ?? configuration["MINIO_ENDPOINT"] ?? string.Empty;
            options.AccessKey = minioSection["AccessKey"] ?? configuration["MINIO_ROOT_USER"] ?? string.Empty;
            options.SecretKey = minioSection["SecretKey"] ?? configuration["MINIO_ROOT_PASSWORD"] ?? string.Empty;
            options.PublicEndpoint = minioSection["PublicEndpoint"] ?? configuration["MINIO_PUBLIC_ENDPOINT"] ?? string.Empty;
            options.Secure = bool.TryParse(minioSection["Secure"] ?? configuration["MINIO_SECURE"], out bool sec) && sec;
        });
        services.AddSingleton<IStorageService, MinioStorageService>();

        // 4b. Security & Data
        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IDocxSecurityScanner, DocxSecurityScannerService>();
        services.AddScoped<ISqlExecutorService, SqlExecutorService>();

        // 5. PDF Converter (Gotenberg 8)
        var gotenbergSettings = new GotenbergSettings
        {
            Url = configuration["GotenbergUrl"]
                ?? configuration["GOTENBERG_URL"]
                ?? string.Empty
        };
        services.Configure<GotenbergSettings>(opts => opts.Url = gotenbergSettings.Url);

        services.AddHttpClient<IPdfRenderer, GotenbergPdfRenderer>(client =>
        {
            if (!string.IsNullOrEmpty(gotenbergSettings.Url))
                client.BaseAddress = new Uri(gotenbergSettings.Url);
        })
        .AddResilienceHandler("gotenberg-resilience", builder =>
        {
            // 1. Total request timeout (inclusive of all attempts)
            builder.AddTimeout(TimeSpan.FromSeconds(60));

            // 2. Adaptive Retry with Exponential Backoff + Jitter for transient errors
            builder.AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = Polly.DelayBackoffType.Exponential,
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

            // 3. Circuit Breaker: Trip on 50% transient failures over 30s window; 15s break duration
            builder.AddCircuitBreaker(new Microsoft.Extensions.Http.Resilience.HttpCircuitBreakerStrategyOptions
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

            // 4. Per-attempt timeout (safeguards against hanging Chromium/LibreOffice render)
            builder.AddTimeout(TimeSpan.FromSeconds(30));
        });

        services.AddHttpClient("gotenberg-health", client =>
        {
            if (!string.IsNullOrEmpty(gotenbergSettings.Url))
                client.BaseAddress = new Uri(gotenbergSettings.Url);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHttpClient("minio-health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // 6. Imaging & Shared Services
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IImageOptimizer, ImageOptimizerService>();
        services.AddSingleton<IMediaGenerationService, MediaGenerationService>();
        services.AddSingleton<JsonDataParser>();
        services.AddSingleton<IJsonHierarchyParser>(sp => sp.GetRequiredService<JsonDataParser>());
        services.AddSingleton<IJsonNamedArrayParser>(sp => sp.GetRequiredService<JsonDataParser>());
        services.AddSingleton<ITemplateScannerService, TemplateScannerService>();
        services.AddSingleton<ISchemaInferenceService, SchemaInferenceService>();
        services.AddSingleton<IJsonSchemaValidationService, JsonSchemaValidationService>();
        services.AddSingleton<ITemplateDraftCache, InMemoryTemplateDraftCache>();
        services.AddSingleton<ICompiledTemplateCache, MemoryCompiledTemplateCache>();
        services.AddSingleton<IDocumentMetrics, DocumentMetrics>();

        // 6b. Render Engines
        services.AddScoped<WordMediaInjector>();
        services.AddScoped<ExcelMediaInjector>();
        services.AddScoped<IHtmlHelperRegistry, HtmlHelperRegistry>();
        services.AddSingleton<HtmlPlaceholderTransformer>();
        services.AddSingleton<HtmlLayoutProcessor>();
        services.AddScoped<IRenderEngine, HtmlTemplateEngine>();
        services.AddScoped<IRenderEngine, DocxTemplateEngine>();
        services.AddScoped<IRenderEngine, ExcelTemplateEngine>();

        // 8. Security Services
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
