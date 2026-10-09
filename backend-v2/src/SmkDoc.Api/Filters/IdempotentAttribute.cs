using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Decorator attribute applied to state-mutating controller actions to enable IETF Idempotency-Key protection.
/// Supports both Opt-in (default) and Mandatory validation with configurable TTL.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    /// <summary>
    /// Cache TTL in hours for completed successful responses. Defaults to 24 hours.
    /// </summary>
    public int TtlHours { get; set; } = 24;

    /// <summary>
    /// Indicates whether the Idempotency-Key header is strictly required (Mandatory) or optional (Opt-in).
    /// Defaults to false (Opt-in) for backward compatibility.
    /// </summary>
    public bool Mandatory { get; set; } = false;

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var idempotencyStore = serviceProvider.GetRequiredService<IIdempotencyStore>();
        var executionContext = serviceProvider.GetRequiredService<IExecutionContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyFilter>>();
        var jsonOptions = serviceProvider.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions
            ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        return new IdempotencyFilter(
            idempotencyStore,
            executionContext,
            logger,
            jsonOptions,
            TimeSpan.FromHours(TtlHours),
            Mandatory);
    }
}
