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
public sealed class ETagFilter(IConcurrencyContext concurrencyContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Headers.TryGetValue(OpenApiNames.IfMatchHeader, out var rawIfMatch))
        {
            concurrencyContext.IfMatch = rawIfMatch.ToString();
        }

        var executed = await next();

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

