using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmkDoc.Api.ExceptionHandlers;
using SmkDoc.Api.Filters;
using SmkDoc.Api.HealthChecks;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddApiControllers();
        services.AddApiCors(configuration);
        services.AddApiAuthentication(configuration);
        services.AddApiOpenApi();
        services.AddApiRateLimiting();
        services.AddApiHealthChecks();

        return services;
    }

    private static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        var jsonEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

        services.AddControllers(options => { options.Filters.Add<ValidateCommandFilter>(); }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.Converters.Add(jsonEnumConverter);
        });

        services.AddScoped<IdempotencyFilter>();
        services.AddEndpointsApiExplorer();

        return services;
    }

    private static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (string.IsNullOrWhiteSpace(configuration["Cors:AllowedOrigins"])
                ? []
                : configuration["Cors:AllowedOrigins"]!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? "Production";
        bool isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        var allowedOrigins = configuredOrigins.Length > 0
            ? configuredOrigins
            : isDevelopment
                ? ["http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:3000", "http://127.0.0.1:5173"]
                : Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    policy.WithOrigins();
                }
            });
        });

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        {
            throw new InvalidOperationException(
                "JWT Secret is required and must be at least 32 characters (256 bits). Set Jwt:Secret in appsettings.json or environment variables.");
        }

        var signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "SmkDoc",
                    ValidAudience = configuration["Jwt:Audience"] ?? "SmkDoc.Portal",
                    IssuerSigningKey = signingKey
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "SAMMAKORN Document Generation Service API",
                    Version = "v2",
                    Description =
                        "Enterprise document generation microservice accepting JSON payloads and generating pixel-perfect PDF, DOCX, or XLSX output based on SDD v1.3."
                };

                var apiKeyScheme = new OpenApiSecurityScheme
                {
                    Description = "Enter API Key in header: X-API-Key",
                    Name = "X-API-Key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                };

                var bearerScheme = new OpenApiSecurityScheme
                {
                    Description = "Enter JWT Bearer token: Authorization: Bearer <token>",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };

                var components = document.Components ??= new OpenApiComponents();
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["ApiKey"] = apiKeyScheme;
                components.SecuritySchemes["Bearer"] = bearerScheme;

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("ApiKey")] = []
                });

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("api", context =>
            {
                var apiKeyHeader = context.Request.Headers["X-API-Key"].ToString();
                var partitionKey = string.IsNullOrEmpty(apiKeyHeader)
                    ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                    : apiKeyHeader;

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
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
                context.HttpContext.Response.ContentType = "application/problem+json";
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    Detail = "Rate limit exceeded. Maximum 60 requests per minute per API key."
                };
                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
            };
        });

        return services;
    }

    private static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: ["db"])
            .AddCheck<GotenbergHealthCheck>("gotenberg", tags: ["deps"])
            .AddCheck<MinioHealthCheck>("minio", tags: ["deps"]);

        return services;
    }
}
