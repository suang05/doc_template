# [PATTERN-004]: 04_api_presentation_layer.md — Presentation Layer Design Patterns & HTTP SSoT

<context>
Scope: Presentation Layer Patterns, Thin Controllers, ApiResponse Envelopes, Idempotency & RFC 9457 Exception Handling
Target: `SmkDoc.Api/` (`Controllers/`, `Common/Responses/`, `Filters/`, `Middleware/`)
Authority: Absolute SSoT Invariant (Thin orchestrators, zero business logic, strictly standardized HTTP wire format)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_API_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Presentation Invariants & HTTP Semantics Matrix

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
| **OpenAPI Schema Metadata** | Action without `[ProducesResponseType]` metadata | Explicit `[ProducesResponseType(typeof(ApiResponse<T>), StatusCodes.Status200OK)]` on all endpoints |

---

## 2. Level 2: Core Presentation SSoT ASTs & Wire Standards

### RFC 9457 ProblemDetails Exception Translation Matrix
| Domain / Application Exception | HTTP Status Code | RFC 9457 Title | ProblemDetails Type URI |
|---|---|---|---|
| `ValidationException` | `400 Bad Request` | Validation Error | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `NotFoundException` | `404 Not Found` | Resource Not Found | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| `ConflictException` | `409 Conflict` | Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `ForbiddenException` | `403 Forbidden` | Forbidden | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| `DomainValidationException` | `422 Unprocessable`| Domain Rule Violation | `https://tools.ietf.org/html/rfc9110#section-15.5.21` |
| Unhandled Exceptions | `500 Internal Error`| Internal Server Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

### OWASP Top-Level Array Envelope
```csharp
// >> CANONICAL: Wrapped batch request envelope
public sealed record BatchCreateFieldsRequest(IReadOnlyList<CreateFieldDto> Items, bool DryRun = false);
```

### UTC ISO-8601 Timestamp Standard
* Format: `yyyy-MM-ddTHH:mm:ssZ` (or with fractional seconds: `yyyy-MM-ddTHH:mm:ss.fffZ`).
* Prohibited: Local time offsets without `Z` (e.g. `2026-10-21 15:30:00` or raw ticks).

---

## 3. Level 3: Canonical Presentation Archetypes

### Thin Controller Archetype (`TemplateController`)
>> MANDATORY CANONICAL TEMPLATE [RULE-API-001]:
* [MUST] Inherit from `ControllerBase`, apply `[ApiController]`, and define route as `[Route("api/v1/{plural-kebab-resource}")]`.
* [MUST] Inject single-responsibility UseCases via C# 13 primary constructor (prohibit generic multi-method service injection).
* [MUST] Keep action methods thin (<= 5 lines per action): resolve identity, delegate to UseCase, and wrap result in standard response envelope.
* [MUST] Apply `[Idempotent]` attribute on all state-mutating endpoints (`POST`, `PUT`, `DELETE`).
* [MUST] Return `CreatedAtAction` with `201 Created` for resource creation, `Ok(new ApiResponse<T>(result))` for reads/updates, and `NoContent()` with `204 NoContent` for deletions.
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

### Standard Response Envelopes
>> MANDATORY CANONICAL TEMPLATE [RULE-ENV-001]:
* [MUST] Encapsulate all single-resource reads, mutations, and actions in `ApiResponse<T>(T Data, bool Success = true, string? Message = null, DateTimeOffset Timestamp = default)`.
* [MUST] Encapsulate all paginated collection queries in `PagedApiResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, bool Success = true)`.
* [MUST NOT] Return anonymous objects (`new { success = true }`) or naked domain entities across the HTTP boundary.
```csharp
namespace SmkDoc.Api.Common.Responses;

public sealed record ApiResponse<T>(T Data, bool Success = true, string? Message = null, DateTimeOffset Timestamp = default)
{
    public DateTimeOffset Timestamp { get; init; } = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp;
}

public sealed record PagedApiResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, bool Success = true)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
```

### Centralized RFC 9457 Global Exception Handler
>> MANDATORY CANONICAL TEMPLATE [RULE-ERR-001]:
* [MUST] Implement Microsoft `IExceptionHandler` registering as the single RFC 9457 gateway.
* [MUST] Map domain and application exceptions strictly according to Level 2 Translation Matrix.
* [MUST] Set `httpContext.Response.ContentType = "application/problem+json"` and serialize RFC 9457 `ProblemDetails`.
* [MUST NOT] Leak stack traces, SQL syntax, or internal connection strings to callers in production environments.
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, title, detail, type) = exception switch
        {
            ValidationException vex => (StatusCodes.Status400BadRequest, "Validation Error", "Validation failed.", "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Resource Not Found", nfe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ConflictException ce => (StatusCodes.Status409Conflict, "Conflict", ce.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            DomainValidationException dve => (StatusCodes.Status422UnprocessableEntity, "Domain Rule Violation", dve.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.21"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.", "https://tools.ietf.org/html/rfc9110#section-15.6.1")
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
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);

        return true;
    }
}
```

---

## 4. Level 4: Anti-Patterns & Micro-Diffs

### [TRAP-API-001]: Fat Controller vs Thin Orchestrator
```csharp
// ! BANNED: Fat controller executing business logic or entity manipulation
[HttpPost]
public async Task<IActionResult> Create(TemplateDto dto)
{
    if (string.IsNullOrEmpty(dto.Name)) return BadRequest();
    var template = new Template { Name = dto.Name };
    await db.Templates.AddAsync(template);
    await db.SaveChangesAsync();
    return Ok(template);
}

// >> CANONICAL: Thin controller (<= 5 lines) delegating to single-responsibility UseCase
[HttpPost]
[Idempotent]
public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, CancellationToken ct)
{
    var result = await createTemplateUseCase.ExecuteAsync(command, ct);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
}
```

### [TRAP-API-002]: Raw Anonymous Object vs Strongly-Typed DTO Envelope
```csharp
// ! BANNED: Returning anonymous objects without standard envelope
return Ok(new { success = true, templateId = id });

// >> CANONICAL: Strongly-typed ApiResponse<T> containing Application DTO
return Ok(new ApiResponse<TemplateResultDto>(result));
```
