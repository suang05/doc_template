using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Global action filter that hydrates the ambient If-Match concurrency context on ingress
/// and automatically emits the HTTP ETag response header on 2xx egress when the payload implements IHasConcurrencyToken.
/// </summary>
public sealed class ETagFilter(IConcurrencyContextSetter concurrencySetter) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Step 1: Hydrate ambient If-Match context on request ingress
        if (context.HttpContext.Request.Headers.TryGetValue(OpenApiNames.IfMatchHeader, out var rawIfMatch))
        {
            concurrencySetter.SetIfMatch(rawIfMatch.ToString());
        }

        // Step 2: Execute action pipeline
        var executed = await next();

        // Step 3: Emit quoted ETag header on 2xx response egress if payload carries a concurrency token
        if (executed.Exception is null &&
            executed.Result is ObjectResult { StatusCode: null or >= 200 and < 300, Value: { } value } &&
            !context.HttpContext.Response.Headers.ContainsKey(OpenApiNames.ETagHeader))
        {
            var payload = value is IApiResponse envelope ? envelope.Data : value;
            if (payload is IHasConcurrencyToken { ConcurrencyToken: { Length: > 0 } token })
            {
                context.HttpContext.Response.Headers.ETag = $"\"{token}\"";
            }
        }
    }
}

