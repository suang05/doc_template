using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmkDoc.Api.Authentication;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.ExceptionHandlers;
using SmkDoc.Api.Filters;
using SmkDoc.Api.HealthChecks;
using SmkDoc.Api.OpenApi.Transformers;
using SmkDoc.Api.RateLimiting;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.RateLimiting;
using StackExchange.Redis;

namespace SmkDoc.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddApiProblemDetails(configuration);

        services.AddApiControllers();
        services.AddApiCors(configuration);
        services.AddApiAuthentication(configuration);
        services.AddApiOpenApi();
        services.AddApiRateLimiting(configuration);
        services.AddApiHealthChecks(configuration);

        return services;
    }

    private static void AddApiProblemDetails(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var errorCode = context.ProblemDetails.Extensions.TryGetValue("errorCode", out var existingCode) &&
                                existingCode is string existingCodeStr &&
                                !string.IsNullOrWhiteSpace(existingCodeStr)
                    ? existingCodeStr
                    : ProblemDetailsBuilder.ResolveDefaultErrorCode(context.ProblemDetails.Status);

                context.ProblemDetails.Type ??= ProblemDetailsBuilder.ResolveTypeUri(
                    errorCode,
                    ProblemDetailsBuilder.TryParseBaseUri(configuration["Problems:BaseUri"])).OriginalString;
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions.TryAdd("errorCode", errorCode);
                context.ProblemDetails.Extensions.TryAdd("traceId", ProblemDetailsBuilder.ResolveTraceId(context.HttpContext));
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
    }

    private static void AddApiControllers(this IServiceCollection services)
    {
        var jsonEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

        services.AddControllers(options =>
        {
            options.Filters.Add(new ProducesAttribute("application/json"));
            options.Filters.AddService<ETagFilter>();
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.JsonSerializerOptions.Converters.Add(jsonEnumConverter);
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.Converters.Add(jsonEnumConverter);
        });

        services.AddScoped<IdempotencyFilter>();
        services.AddScoped<ETagFilter>();
        services.AddEndpointsApiExplorer();
    }

    private static void AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (string.IsNullOrWhiteSpace(configuration["Cors:AllowedOrigins"])
                ? []
                : configuration["Cors:AllowedOrigins"]!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? "Production";
        bool isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        string[] allowedOrigins = configuredOrigins;
        if (allowedOrigins.Length == 0)
        {
            allowedOrigins = isDevelopment
                ? ["http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:3000", "http://127.0.0.1:5173"]
                : [];
        }

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
    }

    private static void AddApiAuthentication(
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

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SmartScheme";
                options.DefaultAuthenticateScheme = "SmartScheme";
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("SmartScheme", "Bearer or ApiKey", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey(HttpHeaderConstants.ApiKeyHeader))
                    {
                        return ApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorization(ConfigureAuthorizationPolicies);
    }

    private static void ConfigureAuthorizationPolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(AuthorizationPolicies.RequireSuperAdmin, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim("SystemRole", RoleConstants.SuperAdmin) ||
                ctx.User.IsInRole(RoleConstants.SuperAdmin));
        });

        options.AddPolicy(AuthorizationPolicies.RequireProjectAdmin, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });

        options.AddPolicy(AuthorizationPolicies.CanManageTemplates, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Developer) ||
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });

        options.AddPolicy(AuthorizationPolicies.CanGenerateDocuments, policy =>
        {
            policy.RequireAssertion(ctx => ctx.User.Identity?.IsAuthenticated == true);
        });

        options.AddPolicy(AuthorizationPolicies.CanManageDataConnections, policy =>
        {
            policy.RequireAssertion(ctx => ctx.User.IsInRole(RoleConstants.Admin));
        });

        options.AddPolicy(AuthorizationPolicies.CanManageDatasets, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Developer) ||
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });
    }

    private static bool HasReadWriteM2MScope(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext ctx) =>
        ctx.User.IsInRole("M2MClient") &&
        ctx.User.HasClaim(c => c.Type is "Scope" or "ApiKeyScope" && c.Value == "ReadWrite");

    private static void AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<OpenApiDocumentTransformer>();
            options.AddOperationTransformer<OpenApiOperationTransformer>();
        });
    }

    private static void AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        bool useRedis = RedisConfigurationResolver.ResolveConnectionString(configuration) is not null;
        string instancePrefix = RedisConfigurationResolver.ResolveInstancePrefix(configuration);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("auth-strict", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => CreateFixedWindowOptions(5));
            });

            options.AddPolicy("api", context => CreateFixedWindowPartition(context, 60, useRedis, instancePrefix));
            options.AddPolicy("rendering", context => CreateFixedWindowPartition(context, 30, useRedis, instancePrefix));
            options.AddPolicy("test-conn", context => CreateFixedWindowPartition(context, 10, useRedis, instancePrefix));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) => HandleRateLimitRejectedAsync(context, configuration, ct);
        });
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext context,
        int permitLimit,
        bool useRedis,
        string instancePrefix)
    {
        var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
        var windowOptions = CreateFixedWindowOptions(permitLimit);

        if (useRedis)
        {
            var multiplexer = context.RequestServices.GetService<IConnectionMultiplexer>();
            if (multiplexer is not null)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger<RedisFixedWindowRateLimiter>();

                return RateLimitPartition.Get(
                    partitionKey,
                    key => new RedisFixedWindowRateLimiter(multiplexer, key, windowOptions, logger, instancePrefix));
            }
        }

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => windowOptions);
    }

    private static async ValueTask HandleRateLimitRejectedAsync(
        Microsoft.AspNetCore.RateLimiting.OnRejectedContext context,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        var problemDetails = ProblemDetailsBuilder.Create(
            status: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests",
            detail: "Rate limit exceeded. Maximum 60 requests per minute per caller partition.",
            errorCode: SmkDoc.Domain.Exceptions.ErrorCodes.RateLimited,
            instance: context.HttpContext.Request.Path,
            traceId: ProblemDetailsBuilder.ResolveTraceId(context.HttpContext),
            typeBaseUri: ProblemDetailsBuilder.TryParseBaseUri(configuration["Problems:BaseUri"]));
        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
    }

    private static void AddApiHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: ["db"])
            .AddCheck<GotenbergHealthCheck>("gotenberg", tags: ["deps"])
            .AddCheck<MinioHealthCheck>("minio", tags: ["deps"]);

        if (RedisConfigurationResolver.ResolveConnectionString(configuration) is not null)
        {
            healthChecks.AddCheck<RedisHealthCheck>("redis", tags: ["deps"]);
        }
    }
}
