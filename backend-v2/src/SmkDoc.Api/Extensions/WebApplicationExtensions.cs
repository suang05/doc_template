using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SmkDoc.Api.Contracts.Common;
using SmkDoc.Api.Middleware;

namespace SmkDoc.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void ConfigureHostPort(this WebApplicationBuilder builder)
    {
        var customPort = builder.Configuration["PORT"] ?? builder.Configuration["DOC_SERVER_PORT"];
        if (!string.IsNullOrEmpty(customPort) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
            int.TryParse(customPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
        {
            builder.WebHost.UseKestrel(options => options.ListenAnyIP(port));
        }
    }

    public static WebApplication UsePresentationPipeline(this WebApplication app)
    {
        // 1. Reverse Proxy / Ingress (Traefik / Nginx) - Must be first
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        // 2. Centralized RFC 9457 Exception Handler (.NET 8/10)
        app.UseExceptionHandler();

        // 3. Security Headers & CORS
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseCors();

        // 4. Endpoint Routing (must execute prior to authentication and context resolution)
        app.UseRouting();

        // 5. Authentication & Perimeter Project Context Gateway
        app.UseAuthentication();
        app.UseMiddleware<ProjectContextMiddleware>();
        app.UseAuthorization();

        // 6. Rate Limiting
        app.UseRateLimiter();

        // 6. Controllers with Rate Limiting Policy
        app.MapControllers().RequireRateLimiting("api");

        // 7. OpenAPI & Scalar Interactive Documentation
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("SMK Document Server API")
                   .WithTheme(ScalarTheme.Moon);
        });
        app.MapGet("/swagger", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

        // 8. Health Check Endpoint
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var checks = report.Entries.Select(e => new HealthCheckItem(
                    e.Key,
                    FormatHealthStatus(e.Value.Status),
                    e.Value.Description,
                    (int)e.Value.Duration.TotalMilliseconds));

                var payload = new HealthCheckResponse(
                    FormatHealthStatus(report.Status),
                    checks,
                    (int)report.TotalDuration.TotalMilliseconds);

                await context.Response.WriteAsJsonAsync(payload);
            }
        });

        // 9. Root Service Information
        app.MapGet("/", () =>
        {
            var response = new ServerStatusResponse(
                Service: "SMK Document Server v2",
                Architecture: "Clean Architecture (SDD v1.3)",
                Status: "Operational",
                Version: "2.0.0",
                Docs: "/scalar/v1",
                Openapi: "/openapi/v1.json");

            return Results.Ok(response);
        }).ExcludeFromDescription();

        return app;
    }

    private static string FormatHealthStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy"
    };
}
