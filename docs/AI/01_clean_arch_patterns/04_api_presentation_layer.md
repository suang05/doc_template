# [PATTERN-004]: 04_api_presentation_layer.md — Presentation Layer Design Patterns & HTTP SSoT

<context>
Scope: Presentation Layer Patterns, Thin Controllers, ApiResponse Envelopes, Idempotency & RFC 9457 Exception Handling
Target: `SmkDoc.Api/` (`Controllers/`, `Common/Responses/`, `Filters/`, `Middleware/`)
Authority: Absolute SSoT Invariant (Thin orchestrators, zero business logic, strictly standardized HTTP wire format)
Boundary: Presentation controllers, HTTP wire format & error handling. Prohibit business rules, persistence logic, or task roadmaps.
</context>

---

## 1. Presentation Layer Invariants & HTTP Semantics

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Controller Logic** | Business logic, entity mutation, or SQL queries inside action | Thin orchestrator ($\le 5$ lines per action) delegating to UseCase |
| **Response Formats** | Raw anonymous objects: `return Ok(new { data = ... })` | Strongly-typed `ApiResponse<T>` or `PagedApiResponse<T>` |
| **Error Format** | Custom ad-hoc error JSON objects | RFC 9457 `ProblemDetails` dispatched via `GlobalExceptionHandler` |
| **Mutating Routes** | Unprotected mutations without idempotency safeguards | `[Idempotent]` filter on `POST`, `PUT`, `DELETE` routes |
| **Binary Downloads** | In-memory `byte[]` arrays returned via `File(byte[], ...)` | Direct streaming via `Response.Body` |
| **Route Naming** | Inconsistent verb/camelCase routes: `/api/createTemplate` | RESTful plural kebab-case: `api/v1/templates`, `api/v1/data-sources` |
| **Top-Level Arrays** | Accepting raw array: `[FromBody] List<TItem>` | Wrapped batch envelope: `[FromBody] BatchRequest { Items = [...] }` |
| **Timestamp Wire Format**| Unspecified/local datetime strings | Strict ISO-8601 UTC with `Z` suffix (`2026-10-21T15:30:00Z`) |

---

## 2. Thin Controller Archetype

```csharp
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

namespace SmkDoc.Api.Controllers.Authoring;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    ListTemplatesUseCase listTemplatesUseCase,
    GetTemplateByIdUseCase getTemplateByIdUseCase,
    CreateTemplateUseCase createTemplateUseCase,
    DeactivateTemplateUseCase deactivateTemplateUseCase,
    IExecutionContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var targetProjectId = context.ProjectId ?? projectId ?? throw new BadHttpRequestException("ProjectId required.");
        var result = await listTemplatesUseCase.ExecuteAsync(new ListTemplatesQuery(targetProjectId), ct);
        return Ok(new ApiResponse<IEnumerable<TemplateResultDto>>(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await getTemplateByIdUseCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }

    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, CancellationToken ct)
    {
        var result = await createTemplateUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        await deactivateTemplateUseCase.ExecuteAsync(new DeactivateTemplateCommand(id), ct);
        return NoContent();
    }
}
```

---

## 3. Standard Response Envelopes

### Single Item Envelope (`ApiResponse<T>`)
```csharp
namespace SmkDoc.Api.Common.Responses;

public sealed record ApiResponse<T>(
    T Data,
    bool Success = true,
    string? Message = null,
    DateTimeOffset Timestamp = default)
{
    public DateTimeOffset Timestamp { get; init; } = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp;
}
```

### Paged List Envelope (`PagedApiResponse<T>`)
```csharp
namespace SmkDoc.Api.Common.Responses;

public sealed record PagedApiResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    bool Success = true)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
```

---

## 4. Centralized RFC 9457 Global Exception Handler

All application and domain exceptions are intercepted and translated into RFC 9457 `ProblemDetails` via ASP.NET Core `IExceptionHandler`:

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, detail, type) = exception switch
        {
            ValidationException vex => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "One or more validation errors occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),

            NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                nfe.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"),

            ConflictException ce => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ce.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),

            DomainValidationException dve => (
                StatusCodes.Status422UnprocessableEntity,
                "Domain Rule Violation",
                dve.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.21"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected server error occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException validationEx)
        {
            problemDetails.Extensions["errors"] = validationEx.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

---

## 5. OWASP Top-Level JSON Array Defense & Wire Standards

### [RULE-API-001]: Top-Level Request Wrapping
Binding raw JSON arrays directly (`[FromBody] List<TItem> items`) is prohibited.
1. **Security (JSON Hijacking)**: While modern browsers protect against classic `__proto__` script tag exploits, raw arrays remain vulnerable in non-browser JSON evaluators.
2. **Contract Extensibility**: Wrapping arrays in an object envelope allows non-breaking schema evolution (e.g., adding `dryRun`, `batchId`, `mode` properties later).

```csharp
// BANNED:
// public async Task<IActionResult> BatchCreate([FromBody] List<CreateFieldDto> items)

// MANDATORY SSoT:
public sealed record BatchCreateFieldsRequest(
    IReadOnlyList<CreateFieldDto> Items,
    bool DryRun = false);

[HttpPost("batch")]
[Idempotent]
public async Task<IActionResult> BatchCreate(
    [FromBody] BatchCreateFieldsRequest request,
    CancellationToken ct)
{
    var result = await batchCreateFieldsUseCase.ExecuteAsync(new BatchCreateFieldsCommand(request.Items, request.DryRun), ct);
    return Ok(new ApiResponse<BatchCreateFieldsResultDto>(result));
}
```

### [RULE-API-002]: Timestamp Wire Format
All timestamps crossing the API boundary MUST serialize as UTC ISO-8601 with explicit `Z` suffix.
* Format: `yyyy-MM-ddTHH:mm:ssZ` (or with fractional seconds: `yyyy-MM-ddTHH:mm:ss.fffZ`).
* Prohibited: Local time offsets without `Z` (e.g. `2026-10-21 15:30:00` or raw ticks).

