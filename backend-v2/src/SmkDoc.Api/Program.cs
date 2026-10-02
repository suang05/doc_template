using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using SmkDoc.Api.HealthChecks;
using SmkDoc.Api.Middleware;
using SmkDoc.Api.Models;
using SmkDoc.Application;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Security;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Infrastructure.Engines.Excel;
using SmkDoc.Infrastructure.Engines.Html;
using SmkDoc.Infrastructure.Engines.Word;
using SmkDoc.Infrastructure.Engines;
using SmkDoc.Infrastructure.Imaging;
using SmkDoc.Infrastructure.Pdf;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.Infrastructure.Storage;
using SmkDoc.Infrastructure.Schema;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// --- 0. IP & Port Binding from Environment ---
var customPort = builder.Configuration["PORT"] ?? builder.Configuration["DOC_SERVER_PORT"];
if (!string.IsNullOrEmpty(customPort) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{customPort}");
}

// --- 0b. In-Memory Cache (for TemplateDraftCache) ---
builder.Services.AddMemoryCache();

// --- 1. Presentation ---
builder.Services.AddControllers(options =>
{
    // Automatic FluentValidation command validation filter
    options.Filters.Add<SmkDoc.Api.Filters.ValidateCommandFilter>();
    // Global exception filter: handles all Domain Exceptions -> RFC 7807 Problem Details
    options.Filters.Add<SmkDoc.Api.Filters.GlobalExceptionFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- 1b. Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "default_secret_key_for_dev_min_32_chars_long!";
var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmkDoc",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SmkDoc.Portal",
            IssuerSigningKey = key
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SAMMAKORN Document Generation Service API",
        Version = "v2",
        Description = "Enterprise document generation microservice accepting JSON payloads and generating pixel-perfect PDF, DOCX, or XLSX output based on SDD v1.3."
    });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Enter API Key in header: X-API-Key",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// --- 2. Execution Context ---
builder.Services.AddScoped<ExecutionContextImpl>();
builder.Services.AddScoped<IExecutionContext>(sp => sp.GetRequiredService<ExecutionContextImpl>());

// --- 3. Database (PostgreSQL) ---
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException(
        "Database connection string is required. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorCodesToAdd: null)));

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// --- 4. Storage (MinIO S3) ---
builder.Services.Configure<MinioSettings>(options =>
{
    var minioSection = builder.Configuration.GetSection("Minio");
    options.Endpoint = minioSection["Endpoint"] ?? builder.Configuration["MINIO_ENDPOINT"] ?? string.Empty;
    options.AccessKey = minioSection["AccessKey"] ?? builder.Configuration["MINIO_ROOT_USER"] ?? string.Empty;
    options.SecretKey = minioSection["SecretKey"] ?? builder.Configuration["MINIO_ROOT_PASSWORD"] ?? string.Empty;
    options.PublicEndpoint = minioSection["PublicEndpoint"] ?? builder.Configuration["MINIO_PUBLIC_ENDPOINT"] ?? string.Empty;
    options.Secure = bool.TryParse(minioSection["Secure"] ?? builder.Configuration["MINIO_SECURE"], out bool sec) && sec;
});
builder.Services.AddSingleton<IStorageService, MinioStorageService>();

// --- 4b. Security & Data ---
builder.Services.AddSingleton<IDataProtectionService, SmkDoc.Infrastructure.Security.DataProtectionService>();
builder.Services.AddSingleton<IDocxSecurityScanner, SmkDoc.Infrastructure.Security.DocxSecurityScannerService>();
builder.Services.AddScoped<ISqlExecutorService, SmkDoc.Infrastructure.Data.SqlExecutorService>();

// --- 5. PDF Converter (Gotenberg 8) ---
var gotenbergSettings = new GotenbergSettings
{
    Url = builder.Configuration["GotenbergUrl"]
        ?? builder.Configuration["GOTENBERG_URL"]
        ?? string.Empty
};
builder.Services.Configure<GotenbergSettings>(opts => opts.Url = gotenbergSettings.Url);

builder.Services.AddHttpClient<IPdfRenderer, GotenbergPdfRenderer>(client =>
{
    if (!string.IsNullOrEmpty(gotenbergSettings.Url))
        client.BaseAddress = new Uri(gotenbergSettings.Url);
});

// --- 5b. Named HTTP clients for health checks ---
builder.Services.AddHttpClient("gotenberg-health", client =>
{
    if (!string.IsNullOrEmpty(gotenbergSettings.Url))
        client.BaseAddress = new Uri(gotenbergSettings.Url);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("minio-health", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// --- 5c. Health Checks ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmkDoc.Infrastructure.Persistence.AppDbContext>("postgres", tags: ["db"])
    .AddCheck<GotenbergHealthCheck>("gotenberg", tags: ["deps"])
    .AddCheck<MinioHealthCheck>("minio", tags: ["deps"]);

// --- 6. Imaging & Shared Services ---
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddSingleton<IBarcodeService, BarcodeService>();
builder.Services.AddSingleton<IImageOptimizer, SmkDoc.Infrastructure.Imaging.ImageOptimizerService>();
builder.Services.AddSingleton<IMediaGenerationService, SmkDoc.Infrastructure.Imaging.MediaGenerationService>();
builder.Services.AddSingleton<IJsonDataParser, SmkDoc.Infrastructure.Parsing.JsonDataParser>();
builder.Services.AddSingleton<ITemplateScannerService, SmkDoc.Infrastructure.Engines.TemplateScannerService>();
builder.Services.AddSingleton<ISchemaInferenceService, SmkDoc.Infrastructure.Schema.SchemaInferenceService>();
builder.Services.AddSingleton<IJsonSchemaValidationService, JsonSchemaValidationService>();

// --- 6b. Render Engines & Pipeline Components (Strategy Pattern) ---
builder.Services.AddScoped<WordMediaInjector>();
builder.Services.AddScoped<SmkDoc.Infrastructure.Engines.Excel.ExcelMediaInjector>();
builder.Services.AddScoped<SmkDoc.Infrastructure.Engines.Html.Helpers.IHtmlHelperRegistry, SmkDoc.Infrastructure.Engines.Html.Helpers.HtmlHelperRegistry>();
builder.Services.AddSingleton<SmkDoc.Infrastructure.Engines.Html.Pipeline.HtmlPlaceholderTransformer>();
builder.Services.AddSingleton<SmkDoc.Infrastructure.Engines.Html.Pipeline.HtmlLayoutProcessor>();
builder.Services.AddScoped<IRenderEngine, HtmlTemplateEngine>();
builder.Services.AddScoped<IRenderEngine, DocxTemplateEngine>();
builder.Services.AddScoped<IRenderEngine, ExcelTemplateEngine>();

// --- 6b. Rate Limiting (60 req/min per API key, partitioned) ---
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("api", context =>
    {
        // Partition by API key value so each caller gets their own bucket
        var key = context.Request.Headers["X-API-Key"].ToString();
        var partition = string.IsNullOrEmpty(key)
            ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : key;

        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "TooManyRequests", message = "Rate limit exceeded. Maximum 60 requests per minute per API key." }, ct);
    };
});

// --- 7. Application Use Cases (Modular Monolith) ---
builder.Services.AddSingleton<ITemplateDraftCache, InMemoryTemplateDraftCache>();
builder.Services.AddApplicationServices();

// --- 8. Security Services ---
builder.Services.AddScoped<IPasswordHasher, SmkDoc.Infrastructure.Security.BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, SmkDoc.Infrastructure.Security.JwtTokenGenerator>();

var app = builder.Build();

// Auto-seed default API key from MASTER_API_KEY env var
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var companyRepo = scope.ServiceProvider.GetRequiredService<IRepository<SmkDoc.Domain.Entities.Company>>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<SmkDoc.Domain.Entities.Project>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var company = await companyRepo.FirstOrDefaultAsync(c => c.Name == "SAMMAKORN");
        if (company == null)
        {
            company = new SmkDoc.Domain.Entities.Company("SAMMAKORN");
            await companyRepo.AddAsync(company);
            await uow.CommitAsync();
        }

        var project = await projectRepo.FirstOrDefaultAsync(p => p.CompanyId == company.Id);
        if (project == null)
        {
            project = new SmkDoc.Domain.Entities.Project(company.Id, "Default Project", "default");
            await projectRepo.AddAsync(project);
            await uow.CommitAsync();
        }

        string? masterApiKey = builder.Configuration["MASTER_API_KEY"]
            ?? builder.Configuration["Security:ApiKey"];

        if (string.IsNullOrWhiteSpace(masterApiKey))
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException("CRITICAL SECURITY ERROR: MASTER_API_KEY or Security:ApiKey must be set in Production.");
            }
            masterApiKey = "dev-smk-key-2026";
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Using temporary Development API Key. NEVER use this in production.");
        }

        var apiKeyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        var defaultKeyHash = ApiKeyHelper.ComputeHash(masterApiKey);
        var existingKey = await apiKeyRepo.GetByKeyHashAsync(defaultKeyHash);
        if (existingKey == null)
        {
            await apiKeyRepo.AddAsync(new SmkDoc.Domain.Entities.ApiKey(project.Id, "Master Environment Key", "master", defaultKeyHash, null));
            await uow.CommitAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed on startup. All database operations will fail.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMK Document Server v2"));
}

// Support reverse proxy (Traefik/Nginx/Coolify) — must be first
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<SecurityHeadersMiddleware>();
// UseHttpsRedirection removed — Traefik handles TLS termination in production
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers().RequireRateLimiting("api");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            }),
            totalDurationMs = (int)report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.MapGet("/", () => Results.Ok(new
{
    service = "SMK Document Server v2",
    architecture = "Clean Architecture (SDD v1.3)",
    status = "Operational",
    version = "2.0.0",
    docs = "/swagger"
}));

app.Run();
