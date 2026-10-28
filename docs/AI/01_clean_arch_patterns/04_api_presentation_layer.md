# [PATTERN-004]: 04_api_presentation_layer.md — Presentation Layer Design Patterns & HTTP SSoT

<context>
Scope: Presentation Layer Patterns, Thin Controllers, ApiResponse Envelopes, Idempotency, Binary Streaming & RFC 9457 Exception Handling
Target: `SmkDoc.Api/` (`Controllers/`, `Common/Responses/`, `Filters/`, `Middleware/`)
Authority: Absolute SSoT Invariant (Thin orchestrators, zero business logic, strictly standardized HTTP wire format)
Invariants: Thin Actions (<=5 LOC), RFC 9457 ProblemDetails, Idempotency on Mutating Routes, ApiResponse Envelope, Direct Stream Egress
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_API_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Presentation Invariants & HTTP Semantics Matrix

### Architectural Wiring & Data Contracts
| Direction | Upstream Origin | Current Layer Role (HTTP Presentation Gateway) | Downstream Destination |
|---|---|---|---|
| **Wire Inbound** | HTTP Client (Next.js Proxy / ERP) | Resolves Tenant & Auth Context | Controller Action (`[ApiController]`) |
| **Command Dispatch** | Route & Body Parameters | Instantiates Command & forwards to UseCase | Application (`createTemplateUseCase.ExecuteAsync`) |
| **Mutation Safeguard**| Mutating HTTP Methods (`POST`, `PUT`, `DELETE`) | Enforces deduplication via `Idempotency-Key` | Idempotency Middleware / In-Memory Store |
| **Wire Egress** | Application DTO (`TemplateResultDto`) | Envelopes into `ApiResponse<T>` with HTTP Status | Client (`200 OK`, `201 Created` + Location) |
| **Streaming Egress** | Binary Render Stream | Streams directly to `Response.Body` | HTTP Chunked Stream (`application/pdf`) |
| **Error Egress** | Unhandled Exceptions | Dispatches to RFC 9457 `ProblemDetails` | HTTP Client (`application/problem+json`) |

### Presentation Invariants & HTTP Semantics Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Controller Logic** | Business logic, entity mutation, or SQL queries inside action | Thin orchestrator ($\le 5$ lines per action) delegating to UseCase |
| **Response Formats** | Raw anonymous objects: `return Ok(new { data = ... })` | Strongly-typed `ApiResponse<T>` or `PagedApiResponse<T>` |
| **Error Format** | Custom ad-hoc error JSON objects | RFC 9457 `ProblemDetails` dispatched via `GlobalExceptionHandler` |
| **Validation Error Payload**| Omitting field error dictionary from validation response | Structured RFC 9457 `errors` dictionary populated in `problemDetails.Extensions["errors"]` |
| **Mutating Routes** | Unprotected mutations without idempotency safeguards | `[Idempotent]` filter on `POST`, `PUT`, `DELETE` routes |
| **Binary Downloads** | In-memory `byte[]` arrays returned via `File(byte[], ...)` | Direct streaming via `Response.Body` or `File(Stream, ...)` |
| **Route Naming** | Inconsistent verb/camelCase routes: `/api/createTemplate` | RESTful plural kebab-case: `api/v1/templates`, `api/v1/data-sources` |
| **Top-Level Arrays** | Accepting raw array: `[FromBody] List<TItem>` | Wrapped batch envelope: `[FromBody] BatchRequest { Items = [...] }` |
| **Timestamp Wire Format**| Unspecified/local datetime strings | Strict ISO-8601 UTC with `Z` suffix (`2026-10-21T15:30:00Z`) |
| **OpenAPI Schema Metadata**| Action without `[ProducesResponseType]` metadata | Explicit `[ProducesResponseType(typeof(ApiResponse<T>), StatusCodes.Status200OK)]` |

---

## 2. Level 2: Core Presentation SSoT ASTs & Cognitive API Engine

### Engineering Rationale & Architectural Decisions
* **Why Thin Controllers (<= 5 Lines):** Keeps controllers purely focused on HTTP transport and identity resolution, preventing business logic duplication and making use cases independently testable.
* **Why Standard Response Envelopes (`ApiResponse<T>`):** Guarantees consistent client consumption contracts (`data`, `success`, `message`, `timestamp`) across all endpoints.
* **Why RFC 9457 Global Exception Handler:** Replaces ad-hoc error formats with an IETF standard, ensuring clients receive machine-parsable error codes and validation details without stack traces.
* **Why Idempotency Keys on Mutations:** Protects distributed systems against network retries or double-submission bugs, ensuring operations execute exactly once.
* **Why Primary Constructor Injection:** Enforces explicit, compile-time verified dependencies with zero boilerplate assignment ceremony.

### Cognitive Presentation Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Uniform Wire Contracts, High-Throughput Egress & Absolute Transport Separation
  TradeOffPriority: ContractConsistency > TransportSpeed > FaultTolerance > CustomErrorPayloads

ProactiveApiGuards:
  - Scope: SmkDoc.Api.Controllers
    Trigger: Controller action exceeding 5 lines or executing validation logic
    CanonicalAction: Delegate directly to single-responsibility IUseCase<TReq, TRes> >> Ref: [RULE-API-001]

  - Scope: SmkDoc.Api.Controllers
    Trigger: POST, PUT, or DELETE endpoint declared without [Idempotent] filter
    CanonicalAction: Apply [Idempotent] attribute to ensure deterministic deduplication

  - Scope: SmkDoc.Api.Responses
    Trigger: Returning raw domain entity or anonymous object new { ... }
    CanonicalAction: Wrap immutable Application DTO in ApiResponse<T> envelope >> Ref: [RULE-API-003]

  - Scope: SmkDoc.Api.Exceptions
    Trigger: Try-catch block inside controller action swallowing or remapping exception
    CanonicalAction: Remove try-catch; let exception bubble up to RFC 9457 GlobalExceptionHandler >> Ref: [RULE-API-002]

CoreExtensionSlots:
  ExceptionHandlerSlot: IExceptionHandler (RFC 9457 ProblemDetails translation)
  IdempotencyStoreSlot: IIdempotencyStore (In-Memory / Redis idempotency cache)
  ApiResponseEnvelopeSlot: ApiResponse<T> / PagedApiResponse<T> (Unified HTTP payload format)
```

### RFC 9457 ProblemDetails Exception Translation Matrix
| Domain / Application Exception | HTTP Status Code | RFC 9457 Title | ProblemDetails Type URI |
|---|---|---|---|
| `ValidationException` | `400 Bad Request` | Validation Error | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `NotFoundException` | `404 Not Found` | Resource Not Found | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| `ConflictException` | `409 Conflict` | Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `ForbiddenException` | `403 Forbidden` | Forbidden | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| `DomainValidationException` | `422 Unprocessable`| Domain Rule Violation | `https://tools.ietf.org/html/rfc9110#section-15.5.21` |
| `DataConnectionDecryptionException` | `500 Internal Error`| Integration Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |
| `DatasetExecutionException` | `500 Internal Error`| Integration Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |
| Unhandled Exceptions | `500 Internal Error`| Internal Server Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

### OWASP Top-Level Array Envelope
```csharp
// >> CANONICAL: Wrapped batch request envelope preventing JSON array injection
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
* [MUST] Return `CreatedAtAction` with `201 Created` for resource creation, `Ok(new ApiResponse<T>(result))` for reads/updates, `NoContent()` with `204 NoContent` for deletions, and `File(stream, contentType, fileName)` for binary streaming.
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
using SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

namespace SmkDoc.Api.Controllers.Authoring;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    ListTemplatesUseCase listTemplatesUseCase,
    GetTemplateByIdUseCase getTemplateByIdUseCase,
    CreateTemplateUseCase createTemplateUseCase,
    DeactivateTemplateUseCase deactivateTemplateUseCase,
    RenderDocumentUseCase renderDocumentUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListTemplatesQuery(page, pageSize);
        var result = await listTemplatesUseCase.ExecuteAsync(query, ct);
        return Ok(new PagedApiResponse<TemplateResultDto>(result.Items, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateByIdQuery(id);
        var result = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }

    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, CancellationToken ct)
    {
        var result = await createTemplateUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
    {
        var command = new DeactivateTemplateCommand(id);
        await deactivateTemplateUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/render")]
    [Idempotent]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Render([FromRoute] Guid id, [FromBody] RenderDocumentQuery query, CancellationToken ct)
    {
        var stream = await renderDocumentUseCase.ExecuteAsync(query, ct);
        return File(stream, "application/pdf", $"{id}.pdf");
    }
}
```

### Centralized RFC 9457 Global Exception Handler Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-API-002]:
* [MUST] Implement `IExceptionHandler` from `Microsoft.AspNetCore.Diagnostics`.
* [MUST] Map domain and application exceptions strictly according to Level 2 Translation Matrix.
* [MUST] Populate `problemDetails.Extensions["errors"]` with structured validation dictionary when handling `ValidationException`.
* [MUST] Write JSON responses using `application/problem+json` content type.
* [MUST NOT] Leak raw stack traces or internal database error messages to public callers.
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, detail, type) = exception switch
        {
            ValidationException vex => (StatusCodes.Status400BadRequest, "Validation Error", "Validation failed.", "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Resource Not Found", nfe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ConflictException ce => (StatusCodes.Status409Conflict, "Conflict", ce.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            ForbiddenException fe => (StatusCodes.Status403Forbidden, "Forbidden", fe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
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

### Unified Response Envelopes Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-API-003]:
* [MUST] Encapsulate all JSON API responses in immutable `ApiResponse<T>` or `PagedApiResponse<T>`.
* [MUST] Populate `Timestamp` using UTC ISO-8601 formatting.
* [MUST] Provide computed pagination properties (`TotalPages`, `HasNextPage`, `HasPreviousPage`) in `PagedApiResponse<T>`.
```csharp
namespace SmkDoc.Api.Common.Responses;

public record ApiResponse<T>(
    T Data,
    bool Success = true,
    string? Message = null,
    DateTimeOffset Timestamp = default)
{
    public DateTimeOffset Timestamp { get; init; } = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp;
}

public sealed record PagedApiResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    bool Success = true,
    string? Message = null,
    DateTimeOffset Timestamp = default) : ApiResponse<IReadOnlyList<T>>(Items, Success, Message, Timestamp)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-API-001]: Fat Controller Action & Business Logic Leakage
```diff
- [HttpPost] public async Task<IActionResult> Create(CreateTemplateRequest req) { if (string.IsNullOrEmpty(req.Name)) return BadRequest(); var t = Template.Create(...); await repo.AddAsync(t); await uow.CommitAsync(); return Ok(t); }
+ [HttpPost] [Idempotent] public async Task<IActionResult> Create([FromBody] CreateTemplateCommand cmd, CancellationToken ct) { var result = await useCase.ExecuteAsync(cmd, ct); return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result)); }
```
*Rationale: Placing business logic in controllers violates single responsibility, bypasses application pipelines, and makes endpoint testing cumbersome.*

### [TRAP-API-002]: Ad-Hoc Anonymous Response Envelope
```diff
- return Ok(new { success = true, result = templateDto });
+ return Ok(new ApiResponse<TemplateResultDto>(templateDto));
```
*Rationale: Anonymous object returns produce untyped OpenAPI schemas and break client code-generator typing.*

### [TRAP-API-003]: Missing Idempotency Filter on Mutation Route
```diff
- [HttpPost("render")] public async Task<IActionResult> Render([FromBody] RenderDocumentCommand command)
+ [HttpPost("render")] [Idempotent] public async Task<IActionResult> Render([FromBody] RenderDocumentCommand command, CancellationToken ct)
```
*Rationale: Mutating endpoints without `[Idempotent]` safeguards cause duplicate billing, redundant document renders, and race conditions on client retry.*

### [TRAP-API-004]: Multi-MB File Download via RAM Byte Array
```diff
- var bytes = await storage.DownloadAsync(id, ct); return File(bytes, "application/pdf", "output.pdf");
+ response.ContentType = "application/pdf"; await storageStream.CopyToAsync(response.Body, 81920, ct);
```
*Rationale: Buffering binary documents into `byte[]` arrays causes Large Object Heap fragmentation under concurrent downloads.*

### [TRAP-API-005]: Direct Domain Entity Egress Across Controller Boundary
```diff
- [HttpGet("{id}")] public async Task<ActionResult<Template>> Get(Guid id) => Ok(await repo.GetByIdAsync(new TemplateId(id)));
+ [HttpGet("{id:guid}")] public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct) => Ok(new ApiResponse<TemplateResultDto>(await useCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct)));
```
*Rationale: Returning Domain Entities directly exposes internal data models, navigation proxies, and private domain invariants to outside callers.*
