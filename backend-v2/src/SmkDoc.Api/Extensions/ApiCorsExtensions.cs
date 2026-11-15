using SmkDoc.Api.Constants;
using SmkDoc.Api.Filters;

namespace SmkDoc.Api.Extensions;

internal static class ApiCorsExtensions
{
    private static readonly string[] AllowedCorsHeaders =
    [
        HttpHeaderConstants.AuthorizationHeader,
        "Content-Type",
        "Accept",
        HttpHeaderConstants.ProjectIdHeader,
        IdempotencyFilter.HeaderName,
        "If-Match",
        "X-Request-Id"
    ];

    private static readonly string[] AllowedCorsMethods =
    [
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS",
        "HEAD"
    ];

    private static readonly string[] ExposedCorsHeaders =
    [
        "ETag",
        "Location",
        "Retry-After",
        "Content-Disposition",
        IdempotencyFilter.HeaderReplayed,
        "Deprecation",
        "Sunset",
        "Link",
        "X-Request-Id"
    ];

    internal static void AddApiCors(
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
                        .WithHeaders(AllowedCorsHeaders)
                        .WithMethods(AllowedCorsMethods)
                        .WithExposedHeaders(ExposedCorsHeaders)
                        .AllowCredentials();
                }
                else
                {
                    policy.WithOrigins();
                }
            });
        });
    }
}
