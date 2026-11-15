using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.ExceptionHandlers;
using SmkDoc.Api.Filters;

namespace SmkDoc.Api.Extensions;

internal static class ApiCoreExtensions
{
    internal static void AddApiProblemDetails(
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

    internal static void AddApiControllers(this IServiceCollection services)
    {
        var jsonEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

        services.AddControllers(options =>
        {
            options.Filters.Add(new ProducesAttribute("application/json"));
            options.Filters.AddService<ETagResponseFilter>();
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
        services.AddScoped<ETagResponseFilter>();
        services.AddEndpointsApiExplorer();
    }
}
