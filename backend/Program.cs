using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SmkDocServer.API.Middleware;
using SmkDocServer.Application.Services;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;
using SmkDocServer.Infrastructure.Pdf;
using SmkDocServer.Infrastructure.Services.Document;
using SmkDocServer.Infrastructure.Services.QrCode;
using SmkDocServer.Infrastructure.Services.Barcode;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();

// ตั้งค่า CORS สำหรับ Portal Frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ตั้งค่า Swagger ให้รองรับการใส่ API Key
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "ใส่ API Key ตรงนี้ (ค่า Default คือ: secret-smk-key-2026)",
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
            new List<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection();

// --- 1. Database (PostgreSQL) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") 
                        ?? "Host=db;Database=smkdoc;Username=postgres;Password=password", 
                        sqlOptions => sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null)));

// --- 2. Advanced Document Engines (Qorstack OpenXML & ClosedXML Architecture) ---
builder.Services.AddHttpClient();
builder.Services.Configure<SmkDocServer.Infrastructure.Services.Storage.MinioSettings>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<SmkDocServer.Domain.Models.ReportBroSettings>(builder.Configuration.GetSection("ReportBro"));
builder.Services.AddSingleton<SmkDocServer.Domain.Interfaces.IMinioStorageService, SmkDocServer.Infrastructure.Services.Storage.MinioStorageService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IDocxProcessingService, DocxProcessingService>();
builder.Services.AddScoped<IExcelProcessingService, ExcelProcessingService>();

// --- 3. PDF Converter (Gotenberg) ---
builder.Services.AddHttpClient<IPdfConverter, GotenbergPdfConverter>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GotenbergUrl"] ?? "http://localhost:3000");
});

// --- 5. Application Services ---
builder.Services.AddScoped<IApiKeyService, SmkDocServer.Infrastructure.Services.Security.ApiKeyService>();
builder.Services.AddScoped<IGenerationLogService, SmkDocServer.Infrastructure.Services.Logging.GenerationLogService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ITemplateStorageService, TemplateStorageService>();
builder.Services.AddScoped<ITemplateResolverService, TemplateResolverService>();
builder.Services.AddScoped<ITemplateVersioningService, TemplateVersioningService>();
builder.Services.AddScoped<ITemplateSchemaService, TemplateSchemaService>();
builder.Services.AddScoped<IDataConnectionService, SmkDocServer.Infrastructure.Services.DataSource.DataConnectionService>();
builder.Services.AddScoped<FieldMappingService>();
// ITemplateProcessor registrations (Strategy Pattern — Rule 9)
builder.Services.AddScoped<ITemplateProcessor, SmkDocServer.Infrastructure.Services.Document.HtmlTemplateProcessor>();
builder.Services.AddScoped<ITemplateProcessor, SmkDocServer.Infrastructure.Services.Document.OpenXmlTemplateProcessor>();
// Sprint 8: ReportBro pixel-perfect PDF
builder.Services.AddScoped<ITemplateProcessor, SmkDocServer.Infrastructure.Services.Document.ReportBroProcessor>();

var app = builder.Build();

// --- 6. สร้าง Database อัตโนมัติ (Migration) และซิงก์ Templates & Default Project/Key เข้า DB & MinIO ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    try
    {
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var defaultApiKey = builder.Configuration.GetValue<string>("Security:ApiKey") ?? "secret-smk-key-2026";
        await apiKeyService.EnsureDefaultProjectAndKeyAsync(defaultApiKey);

        var templateStorage = scope.ServiceProvider.GetRequiredService<ITemplateStorageService>();
        await templateStorage.SyncLocalTemplatesAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not initialize database entities or sync templates during startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// เปิดใช้งาน CORS
app.UseCors();

// เปิดใช้งาน API Key Security Middleware
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();
app.MapControllers();

// Root API Health & Status Endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "SMK Document Generation Platform API",
    status = "Online",
    version = "1.0.0",
    docs = "/swagger"
}));

app.Run();
