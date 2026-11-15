using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.OpenApi.Transformers;

/// <summary>
/// Operation-level OpenAPI contract: stable operationId, security override for public routes,
/// Idempotency-Key / If-Match / ETag headers, and RFC 9457 error responses.
/// </summary>
public sealed class OpenApiOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] VersionedResponseCodes = ["200", "201", "202"];

    private static readonly IReadOnlyDictionary<string, string> DefaultErrorDescriptions =
        new Dictionary<string, string>
        {
            ["401"] = "Unauthorized - missing or invalid credentials.",
            ["403"] = "Forbidden - authenticated but not permitted.",
            ["422"] = "Unprocessable Entity - semantic or schema validation failed.",
            ["429"] = "Too Many Requests - rate limit exceeded (see Retry-After header).",
            ["500"] = "Internal Server Error."
        };

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Step 1: Identity & Lifecycle
        ApplyOperationId(operation, context.Description);
        ApplyDeprecationStatus(operation, context.Description);

        // Step 2: Security
        var isPublic = IsPublicRoute(context.Description);
        if (isPublic)
        {
            operation.Security = [];
        }

        // Step 3: Headers
        ApplyProjectIdHeader(operation, context.Description, isPublic);
        ApplyIdempotencyHeader(operation, context.Description);
        ApplyIfMatchRequestHeader(operation, context.Description);
        ApplyETagResponseHeader(operation, context.Description);

        // Step 4: Error contract
        AddDefaultErrorResponses(operation, isPublic);
        ConvertErrorResponsesToProblemDetails(operation);

        return Task.CompletedTask;
    }

    private static void ApplyOperationId(OpenApiOperation operation, ApiDescription description)
    {
        var routeValues = description.ActionDescriptor.RouteValues;
        var hasController = routeValues.TryGetValue("controller", out var controller);
        var action = ResolveActionName(description.ActionDescriptor, routeValues);
        if (!hasController || action is null)
        {
            return;
        }

        operation.OperationId = OpenApiOperationIdFactory.Create(controller!, action);
    }

    /// <summary>
    /// MVC strips the "Async" suffix from the routed action name, which would make
    /// <c>Generate</c> and <c>GenerateAsync</c> collide - prefer the real method name.
    /// </summary>
    private static string? ResolveActionName(ActionDescriptor descriptor, IDictionary<string, string?> routeValues)
    {
        if (descriptor is ControllerActionDescriptor { MethodInfo: { } methodInfo })
        {
            return methodInfo.Name;
        }

        return routeValues.TryGetValue("action", out var action) ? action : null;
    }

    private static string NormalizeRoutePath(string? relativePath)
    {
        var trimmed = (relativePath ?? string.Empty).Trim('/');
        return string.Create(trimmed.Length + 1, trimmed, static (span, source) =>
        {
            span[0] = '/';
            source.AsSpan().CopyTo(span[1..]);
        });
    }

    private static bool IsPublicRoute(ApiDescription description)
    {
        var path = NormalizeRoutePath(description.RelativePath);
        return OpenApiNames.PublicRoutes.Contains(path, StringComparer.OrdinalIgnoreCase) ||
               path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyDeprecationStatus(OpenApiOperation operation, ApiDescription description)
    {
        if (description.ActionDescriptor is not ControllerActionDescriptor cad)
        {
            return;
        }

        bool hasObsolete = cad.MethodInfo?.IsDefined(typeof(ObsoleteAttribute), inherit: false) ?? false;
        bool hasDeprecatedFilter = description.ActionDescriptor.EndpointMetadata?
            .OfType<DeprecatedEndpointAttribute>()
            .Any(attr => attr.MatchesPath(description.RelativePath)) ?? false;

        if (hasObsolete || hasDeprecatedFilter)
        {
            operation.Deprecated = true;
        }
    }

    private static void ApplyProjectIdHeader(OpenApiOperation operation, ApiDescription description, bool isPublic)
    {
        if (isPublic)
        {
            return;
        }

        var path = NormalizeRoutePath(description.RelativePath);
        if ((path.StartsWith("/api/v1/management", StringComparison.OrdinalIgnoreCase) &&
             !path.StartsWith("/api/v1/management/settings/fonts", StringComparison.OrdinalIgnoreCase)) ||
            path.StartsWith("/api/v1/portal", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Parameters ??= [];
        if (operation.Parameters.Any(p => string.Equals(p.Name, OpenApiNames.ProjectIdHeader, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = OpenApiNames.ProjectIdHeader,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Target tenant project UUID. Required for Bearer JWT requests on data-plane endpoints; optional for API keys (must match if supplied).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "uuid"
            }
        });
    }

    private static void ApplyIdempotencyHeader(OpenApiOperation operation, ApiDescription description)
    {
        var idempotent = description.ActionDescriptor.EndpointMetadata?
            .OfType<IdempotentAttribute>()
            .FirstOrDefault();
        if (idempotent is null)
        {
            return;
        }

        operation.Parameters ??= [];
        var existingIdempotency = operation.Parameters.FirstOrDefault(p =>
            p.In == ParameterLocation.Header &&
            string.Equals(p.Name, OpenApiNames.IdempotencyKeyHeader, StringComparison.OrdinalIgnoreCase));
        if (existingIdempotency is not null)
        {
            operation.Parameters.Remove(existingIdempotency);
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = OpenApiNames.IdempotencyKeyHeader,
            In = ParameterLocation.Header,
            Required = idempotent.Mandatory,
            Description = $"Client-generated unique key making retries safe. Completed responses are replayed for {idempotent.TtlHours} hours.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }

    private static void ApplyIfMatchRequestHeader(OpenApiOperation operation, ApiDescription description)
    {
        bool hasFromIfMatch = HasIfMatchAttribute(description);
        bool declaresPreconditionStatus =
            operation.Responses?.ContainsKey("412") == true ||
            operation.Responses?.ContainsKey("428") == true;

        if (!hasFromIfMatch && !declaresPreconditionStatus)
        {
            return;
        }

        if (hasFromIfMatch)
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd("412", new OpenApiResponse
            {
                Description = "Precondition Failed - stale resource version or malformed If-Match ETag."
            });
            operation.Responses.TryAdd("428", new OpenApiResponse
            {
                Description = "Precondition Required - missing required If-Match header."
            });
        }

        SetIfMatchParameter(operation, required: hasFromIfMatch);
    }

    private static bool HasIfMatchAttribute(ApiDescription description) =>
        description.ActionDescriptor is ControllerActionDescriptor { MethodInfo: { } methodInfo } &&
        methodInfo.GetParameters().Any(parameter => parameter.IsDefined(typeof(FromIfMatchAttribute), inherit: false));

    private static void SetIfMatchParameter(OpenApiOperation operation, bool required)
    {
        const string ifMatchDescription =
            "Optimistic concurrency version token (strong ETag in \"v{version}\" format returned by prior read).";

        operation.Parameters ??= [];
        var existingIfMatch = operation.Parameters.FirstOrDefault(parameter =>
            parameter.In == ParameterLocation.Header &&
            string.Equals(parameter.Name, OpenApiNames.IfMatchHeader, StringComparison.OrdinalIgnoreCase));

        if (existingIfMatch is OpenApiParameter concreteParam)
        {
            concreteParam.Required = required;
            concreteParam.Description = ifMatchDescription;
            concreteParam.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
            return;
        }

        if (existingIfMatch is not null)
        {
            operation.Parameters.Remove(existingIfMatch);
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = OpenApiNames.IfMatchHeader,
            In = ParameterLocation.Header,
            Required = required,
            Description = ifMatchDescription,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }

    private static void ApplyETagResponseHeader(OpenApiOperation operation, ApiDescription description)
    {
        if (operation.Responses is null)
        {
            return;
        }

        var hasIfMatch = operation.Parameters?.Any(parameter =>
            parameter.In == ParameterLocation.Header &&
            string.Equals(parameter.Name, OpenApiNames.IfMatchHeader, StringComparison.OrdinalIgnoreCase)) == true;

        var returnsVersionToken = description.SupportedResponseTypes
            .Where(responseType =>
                responseType.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created or StatusCodes.Status202Accepted &&
                responseType.Type is not null)
            .Any(responseType => UnwrapsToVersionToken(responseType.Type!));

        if (!hasIfMatch && !returnsVersionToken)
        {
            return;
        }

        foreach (var statusCode in VersionedResponseCodes)
        {
            if (operation.Responses.TryGetValue(statusCode, out var response) &&
                response is OpenApiResponse concreteResponse)
            {
                concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteResponse.Headers[OpenApiNames.ETagHeader] = new OpenApiHeader
                {
                    Description = "Updated resource concurrency version token (strong ETag in \"v{version}\" format).",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }

    private static bool UnwrapsToVersionToken(Type responseType)
    {
        if (typeof(IHasVersion).IsAssignableFrom(responseType))
        {
            return true;
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ApiResponse<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            return typeof(IHasVersion).IsAssignableFrom(innerType);
        }

        return false;
    }

    private static void AddDefaultErrorResponses(OpenApiOperation operation, bool isPublic)
    {
        operation.Responses ??= [];
        foreach (var (statusCode, description) in DefaultErrorDescriptions)
        {
            var isAuthResponse = statusCode is "401" or "403";
            if (isPublic && isAuthResponse)
            {
                continue;
            }

            operation.Responses.TryAdd(statusCode, new OpenApiResponse { Description = description });
        }
    }

    private static void ConvertErrorResponsesToProblemDetails(OpenApiOperation operation)
    {
        var errorCodes = operation.Responses!.Keys.Where(IsErrorStatusCode).ToArray();
        foreach (var statusCode in errorCodes)
        {
            var existing = operation.Responses[statusCode];
            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = existing.Description,
                Headers = existing.Headers,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [OpenApiNames.ProblemJsonMediaType] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(OpenApiNames.ProblemDetailsSchema)
                    }
                }
            };
        }
    }

    private static bool IsErrorStatusCode(string statusCode) =>
        statusCode.Length == 3 && statusCode[0] is '4' or '5';
}

