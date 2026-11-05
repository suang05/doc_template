using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Data;
using SmkDoc.Infrastructure.Data.Drivers;
using SmkDoc.Infrastructure.Engines;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Html.Helpers;
using SmkDoc.Infrastructure.Engines.Html.Pipeline;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Observability;
using SmkDoc.Infrastructure.Parsing;
using SmkDoc.Infrastructure.Pdf;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;
using SmkDoc.Infrastructure.Persistence.Queries;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.Infrastructure.Queue;
using SmkDoc.Infrastructure.Schema;
using SmkDoc.Infrastructure.Security;
using SmkDoc.Infrastructure.Services.Logging;
using SmkDoc.Infrastructure.Storage;
using SmkDoc.Application.Modules.Authoring.Fonts.Services;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Infrastructure.Engines.DraftHandlers;
using SmkDoc.Infrastructure.Engines.Fonts;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 2. Unified Channel-Agnostic Ambient Project & Caller Context
        services.AddScoped<CurrentProjectContext>();
        services.AddScoped<ICurrentProjectContext>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<ICurrentProjectContextSetter>(sp => sp.GetRequiredService<CurrentProjectContext>());

        // 3. Database
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "Database connection string is required. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        services.AddScoped<TenantSecuritySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
            options.AddInterceptors(sp.GetRequiredService<TenantSecuritySaveChangesInterceptor>());
        });

        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDataConnectionRepository, DataConnectionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProjectRoleRepository, UserProjectRoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IGenerationLogRepository, GenerationLogRepository>();
        services.AddScoped<IGenerationLogMetricsRepository, GenerationLogMetricsRepository>();
        services.AddScoped<IGenerationLogQueryService, GenerationLogQueryService>();
        services.AddSingleton<IGenerationLogQueue, ChannelGenerationLogQueue>();
        services.AddHostedService<GenerationLogBatchWorker>();
        services.AddScoped<IUserWorkspaceQueryService, UserWorkspaceQueryService>();
        services.AddScoped<IProjectQueryService, ProjectQueryService>();
        services.AddScoped<ITemplateQueryService, TemplateQueryService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IDocxSecurityScanner, DocxSecurityScannerService>();
        services.AddSingleton<ISqlConnectionValidator, SqlSecurityGuard>();
        services.AddSingleton<ISqlDriverStrategy, PostgreSqlDriverStrategy>();
        services.AddSingleton<ISqlDriverFactory, SqlDriverFactory>();
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

            // 3. Circuit Breaker: Trip on 50% transient failures over 30s window; 15s break duration
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
        services.AddSingleton<IJsonTableParser>(sp => sp.GetRequiredService<JsonDataParser>());
        services.AddSingleton<IJsonNamedArrayParser>(sp => sp.GetRequiredService<JsonDataParser>());
        services.AddSingleton<ITemplateScannerService, TemplateScannerService>();
        services.AddScoped<IFontCatalogService, FontCatalogService>();
        services.AddSingleton<ISchemaInferenceService, SchemaInferenceService>();
        services.AddSingleton<IJsonSchemaValidationService, JsonSchemaValidationService>();
        services.AddMemoryCache();
        services.AddSingleton<ITemplateDraftCache, InMemoryTemplateDraftCache>();
        services.AddSingleton<ICompiledTemplateCache, MemoryCompiledTemplateCache>();
        services.AddSingleton<IDatasetResultCache, MemoryDatasetResultCache>();
        services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
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
        services.AddScoped<IRenderEngineFactory, RenderEngineFactory>();

        // 6c. Template Draft Handlers
        services.AddScoped<ITemplateDraftHandler, DocxDraftHandler>();
        services.AddScoped<ITemplateDraftHandler, HtmlDraftHandler>();
        services.AddScoped<ITemplateDraftHandler, XlsxDraftHandler>();
        services.AddScoped<ITemplateDraftHandlerRegistry, TemplateDraftHandlerRegistry>();

        // 8. Security Services
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISecurityAuditService, SecurityAuditService>();

        // 9. Document Render Queue & Gotenberg Backpressure Worker (Track 2)
        services.Configure<DocumentRenderWorkerOptions>(configuration.GetSection("DocumentRenderWorker"));
        var redisConn = RedisConfigurationResolver.ResolveConnectionString(configuration);
        if (redisConn is not null)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<IDocumentRenderQueue>(sp =>
            {
                var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisDocumentRenderQueue>>();
                var prefix = RedisConfigurationResolver.ResolveInstancePrefix(configuration);
                return new RedisDocumentRenderQueue(multiplexer, logger, prefix);
            });
        }
        else
        {
            services.AddSingleton<IDocumentRenderQueue, MemoryDocumentRenderQueue>();
        }
        services.AddHostedService<DocumentRenderQueueWorker>();

        // 9b. Document Ephemeral Retention Worker
        services.Configure<DocumentRetentionWorkerOptions>(configuration.GetSection("DocumentRetentionWorker"));
        services.AddHostedService<DocumentRetentionWorker>();

        return services;
    }
}
