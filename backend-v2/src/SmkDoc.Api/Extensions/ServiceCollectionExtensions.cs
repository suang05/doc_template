using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmkDoc.Api.Authentication;
using SmkDoc.Api.Constants;
using SmkDoc.Api.ExceptionHandlers;
using SmkDoc.Api.Filters;
using SmkDoc.Api.HealthChecks;
using SmkDoc.Api.OpenApi.Transformers;
using SmkDoc.Api.RateLimiting;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Infrastructure.Persistence;
using StackExchange.Redis;

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
        services.AddApiRateLimiting(configuration);
        services.AddApiHealthChecks(configuration);

        return services;
    }

    private static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        var jsonEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

        services.AddControllers().AddJsonOptions(options =>
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

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SmkDocPolicies.RequireSuperAdmin, policy =>
            {
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim("SystemRole", RoleConstants.SuperAdmin) ||
                    ctx.User.IsInRole(RoleConstants.SuperAdmin));
            });

            options.AddPolicy(SmkDocPolicies.RequireProjectAdmin, policy =>
            {
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole(RoleConstants.Admin) ||
                    ctx.User.IsInRole(RoleConstants.SuperAdmin) ||
                    (ctx.User.IsInRole("M2MClient") && ctx.User.HasClaim(c => c.Type is "Scope" or "ApiKeyScope" && c.Value == "ReadWrite")));
            });

            options.AddPolicy(SmkDocPolicies.CanManageTemplates, policy =>
            {
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("Author") ||
                    ctx.User.IsInRole("Developer") ||
                    ctx.User.IsInRole(RoleConstants.Admin) ||
                    ctx.User.IsInRole(RoleConstants.SuperAdmin) ||
                    (ctx.User.IsInRole("M2MClient") && ctx.User.HasClaim(c => c.Type is "Scope" or "ApiKeyScope" && c.Value == "ReadWrite")));
            });

            options.AddPolicy(SmkDocPolicies.CanGenerateDocuments, policy =>
            {
                policy.RequireAssertion(ctx => ctx.User.Identity?.IsAuthenticated == true);
            });

            options.AddPolicy(SmkDocPolicies.CanManageDataConnections, policy =>
            {
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole(RoleConstants.Admin) ||
                    ctx.User.IsInRole(RoleConstants.SuperAdmin));
            });

            options.AddPolicy(SmkDocPolicies.CanManageDatasets, policy =>
            {
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("Author") ||
                    ctx.User.IsInRole("Developer") ||
                    ctx.User.IsInRole(RoleConstants.Admin) ||
                    ctx.User.IsInRole(RoleConstants.SuperAdmin) ||
                    (ctx.User.IsInRole("M2MClient") && ctx.User.HasClaim(c => c.Type is "Scope" or "ApiKeyScope" && c.Value == "ReadWrite")));
            });
        });

        return services;
    }

    private static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<SmkDocDocumentTransformer>();
            options.AddOperationTransformer<SmkDocOperationTransformer>();
        });

        return services;
    }

    private static IServiceCollection AddApiRateLimiting(
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
                var windowOptions = new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => windowOptions);
            });

            options.AddPolicy("api", context =>
            {
                var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
                var windowOptions = new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

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
            });

            options.AddPolicy("rendering", context =>
            {
                var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
                var windowOptions = new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

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
            });

            options.AddPolicy("test-conn", context =>
            {
                var partitionKey = RateLimitPartitionKeyResolver.Resolve(context);
                var windowOptions = new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

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
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    Detail = "Rate limit exceeded. Maximum 60 requests per minute per caller partition."
                };
                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
            };
        });

        return services;
    }

    private static IServiceCollection AddApiHealthChecks(
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

        return services;
    }
}
