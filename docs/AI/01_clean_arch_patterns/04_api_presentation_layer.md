# [PATTERN-004]: 04_api_presentation_layer.md — Presentation Layer Design Patterns & HTTP SSoT

<context>
Scope: Presentation Layer Patterns, Thin Controllers, ApiResponse Envelopes, Mutation Semantics (POST Idempotency vs PUT/DELETE ETag), SSRF Defense, and RFC 9457 Exceptions.
SSoT_For: Controller Action Contracts, HTTP Status Mapping, ProblemDetails Translation, Mutation Concurrency Standard, and Connection Test Semantics.
Out_Of_Scope: Application UseCase orchestration (see: 02_application_layer.md), Security authentication middleware (see: ../03_specialized_mechanics/multi_tenant_security_pipeline.md).
Target: `backend-v2/` (`SmkDoc.Api/Controllers/`, `SmkDoc.Api/Common/Responses/`, `SmkDoc.Api/Filters/`, `SmkDoc.Api/Middleware/`).
TriggerSignals: [SIGNAL_API_MUTATION]
</context>

---

## 1. Level 1: Presentation Invariants & HTTP Semantics Matrix

### Architectural Wiring & Data Contracts
| Direction | Upstream Origin | Current Layer Role (HTTP Presentation Gateway) | Downstream Destination |
|---|---|---|---|
| **Wire Inbound** | HTTP Client (Next.js Proxy / ERP) | Resolves Tenant & Auth Context | Controller Action (`[ApiController]`) |
| **Command Dispatch** | Route & Body Parameters | Instantiates Command & forwards to UseCase | Application (`createTemplateUseCase.ExecuteAsync`) |
| **POST Mutation Safeguard**| State-Mutating `POST` Methods | Enforces deduplication via `Idempotency-Key` | Idempotency Middleware / In-Memory Store |
| **PUT/DELETE Concurrency** | State-Mutating `PUT` / `DELETE` | Enforces concurrency via `If-Match` / `ETag` | UseCase Optimistic Concurrency Engine |
| **Wire Egress** | Application DTO (`TemplateResultDto`) | Envelopes into `ApiResponse<T>` with HTTP Status | Client (`200 OK`, `201 Created` + Location) |
| **Streaming Egress** | Binary Render Stream | Streams directly to `Response.Body` | HTTP Chunked Stream (`application/pdf`) |
| **Error Egress** | Unhandled Exceptions | Dispatches to RFC 9457 `ProblemDetails` | HTTP Client (`application/problem+json`) |

### Presentation Invariants & HTTP Semantics Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Controller Logic** | ! Business logic, entity mutation, or SQL queries inside action | >> Thin orchestrator ($\le 5$ lines per action) delegating to UseCase |
| **Response Formats** | ! Raw anonymous objects: `return Ok(new { data = ... })` | >> Strongly-typed `ApiResponse<T>` or `PagedApiResponse<T>` |
| **Error Format** | ! Custom ad-hoc error JSON objects | >> RFC 9457 `ProblemDetails` dispatched via `GlobalExceptionHandler` |
| **Validation Error Payload**| ! Omitting field error dictionary from validation response | >> Structured RFC 9457 `errors` dictionary populated in `problemDetails.Extensions["errors"]` |
| **POST Mutation Semantics** | ! Unprotected POST mutations without idempotency safeguards | >> `[Idempotent]` filter on state-mutating `POST` endpoints (`Idempotency-Key` header) |
| **PUT/DELETE Concurrency** | ! Applying Redis lock idempotency to naturally idempotent PUT/DELETE or allowing lost updates | >> Optimistic Concurrency Control (`ETag` / `If-Match`) via PostgreSQL `xmin` concurrency token; return `412 Precondition Failed` on mismatch |
| **Connectivity Test Semantics**| ! Returning HTTP 400 for negative network reachability results | >> Return HTTP 200 OK with `ApiResponse<ConnectionTestResultDto>` (`{ success: false, message }`); reserve HTTP 4xx strictly for malformed payloads or SSRF blocks (`403 Forbidden`) |
| **SSRF Defense** | ! Probing internal networks via unvalidated database connection strings | >> Pre-flight `ConnectionHostValidator` blocking loopback, link-local, cloud metadata (`169.254.x.x`), and Docker aliases (`postgres`, `minio`, `redis`, `gotenberg`) |
| **Binary Downloads** | ! In-memory `byte[]` arrays or raw MinIO 307 redirects to external clients | >> Server-side direct bounded streaming via `Response.Body` or `File(Stream, ...)` with `81,920` buffer |
| **Route Taxonomy & SSoT** | ! Declaring duplicate routes (`/api/*` alongside `/api/v1/*`) or verb-based paths (`/api/createTemplate`) | >> Strict SSoT on RESTful plural kebab-case `api/v1/{resource}`; immediate deletion of legacy `/api/*` routes across all controllers |
| **Dual-Track Document Pipeline** | ! Forcing all renders through blocking synchronous HTTP requests or exposing unbounded async queues | >> Dual-Track Pipeline: Track 1 (Sync Monaco preview / instant generate with fixed-window `rendering` rate limit 30 req/min), Track 2 (`POST /api/v1/documents/generate-async`, `202 Accepted` + `Location: /api/v1/documents/jobs/{jobId}`, polled via `GET /api/v1/documents/jobs/{jobId}`) |
| **Validation SSoT** | ! Duplicating payload validation across template and document controllers | >> Canonical SSoT strictly at `POST /api/v1/documents/validate/{slug}`; Studio validate reserved for HTML syntax |
| **Integration RBAC** | ! Unrestricted `[Authorize]` allowing general members to manage DB credentials | >> Restrict Data Connection CRUD/Test to `AdminOrSuperAdmin`; restrict Dataset CRUD to `AuthorOrAdmin` |
| **API Key Presentation Boundaries** | ! Merging SuperAdmin governance and tenant sub-key operations into duplicate ambiguous routes | >> Clear separation: Tenant Sub-Key lifecycle on `/api/v1/api-keys` (Project Admin) vs Platform Governance & Break-Glass on `/api/v1/management/api-keys` (SuperAdmin) |
| **Top-Level Arrays** | ! Accepting raw array: `[FromBody] List<TItem>` | >> Wrapped batch envelope: `[FromBody] BatchRequest { Items = [...] }` |
| **Timestamp Wire Format**| ! Unspecified/local datetime strings | >> Strict ISO-8601 UTC with `Z` suffix (`2026-10-21T15:30:00Z`) |
| **OpenAPI Schema Metadata**| ! Action without `[ProducesResponseType]` metadata | >> Explicit `[ProducesResponseType(typeof(ApiResponse<T>), StatusCodes.Status200OK)]` |

---

## 2. Level 2: Core Presentation SSoT ASTs & Cognitive API Engine

### Engineering Rationale & Architectural Decisions
* **Why Thin Controllers (<= 5 Lines):** Keeps controllers purely focused on HTTP transport and identity resolution, preventing business logic duplication and making use cases independently testable.
* **Why POST Idempotency vs PUT/DELETE ETag Concurrency:** `POST` operations create new resources and are naturally non-idempotent; network retries without an `[Idempotent]` filter risk duplicate billing or entity creation. Conversely, `PUT` and `DELETE` are naturally idempotent by HTTP specification; their primary risk is concurrent overwrites ("lost updates"), which is cleanly solved via `If-Match` / `ETag` optimistic concurrency without Redis lock overhead.
* **Why HTTP 200 for Test Connection Probes:** When testing database connectivity (`POST /api/v1/data-connections/test`), an unreachable database or invalid password is a valid diagnostic business result, NOT an HTTP transport failure. Returning HTTP 400 violates REST semantics and breaks automated health check clients. HTTP 4xx is reserved strictly for syntactically malformed JSON (400) or SSRF security violations (403).
* **Why Pre-Flight `ConnectionHostValidator` Defense:** Attacking internal services via SSRF (Server-Side Request Forgery) using customer database strings is a high-risk attack vector. Validating hostnames against blocked infrastructure and resolving DNS prior to opening TCP sockets prevents internal reconnaissance.
* **Why Standard Response Envelopes (`ApiResponse<T>`):** Guarantees consistent client consumption contracts (`data`, `success`, `message`, `timestamp`) across all endpoints.
* **Why Delegated Tenant Sub-Keys (`/api/v1/api-keys`) vs Platform Governance (`/api/v1/management/api-keys`):** Eliminates route ambiguity and operational bottlenecks. Tenant Project Admins manage operational application keys (ERP, CRM) via ambient project context under strict quota guardrails (max 2 keys), while SuperAdmin retains dedicated platform-wide oversight, quota adjustment, and emergency break-glass revocation on `/api/v1/management/...`.
* **Why RFC 9457 Global Exception Handler:** Replaces ad-hoc error formats with an IETF standard, ensuring clients receive machine-parsable error codes and validation details without stack traces.

### Cognitive Presentation Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Uniform Wire Contracts, High-Throughput Egress & Absolute Transport Separation
  TradeOffPriority: ContractConsistency > ZeroCrossTenantLeakage > NetworkPerimeterSecurity > TransportSpeed

ProactiveApiGuards:
  - Scope: SmkDoc.Api.Controllers
    Trigger: Controller action exceeding 5 lines or executing validation logic
    CanonicalAction: Delegate directly to single-responsibility IUseCase<TReq, TRes> >> Ref: [RULE-API-001]

  - Scope: SmkDoc.Api.Controllers
    Trigger: State-mutating POST endpoint declared without [Idempotent] filter
    CanonicalAction: Apply [Idempotent] attribute to ensure deterministic deduplication >> Ref: [TRAP-API-003]

  - Scope: SmkDoc.Api.Controllers
    Trigger: State-mutating PUT or DELETE endpoint declared with [Idempotent] or without If-Match concurrency
    CanonicalAction: Enforce If-Match header and ETag validation via PostgreSQL xmin >> Ref: [RULE-API-001]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Returning HTTP 400 for negative network reachability probe
    CanonicalAction: Return HTTP 200 OK with ApiResponse<ConnectionTestResultDto> >> Ref: [RULE-API-004]

  - Scope: SmkDoc.Api.Responses
    Trigger: Returning raw domain entity or anonymous object new { ... }
    CanonicalAction: Wrap immutable Application DTO in ApiResponse<T> envelope >> Ref: [RULE-API-003]

  - Scope: SmkDoc.Api.Exceptions
    Trigger: Try-catch block inside controller action swallowing or remapping exception
    CanonicalAction: Remove try-catch; let exception bubble up to RFC 9457 GlobalExceptionHandler >> Ref: [RULE-API-002]

CoreExtensionSlots:
  ExceptionHandlerSlot: IExceptionHandler (RFC 9457 ProblemDetails translation)
  IdempotencyStoreSlot: IIdempotencyStore (In-Memory / Redis idempotency cache for POST)
  ApiResponseEnvelopeSlot: ApiResponse<T> / PagedApiResponse<T> (Unified HTTP payload format)
```

### RFC 9457 ProblemDetails Exception Translation Matrix
| Domain / Application Exception | HTTP Status Code | RFC 9457 Title | ProblemDetails Type URI |
|---|---|---|---|
| `ValidationException` | `400 Bad Request` | Validation Error | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `SecurityViolationException` | `403 Forbidden` | Security Violation | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| `ForbiddenException` | `403 Forbidden` | Forbidden | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| `NotFoundException` | `404 Not Found` | Resource Not Found | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| `ConflictException` | `409 Conflict` | Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `PreconditionFailedException`| `412 Precondition Failed` | Precondition Failed | `https://tools.ietf.org/html/rfc9110#section-15.5.13` |
| `DomainValidationException` | `422 Unprocessable`| Domain Rule Violation | `https://tools.ietf.org/html/rfc9110#section-15.5.21` |
| `DataConnectionException` | `500 Internal Error`| Integration Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |
| `DistributedStateUnavailableException` | `503 Service Unavailable` | Distributed State Unavailable | `https://api.sammakorn.co.th/errors/distributed-state-unavailable` |
| Unhandled Exceptions | `500 Internal Error`| Internal Server Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

### Specialized Rate Limiting Policies Matrix
| Policy Name | Target Endpoints | Limit & Window | Rejection Status | Primary Architectural Rationale |
|---|---|---|---|---|
| `auth-strict` | `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh` | 5 permits / 1 min (by IP) | `429 Too Many Requests` | Defends against password brute-force and credential stuffing |
| `rendering` | `POST /api/v1/documents/generate/*`, `preview*`, `render` | 30 permits / 1 min (by Tenant) | `429 Too Many Requests` | Protects Gotenberg Chromium pods from memory saturation and OOM crashes |
| `test-conn` | `POST /api/v1/data-connections/test` | 10 permits / 1 min (by User/IP) | `429 Too Many Requests` | Mitigates internal network scanning, port enumeration, and SSRF probing |
| `api` | All standard CRUD, query, and management endpoints | 60 permits / 1 min (by Partition) | `429 Too Many Requests` | Baseline API throttling for fair resource distribution |

---

## 3. Level 3: Canonical Presentation Archetypes

### Thin Controller Archetype (`TemplateController`)
>> MANDATORY CANONICAL TEMPLATE [RULE-API-001]:
* [MUST] Inherit from `ControllerBase`, apply `[ApiController]`, mark class `sealed`, and define route as `[Route("api/v1/{plural-kebab-resource}")]`.
* [MUST] Inject single-responsibility `IUseCase<TRequest, TResponse>` / `IUseCase<TRequest>` abstractions via C# 13 primary constructor.
* [MUST] Keep action methods thin (<= 5 lines per action): delegate directly to UseCase and envelope result in `ApiResponse<T>`.
* [MUST] Apply `[Idempotent]` attribute strictly on state-mutating `POST` endpoints.
* [MUST] Enforce Optimistic Concurrency via `[FromHeader(Name = "If-Match")] string? ifMatch` on `PUT` and `DELETE` endpoints (prohibit `[Idempotent]` on PUT/DELETE).
* [MUST] Return `CreatedAtAction` with `201 Created` for resource creation, `Ok(new ApiResponse<T>(result))` for reads/updates, `NoContent()` for deletions, and `File(stream, contentType, fileName)` for binary streaming.
```csharp
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

namespace SmkDoc.Api.Controllers.Authoring;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    IUseCase<ListTemplatesQuery, TemplatePagedResultDto> listTemplatesUseCase,
    IUseCase<GetTemplateByIdQuery, TemplateResultDto> getTemplateByIdUseCase,
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase,
    IUseCase<UpdateTemplateCommand, TemplateResultDto> updateTemplateUseCase,
    IUseCase<DeactivateTemplateCommand> deactivateTemplateUseCase,
    IUseCase<RenderDocumentQuery, Stream> renderDocumentUseCase) : ControllerBase
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
        var result = await getTemplateByIdUseCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{result.ConcurrencyToken}\"";
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

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateTemplateDto dto, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var command = new UpdateTemplateCommand(id, dto.Name, dto.Category, ifMatch);
        var result = await updateTemplateUseCase.ExecuteAsync(command, ct);
        Response.Headers.ETag = $"\"{result.ConcurrencyToken}\"";
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await deactivateTemplateUseCase.ExecuteAsync(new DeactivateTemplateCommand(id, ifMatch), ct);
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
            SecurityViolationException sve => (StatusCodes.Status403Forbidden, "Security Violation", sve.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
            ForbiddenException fe => (StatusCodes.Status403Forbidden, "Forbidden", fe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Resource Not Found", nfe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ConflictException ce => (StatusCodes.Status409Conflict, "Conflict", ce.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            PreconditionFailedException pfe => (StatusCodes.Status412PreconditionFailed, "Precondition Failed", pfe.Message, "https://tools.ietf.org/html/rfc9110#section-15.5.13"),
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

### Diagnostic Connection Test Controller Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-API-004]:
* [MUST] Return HTTP `200 OK` with `ApiResponse<ConnectionTestResultDto>` containing `{ success: false, message: ... }` when network reachability fails.
* [MUST] Invoke pre-flight `ConnectionHostValidator.EnsureSafeTarget(host)` throwing `SecurityViolationException` (`403 Forbidden`) on SSRF target attempts.
```csharp
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

namespace SmkDoc.Api.Controllers.Integration;

[ApiController]
[Route("api/v1/data-connections")]
public sealed class DataConnectionController(
    IUseCase<TestDataConnectionCommand, ConnectionTestResultDto> testConnectionUseCase) : ControllerBase
{
    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse<ConnectionTestResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnection([FromBody] TestDataConnectionCommand command, CancellationToken ct)
    {
        var result = await testConnectionUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ConnectionTestResultDto>(result, Success: result.IsSuccessful, Message: result.FailureReason));
    }
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

### [TRAP-API-003]: Split-Brain Mutation Semantics (Applying `[Idempotent]` to PUT/DELETE instead of `If-Match` Concurrency)
```diff
- [HttpPut("{id:guid}")]
- [Idempotent]
- public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateTemplateDto dto)
+ [HttpPut("{id:guid}")]
+ public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateTemplateDto dto, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
```
*Rationale: PUT and DELETE are naturally idempotent by HTTP specification; applying Redis-lock idempotency introduces distributed lock overhead while failing to prevent lost-update concurrency anomalies.*

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

### [TRAP-API-006]: HTTP 400 Returned on Negative Connection Reachability Test
```diff
- if (!testResult.IsSuccess) return BadRequest(new { error = testResult.Error });
+ return Ok(new ApiResponse<ConnectionTestResultDto>(testResult, Success: testResult.IsSuccess, Message: testResult.Error));
```
*Rationale: An unreachable remote host during connection testing is a normal diagnostic result, NOT an HTTP client error. Returning HTTP 400 confuses automated monitoring tools.*

### [TRAP-API-007]: Missing SSRF Pre-Flight Validation on Remote Network Host
```diff
- var connection = new NpgsqlConnection(command.ConnectionString);
- await connection.OpenAsync(ct);
+ ConnectionHostValidator.EnsureSafeTarget(command.Host);
+ var connection = new NpgsqlConnection(command.ConnectionString);
+ await connection.OpenAsync(ct);
```
*Rationale: Directly opening database connections without pre-flight validation allows SSRF attacks targeting container network aliases or cloud metadata endpoints.*
