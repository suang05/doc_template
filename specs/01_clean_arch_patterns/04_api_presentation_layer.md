# [PATTERN-004]: 04_api_presentation_layer.md — Presentation Layer Design Patterns & HTTP SSoT

<context>
Scope: Presentation Layer Patterns, Thin Controllers (<= 5 LOC), 3-Step Dispatcher, ApiResponse Envelopes, Mutation Semantics (POST Idempotency vs PUT/DELETE ETag), Controller Partitioning, and RFC 9457 Exceptions.
SSoT_For: Controller Action Contracts, 3-Step Dispatcher Pattern, HTTP Status Mapping, Controller Partitioning & Diagnostics, and RFC 9457 ProblemDetails Translation.
Out_Of_Scope: Application UseCase orchestration (see: 02_application_layer.md), Multi-tenant dual-auth middleware (see: ../03_specialized_mechanics/multi_tenant_security_pipeline.md), SSRF network validator mechanics (see: ../00_architecture_core/03_security_and_performance.md).
Target: `backend-v2/` (`SmkDoc.Api/Controllers/`, `SmkDoc.Api/Common/Responses/`, `SmkDoc.Api/Filters/`, `SmkDoc.Api/Middleware/`).
ArchitecturalSignals: [ARCH_EVOLUTION_API]
</context>

---

## 1. Level 1: Affirmative Presentation Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (HTTP Presentation Gateway) | Downstream Destination |
|---|---|---|---|
| **Wire Inbound** | HTTP Client (Next.js Proxy / ERP) | Resolves Tenant & Auth Context | Controller Action (`[ApiController]`) |
| **Command Dispatch** | Route & Body Parameters | Instantiates Command & forwards to UseCase | Application (`{verb}{Aggregate}UseCase.ExecuteAsync`) |
| **POST Mutation Safeguard**| State-Mutating `POST` Methods | Enforces deduplication via `Idempotency-Key` | Idempotency Middleware / In-Memory Store |
| **PUT/DELETE Concurrency** | State-Mutating `PUT` / `DELETE` | Enforces concurrency via global `ETagFilter` & `If-Match` | Ambient `IConcurrencyContext` & `ConcurrencySaveChangesInterceptor` |
| **Wire Egress** | Application DTO (`{Aggregate}ResultDto`) | Envelopes into `ApiResponse<T>` with HTTP Status | Client (`200 OK`, `201 Created` + Location) |
| **Streaming Egress** | Binary Render Stream | Streams directly to `Response.Body` | HTTP Chunked Stream (`application/pdf`) |
| **Error Egress** | Unhandled Exceptions | Dispatches to RFC 9457 `ProblemDetails` | HTTP Client (`application/problem+json`) |

### 1.2 Affirmative Presentation Invariants
| Presentation Architectural Concern | Affirmative Presentation Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Thin Controller Boundary** | Controllers remain thin orchestrators ($\le 5$ LOC per action) delegating directly to a single-responsibility `IUseCase<TReq, TRes>`. Business logic is strictly sealed in Application/Domain. | Controller Action |
| **Unified Response Envelopes** | All standard JSON responses are wrapped in immutable `ApiResponse<T>` or `PagedApiResponse<T>`. Anonymous object returns (`new { ... }`) are prohibited. | Wire Output |
| **RFC 9457 ProblemDetails** | Unhandled and domain exceptions translate through `GlobalExceptionHandler` and `ProblemDetailsBuilder` into RFC 9457 `ProblemDetails` (`application/problem+json`) with `errorCode`, `traceId` (`httpContext.TraceIdentifier`), and validation details in `Extensions["errors"]`. | Error Pipeline |
| **POST Mutation Idempotency** | State-mutating `POST` endpoints enforce deduplication via `[Idempotent]` attribute consuming the `Idempotency-Key` HTTP header. | Mutation Safety |
| **PUT/DELETE Concurrency** | Naturally idempotent `PUT` and `DELETE` endpoints enforce optimistic concurrency via global `ETagFilter` (capturing `If-Match` into `IConcurrencyContext` on ingress and emitting `ETag` from `IHasConcurrencyToken` on 2xx egress) and `[ProducesResponseType(StatusCodes.Status412PreconditionFailed)]`. Redis lock idempotency on PUT/DELETE is prohibited. | Concurrency Control |
| **Diagnostic Probe Semantics** | Connectivity probes (`POST /api/v1/data-connections/test`) return HTTP 200 OK with `ApiResponse<TestDataConnectionResultDto>` (`{ success: false, message }`). HTTP 4xx is reserved strictly for malformed JSON or SSRF blocks. | Integration Diagnostics |
| **SSRF Perimeter Defense** | Network reachability endpoints execute pre-flight `ConnectionHostValidator.EnsureSafeTarget(host)` blocking loopback, link-local, cloud metadata, and container aliases. | Network Boundary |
| **Zero-LOH Streaming Egress** | Binary render/download endpoints stream live `Stream` directly to `Response.Body` or `File(stream, ...)` with bounded `81,920`-byte buffers. Whole-file `byte[]` buffering is prohibited. | Memory & GC |
| **Plural RESTful Taxonomy** | Routes adhere to plural kebab-case `api/v1/{resources}` on `{Aggregate}sController`. Legacy `/api/*` and verb-based route segments are prohibited. | Route Routing |
| **Dual-Track Rendering** | Document generation is partitioned into Track 1 (Sync Monaco preview / instant generate with `rendering` rate limit 30 req/min) and Track 2 (`POST generate-async`, returning `202 Accepted` + `Location`). | Rendering Gateway |
| **Unified Preview Gateway** | Live preview requests (Monaco raw HTML, template slug, or template UUID) route exclusively through `PreviewDocumentUseCase`. In `TemplateMappingController` (`POST /api/v1/templates/{id}/mappings/preview`), preview calls delegate to `PreviewDocumentUseCase` with `TemplateId`, enforcing a single preview pipeline. | Preview SSoT |
| **Pure Schema Ingestion** | Incoming JSON payloads are validated fail-fast during generation without mutating payload data or duplicating validation endpoints. | Ingestion Pipeline |
| **Declarative Policy Security**| Actions are authorized via declarative `[Authorize(Policy = "...")]` attributes matching the Declarative Authorization Policies Matrix. | Endpoint Authorization |
| **Separated API Key Routes** | Tenant sub-key operations reside on `/api/v1/api-keys` (Project Admin, quota max 2), while platform-wide governance resides on `/api/v1/management/api-keys` (SuperAdmin). | Key Governance |
| **OpenAPI Schema Contracts** | Every controller action declares explicit `[ProducesResponseType(typeof(ApiResponse<T>), 2xx)]` for success envelopes and type-less `[ProducesResponseType(StatusCodes.Status4xx)]` for error statuses (automatically bound to `ProblemDetails` by `OpenApiOperationTransformer`). | Contract Metadata |

---

## 2. Level 2: Core Presentation SSoT Catalogs & Cognitive API Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Thin Controllers (<= 5 Lines):** Keeps controllers purely focused on HTTP transport and identity resolution, preventing business logic duplication and making use cases independently testable.
* **Why POST Idempotency vs PUT/DELETE ETag Concurrency:** `POST` operations create new resources and are naturally non-idempotent; network retries without an `[Idempotent]` filter risk duplicate billing or entity creation. Conversely, `PUT` and `DELETE` are naturally idempotent by HTTP specification; their primary risk is concurrent overwrites ("lost updates"), which is cleanly solved via `If-Match` / `ETag` optimistic concurrency without Redis lock overhead.
* **Why HTTP 200 for Test Connection Probes:** When testing database connectivity (`POST /api/v1/data-connections/test`), an unreachable database or invalid password is a valid diagnostic business result, NOT an HTTP transport failure. Returning HTTP 400 violates REST semantics and breaks automated health check clients. HTTP 4xx is reserved strictly for syntactically malformed JSON (400) or SSRF security violations (403).
* **Why Pre-Flight `ConnectionHostValidator` Defense:** Attacking internal services via SSRF (Server-Side Request Forgery) using customer database strings is a high-risk attack vector. Validating hostnames against blocked infrastructure and resolving DNS prior to opening TCP sockets prevents internal reconnaissance.
* **Why Standard Response Envelopes (`ApiResponse<T>`):** Guarantees consistent client consumption contracts (`{ "data": T }` per `06_api_contract_standards.md` SSoT) across all endpoints.
* **Why Delegated Tenant Sub-Keys (`/api/v1/api-keys`) vs Platform Governance (`/api/v1/management/api-keys`):** Eliminates route ambiguity and operational bottlenecks. Tenant Project Admins manage operational application keys (ERP, CRM) via ambient project context under strict quota guardrails (max 2 keys), while SuperAdmin retains dedicated platform-wide oversight, quota adjustment, and emergency break-glass revocation on `/api/v1/management/...`.
* **Why RFC 9457 Global Exception Handler:** Replaces ad-hoc error formats with an IETF standard, ensuring clients receive machine-parsable error codes and validation details without stack traces.

### 2.2 Proactive Presentation Firewalls
```yaml
ControllerFatLogicFirewall:
  Invariant: "Controller actions must not exceed 5 lines or execute business logic."
  ActionOnViolation: "Delegate action execution directly to single IUseCase<TReq, TRes>."

VariableNamingFirewall:
  Invariant: "UseCase execution output must be assigned strictly to var result."
  ActionOnViolation: "Rename pagedResult, pagedLogs, pdfStream, response to var result."

MutationSemanticsFirewall:
  Invariant: "Apply [Idempotent] strictly to POST; enforce ambient If-Match on PUT/DELETE."
  ActionOnViolation: "Remove [Idempotent] from PUT/DELETE and decorate with [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]."

RateLimitingPolicyFirewall:
  Invariant: "Every controller must be decorated with [EnableRateLimiting] policy."
  ActionOnViolation: "Add [EnableRateLimiting('api')] or specific policy attribute to controller."

NotFoundExceptionFirewall:
  Invariant: "Controllers must never check if (result == null) return NotFound()."
  ActionOnViolation: "Remove null check; UseCase must throw strongly-typed NotFoundException."

ProblemDetailsSSoTFirewall:
  Invariant: "All RFC 9457 ProblemDetails instances must be constructed via ProblemDetailsBuilder with errorCode and traceId."
  ActionOnViolation: "Replace inline new ProblemDetails { ... } with ProblemDetailsBuilder.Create(..., traceId: httpContext.TraceIdentifier)."

ErrorProducesResponseTypeFirewall:
  Invariant: "Error [ProducesResponseType] attributes must never declare typeof(ValidationProblemDetails) or typeof(ApiResponse<T>)."
  ActionOnViolation: "Use parameterless [ProducesResponseType(StatusCodes.Status4xx)] and let OpenApiOperationTransformer attach ProblemDetails."
```

### 2.3 RFC 9457 ProblemDetails Exception Translation Matrix
| Domain / Application / Infrastructure Exception | HTTP Status Code | RFC 9457 Title | `errorCode` | ProblemDetails Type URI |
|---|---|---|---|---|
| `ValidationException` | `400 Bad Request` | Validation Failed | `VALIDATION_FAILED` | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `DomainValidationException` | `400 Bad Request` | Domain Validation Error | `DOMAIN_VALIDATION_ERROR` | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `BusinessRuleViolationException` | `400 Bad Request` | Business Rule Violation | `{DomainErrorCode}` | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `UnauthorizedException` / `UnauthorizedAccessException` | `401 Unauthorized` | Unauthorized | `UNAUTHORIZED` | `https://tools.ietf.org/html/rfc9110#section-15.5.2` |
| `ForbiddenException` / `ProjectDeactivatedException` | `403 Forbidden` | Forbidden / Project Deactivated | `FORBIDDEN` / `PROJECT_DEACTIVATED` | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| `NotFoundException` | `404 Not Found` | Not Found | `{ENTITY}_NOT_FOUND` | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| `ConflictException` | `409 Conflict` | Conflict | `{ENTITY}_CONFLICT` | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `DbUpdateConcurrencyException` | `409 Conflict` | Concurrency Conflict | `CONCURRENCY_CONFLICT` | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| `DraftExpiredException` | `410 Gone` | Draft Expired | `DRAFT_EXPIRED` | `https://tools.ietf.org/html/rfc9110#section-15.5.11` |
| `PreconditionFailedException` | `412 Precondition Failed` | Precondition Failed | `PRECONDITION_FAILED` | `https://tools.ietf.org/html/rfc9110#section-15.5.13` |
| `SchemaValidationException` | `422 Unprocessable Entity` | Schema Validation Failed | `SCHEMA_VALIDATION_FAILED` | `https://tools.ietf.org/html/rfc9110#section-15.5.21` |
| `RenderException` / `DatasetExecutionException` / `DataConnectionDecryptionException` | `500 Internal Server Error` | Render / Dataset / Decryption Failed | `{DomainErrorCode}` | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |
| `DistributedStateUnavailableException` | `503 Service Unavailable` | Distributed State Unavailable | `DISTRIBUTED_STATE_UNAVAILABLE` | `https://tools.ietf.org/html/rfc9110#section-15.6.4` |
| Unhandled `Exception` | `500 Internal Server Error` | Internal Server Error | `INTERNAL_SERVER_ERROR` | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

### 2.4 Specialized Rate Limiting Policies Matrix
| Policy Name | Target Endpoints | Limit & Window | Rejection Status | Primary Architectural Rationale |
|---|---|---|---|---|
| `auth-strict` | `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh` | 5 permits / 1 min (by IP) | `429 Too Many Requests` | Defends against password brute-force and credential stuffing |
| `rendering` | `POST /api/v1/documents/generate/*`, `preview*`, `render` | 30 permits / 1 min (by Tenant) | `429 Too Many Requests` | Protects Gotenberg Chromium pods from memory saturation and OOM crashes |
| `test-conn` | `POST /api/v1/data-connections/test` | 10 permits / 1 min (by User/IP) | `429 Too Many Requests` | Mitigates internal network scanning, port enumeration, and SSRF probing |
| `api` | All standard CRUD, query, and management endpoints | 60 permits / 1 min (by Partition) | `429 Too Many Requests` | Baseline API throttling for fair resource distribution |

### 2.5 Declarative Authorization Policies Matrix (`AuthorizationPolicies`)
The portal operates as an internal operator and authoring console (SuperAdmin and Template Designers); external enterprise callers (ERP, CRM) integrate exclusively via Channel A (M2M API Keys). All controller actions enforce authorization declaratively through policies:

| Policy Name | Target Endpoints & Controller Scope | Allowed Subject Identities | Policy Enforcement Criteria |
|---|---|---|---|
| `RequireSuperAdmin` | `api/v1/management/api-keys`, `api/v1/management/projects/*` | Platform SuperAdmin exclusively | `ctx.User.IsInRole(RoleConstants.SuperAdmin)` \| `ctx.User.HasClaim("SystemRole", SuperAdmin)` |
| `RequireProjectAdmin` | `api/v1/api-keys`, `api/v1/audit-logs`, `api/v1/users` | Project Admin, SuperAdmin, or M2M ReadWrite | `ctx.User.IsInRole(RoleConstants.Admin)` \| `ctx.User.IsInRole(RoleConstants.SuperAdmin)` \| `(IsInRole("M2MClient") & Scope == "ReadWrite")` |
| `CanManageTemplates` | `api/v1/templates`, `api/v1/templates/{id}/studio/*`, `mappings`, `datasets` | Template Designer (`Developer`), Admin, SuperAdmin, or M2M ReadWrite | `IsInRole(RoleConstants.Developer)` \| `IsInRole(RoleConstants.Admin)` \| `IsInRole(RoleConstants.SuperAdmin)` \| `Scope == "ReadWrite"` |
| `CanGenerateDocuments` | `api/v1/documents/*` (`generate`, `generate-async`, `preview`, `download`, `versions`) | Any authenticated user or M2M key (ReadOnly/ReadWrite) | `ctx.User.Identity.IsAuthenticated == true` |
| `CanManageDataConnections`| `api/v1/data-connections/*` (`GET`, `POST`, `PUT`, `DELETE`, `test`) | Project Admin or SuperAdmin | `ctx.User.IsInRole(RoleConstants.Admin)` \| `ctx.User.IsInRole(RoleConstants.SuperAdmin)` |
| `CanManageDatasets` | `api/v1/datasets/*` (`GET`, `POST`, `PUT`, `DELETE`) | Template Designer (`Developer`), Admin, SuperAdmin, or M2M ReadWrite | `IsInRole(RoleConstants.Developer)` \| `IsInRole(RoleConstants.Admin)` \| `IsInRole(RoleConstants.SuperAdmin)` \| `Scope == "ReadWrite"` |

### 2.6 Controller Partitioning & Companion Diagnostics Pattern
When an aggregate's capabilities exceed 5 UseCases, or encompass specialized operational concerns (e.g. diagnostics, reachability tests, dry-run schema introspection), enforce single-responsibility by decomposing into cohesive companion controllers sharing the same route prefix:
* **Primary Resource Controller**: `{Aggregate}sController` handles standard state mutations and queries (List, Get, Create, Update, Deactivate) decorated with `[EnableRateLimiting("api")]`.
* **Companion Diagnostics Controller**: `{Resource}DiagnosticsController` isolates network reachability, test probes, or dry-run introspection calls decorated with dedicated `[EnableRateLimiting("test-conn")]`.

| Partitioning Strategy | Route Scope | Typical Invariants & Policies | Canonical Example |
|---|---|---|---|
| **Primary Resource CRUD** | `api/v1/{resources}` | Standard 3-step dispatcher, `[EnableRateLimiting("api")]`, `[Idempotent]` on POST, `If-Match` on PUT/DELETE | `TemplatesController`, `DataConnectionsController` |
| **Companion Diagnostics** | `api/v1/{resources}` | Reachability checks, dry-run schema introspection, `[EnableRateLimiting("test-conn")]` (10 req/min) | `DataConnectionDiagnosticsController`, `DatasetDiagnosticsController` |
| **Interactive Studio Draft**| `api/v1/{resources}/draft` | RAM-only preview lifecycle, ephemeral streaming, Monaco syntax validation | `TemplateDraftController`, `TemplateStudioController` |

---

## 3. Level 3: Canonical Presentation Archetypes (Surgical AST Recipes)

### 3.1 Thin Primary Controller Archetype (`{Aggregate}sController`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-API-001]:
* Inherits from `ControllerBase`, applies `[ApiController]`, marks class `sealed`, and defines route as `[Route("api/v1/{plural-kebab-resource}")]`.
* Injects single-responsibility `IUseCase<TRequest, TResponse>` abstractions via primary constructor ($\le 5$ dependencies).
* Action methods stay thin ($\le 5$ LOC): execute 3-step dispatcher (Instantiate Input, Execute UseCase to `var result`, Envelope Return).
* Applies `[Idempotent]` strictly to `POST` mutations; declares `[ProducesResponseType(StatusCodes.Status412PreconditionFailed)]` on `PUT` and `DELETE` (with `If-Match` and `ETag` handled transparently by global `ETagFilter`).

```csharp
[ApiController]
[Route("api/v1/{aggregate-kebab-plural}")]
[EnableRateLimiting("api")]
public sealed class {Aggregate}sController(
    IUseCase<List{Aggregate}sQuery, {Aggregate}PagedResultDto> listUseCase,
    IUseCase<Get{Aggregate}ByIdQuery, {Aggregate}ResultDto> getByIdUseCase,
    IUseCase<Create{Aggregate}Command, {Aggregate}ResultDto> createUseCase,
    IUseCase<Update{Aggregate}Command, {Aggregate}ResultDto> updateUseCase,
    IUseCase<Deactivate{Aggregate}Command> deactivateUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<{Aggregate}ResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // Step 1: Instantiate Query <Why: map HTTP query parameters to application contract>
        var query = new List{Aggregate}sQuery(null, null, null, page, pageSize);

        // Step 2: Execute UseCase <Why: execute decoupled orchestration logic>
        var result = await listUseCase.ExecuteAsync(query, ct);

        // Step 3: Envelope Egress <Why: standardized pagination wire schema>
        return Ok(new PagedApiResponse<{Aggregate}ResultDto>(result.Items, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<{Aggregate}ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        // Step 1: Instantiate Query <Why: isolate route parameters into strongly-typed query>
        var query = new Get{Aggregate}ByIdQuery(id);

        // Step 2: Execute UseCase <Why: throws strongly-typed NotFoundException if missing>
        var result = await getByIdUseCase.ExecuteAsync(query, ct);

        // Step 3: Envelope Egress <Why: ETagFilter attaches ETag header from IHasConcurrencyToken>
        return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
    }

    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<{Aggregate}ResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] Create{Aggregate}Request request, CancellationToken ct)
    {
        // Step 1: Instantiate Command <Why: translate transport model to immutable command>
        var command = new Create{Aggregate}Command(request.Name, request.Slug, request.Category);

        // Step 2: Execute UseCase <Why: PEMR execution and atomic commit>
        var result = await createUseCase.ExecuteAsync(command, ct);

        // Step 3: Return Typed REST 201 Created <Why: RFC 7231 Location header compliance>
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<{Aggregate}ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] Update{Aggregate}Request request,
        CancellationToken ct)
    {
        // Step 1: Instantiate Command <Why: ambient ETagFilter captures If-Match header>
        var command = new Update{Aggregate}Command(id, request.Name, request.Category);

        // Step 2: Execute UseCase <Why: ConcurrencySaveChangesInterceptor verifies precondition on commit>
        var result = await updateUseCase.ExecuteAsync(command, ct);

        // Step 3: Envelope Egress <Why: ETagFilter emits updated ETag header automatically>
        return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        // Step 1: Instantiate Command <Why: capture target id; ambient filter captures If-Match>
        var command = new Deactivate{Aggregate}Command(id);

        // Step 2: Execute UseCase <Why: soft deactivation in domain aggregate>
        await deactivateUseCase.ExecuteAsync(command, ct);

        // Step 3: Return 204 NoContent <Why: standard REST deletion response>
        return NoContent();
    }
}
```

### 3.2 Centralized RFC 9457 Global Exception Handler & ProblemDetails SSoT Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-API-002]:
* Implements `IExceptionHandler` (`<= 20` LOC per method) delegating RFC 9457 construction to `ProblemDetailsBuilder` and serialization to `IProblemDetailsService`.
* Attaches `httpContext.TraceIdentifier` as `problemDetails.Extensions["traceId"]` and logs 4xx as `LogWarning` vs 5xx as `LogError` with `{ErrorCode}` and `{TraceId}`.
* Maps `DbUpdateConcurrencyException` to `409 Conflict` (`CONCURRENCY_CONFLICT`) and populates `Extensions["errors"]` for `ValidationException` (400 map) and `SchemaValidationException` (422 rule list).
* Configures `AddProblemDetails` (`CustomizeProblemDetails`) so framework-level 400 errors also carry `instance`, `errorCode`, and `traceId`.

```csharp
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string UnexpectedErrorDetail = "An unexpected error occurred.";
    private const string ConcurrencyConflictDetail =
        "The resource was modified by another transaction while your request was processing. Please reload the latest state and retry.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // Step 1: Map exception to HTTP status, title, and stable errorCode <Why: deterministic classification>
        var (status, title, errorCode) = ResolveExceptionDetails(exception);

        // Step 2: Log semantically (4xx warning vs 5xx error with TraceId) <Why: prevent false 5xx alert fatigue>
        LogException(exception, status, errorCode, httpContext.TraceIdentifier);

        // Step 3: Build standardized RFC 9457 ProblemDetails via SSoT builder <Why: guarantee traceId and errorCode>
        var problemDetails = BuildProblemDetails(httpContext, exception, status, title, errorCode);

        // Step 4: Write response via IProblemDetailsService <Why: standard application/problem+json pipeline>
        await WriteResponseAsync(httpContext, problemDetails, exception, ct);
        return true;
    }

    private void LogException(Exception exception, int status, string errorCode, string traceId)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled 5xx Server Error [{ErrorCode}] (TraceId: {TraceId}): {Message}", errorCode, traceId, exception.Message);
            return;
        }

        logger.LogWarning("Handled 4xx Client/Domain Error [{ErrorCode}] (TraceId: {TraceId}): {Message}", errorCode, traceId, exception.Message);
    }

    private static ProblemDetails BuildProblemDetails(
        HttpContext httpContext,
        Exception exception,
        int status,
        string title,
        string errorCode) =>
        ProblemDetailsBuilder.Create(
            status: status,
            title: title,
            detail: ResolveDetailMessage(exception, status),
            errorCode: errorCode,
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier,
            errors: ExtractValidationErrors(exception));

    private static string ResolveDetailMessage(Exception exception, int status) =>
        exception switch
        {
            DbUpdateConcurrencyException => ConcurrencyConflictDetail,
            _ when status == StatusCodes.Status500InternalServerError && exception is not DomainException => UnexpectedErrorDetail,
            _ => exception.Message
        };

    private static object? ExtractValidationErrors(Exception exception) =>
        exception switch
        {
            IValidationException validationEx => validationEx.Errors,
            _ => null
        };

    private async ValueTask WriteResponseAsync(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        Exception exception,
        CancellationToken ct)
    {
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: ct);
        }
    }

    private static (int Status, string Title, string ErrorCode) ResolveExceptionDetails(Exception exception) =>
        exception switch
        {
            DomainException domainEx => (ResolveDomainStatus(domainEx), ResolveDomainTitle(domainEx), domainEx.ErrorCode),
            PreconditionFailedException pfe => (StatusCodes.Status412PreconditionFailed, "Precondition Failed", pfe.ErrorCode),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency Conflict", "CONCURRENCY_CONFLICT"),
            DistributedStateUnavailableException dsue => (StatusCodes.Status503ServiceUnavailable, "Distributed State Unavailable", dsue.ErrorCode),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "BAD_REQUEST"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "INTERNAL_SERVER_ERROR")
        };

    private static int ResolveDomainStatus(DomainException exception) =>
        exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ForbiddenException => StatusCodes.Status403Forbidden,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ValidationException => StatusCodes.Status400BadRequest,
            SchemaValidationException => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };

    private static string ResolveDomainTitle(DomainException exception) =>
        exception.GetType().Name.Replace("Exception", string.Empty);
}
```

### 3.3 Unified Response Envelopes & Global ETag Filter Archetypes
>> MANDATORY CANONICAL BLUEPRINT [RULE-API-003]:
* Encapsulates single-resource responses in immutable `ApiResponse<T>(T Data) : IApiResponse`.
* Encapsulates collection responses in `PagedApiResponse<T>(IEnumerable<T> Data, PaginationMetadata Pagination)`.
* `ETagFilter` hydrates `IConcurrencyContext.IfMatch` on ingress and attaches `Response.Headers.ETag` from `IApiResponse` + `IHasConcurrencyToken` on 2xx egress without reflection.
>> SSoT DELEGATION: Universal response envelopes (`ApiResponse<T>`, `PagedApiResponse<T>`) and RFC 9457 error wire schemas are governed strictly by [06_api_contract_standards.md](../00_architecture_core/06_api_contract_standards.md).

```csharp
public interface IApiResponse
{
    object? Data { get; }
}

public record ApiResponse<T>(T Data) : IApiResponse
{
    object? IApiResponse.Data => Data;
}

public sealed class ETagFilter(IConcurrencyContext concurrencyContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Step 1: Capture If-Match Header on Ingress <Why: auto-sanitizes inside IConcurrencyContext.IfMatch setter>
        if (context.HttpContext.Request.Headers.TryGetValue(HttpHeaderConstants.IfMatch, out var rawIfMatch))
            concurrencyContext.IfMatch = rawIfMatch.ToString();

        var executed = await next();

        // Step 2: Emit ETag Header on 2xx Egress <Why: zero-reflection unwrapping via IApiResponse & IHasConcurrencyToken>
        if (executed.Exception is null &&
            executed.Result is ObjectResult { StatusCode: null or (>= 200 and < 300), Value: IApiResponse { Data: IHasConcurrencyToken { ConcurrencyToken: { Length: > 0 } token } } })
        {
            context.HttpContext.Response.Headers.ETag = $"\"{token}\"";
        }
    }
}
```

### 3.4 Companion Diagnostics Controller Archetype (`{Resource}DiagnosticsController`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-API-004]:
* Separates diagnostic, reachability, or schema introspection endpoints into companion controllers to uphold the $\le 5$ use cases limit.
* Applies dedicated rate limiting (`[EnableRateLimiting("test-conn")]`) on diagnostic routes.
* Returns HTTP `200 OK` with `ApiResponse<{Resource}DiagnosticResultDto>` containing `{ success: false, message: ... }` on reachability failure.

```csharp
[ApiController]
[Route("api/v1/{resource-kebab-plural}")]
[Authorize(Policy = AuthorizationPolicies.CanManageDataConnections)]
public sealed class {Resource}DiagnosticsController(
    IUseCase<Test{Resource}Command, Test{Resource}ResultDto> testUseCase) : ControllerBase
{
    [HttpPost("test")]
    [EnableRateLimiting("test-conn")]
    [ProducesResponseType(typeof(ApiResponse<Test{Resource}ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnection([FromBody] Test{Resource}Request request, CancellationToken ct)
    {
        // Step 1: Translate Request to Diagnostic Command <Why: encapsulate test connection parameters>
        var command = new Test{Resource}Command(request.Provider, request.ConnectionString);

        // Step 2: Execute Diagnostic Probe <Why: reachability failure returns business result, not HTTP 400>
        var result = await testUseCase.ExecuteAsync(command, ct);

        // Step 3: Return Envelope <Why: consistent client diagnostic contract>
        return Ok(new ApiResponse<Test{Resource}ResultDto>(result));
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-API-001]: Fat Controller Action & Business Logic Leakage
```diff
- [HttpPost] public async Task<IActionResult> Create(Create{Aggregate}Request req) { if (string.IsNullOrEmpty(req.Name)) return BadRequest(); var t = {Aggregate}.Create(...); await repo.AddAsync(t); await uow.CommitAsync(); return Ok(t); }
+ [HttpPost] [Idempotent] public async Task<IActionResult> Create([FromBody] Create{Aggregate}Request request, CancellationToken ct) { var command = new Create{Aggregate}Command(request.Name, request.Slug); var result = await useCase.ExecuteAsync(command, ct); return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result)); }
```
*Rationale: Placing business logic in controllers violates single responsibility, bypasses application pipelines, and makes endpoint testing cumbersome.*

### [TRAP-API-002]: Ad-Hoc Anonymous Response Envelope
```diff
- return Ok(new { success = true, result = {aggregate}Dto });
+ return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
```
*Rationale: Anonymous object returns produce untyped OpenAPI schemas and break client code-generator typing.*

### [TRAP-API-003]: Split-Brain Mutation Semantics or Imperative ETag/If-Match Controller Plumbing
```diff
- [HttpPut("{id:guid}")]
- [Idempotent]
- public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Update{Aggregate}Request request, [FromHeader(Name = "If-Match")] string? ifMatch)
+ [HttpPut("{id:guid}")]
+ [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
+ public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Update{Aggregate}Request request, CancellationToken ct)
```
*Rationale: PUT and DELETE are naturally idempotent by HTTP specification; applying `[Idempotent]` introduces distributed lock overhead, while manually binding `[FromHeader(Name = "If-Match")]` or setting `Response.Headers.ETag` in controllers duplicates cross-cutting logic handled by `ETagFilter`.*

### [TRAP-API-004]: Multi-MB File Download via RAM Byte Array
```diff
- var bytes = await storage.DownloadAsync(id, ct); return File(bytes, "application/pdf", "output.pdf");
+ Response.ContentType = "application/pdf"; await storageStream.CopyToAsync(Response.Body, 81920, ct);
```
*Rationale: Buffering binary documents into `byte[]` arrays causes Large Object Heap fragmentation under concurrent downloads.*

### [TRAP-API-005]: Direct Domain Entity Egress Across Controller Boundary
```diff
- [HttpGet("{id}")] public async Task<ActionResult<{Aggregate}>> Get(Guid id) => Ok(await repo.GetByIdAsync(new {Aggregate}Id(id)));
+ [HttpGet("{id:guid}")] public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct) {
+     var query = new Get{Aggregate}ByIdQuery(id);
+     var result = await useCase.ExecuteAsync(query, ct);
+     return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
+ }
```
*Rationale: Returning Domain Entities directly exposes internal data models, navigation proxies, and private domain invariants to outside callers.*

### [TRAP-API-006]: HTTP 400 Returned on Negative Connection Reachability Test
```diff
- if (!result.Success) return BadRequest(new { error = result.Error });
+ return Ok(new ApiResponse<Test{Resource}ResultDto>(result));
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

### [TRAP-API-008]: Double UseCase Calls & Manual Re-Querying in Controller Action
```diff
- [HttpPost("{id:guid}/items")]
- public async Task<IActionResult> AddItem([FromRoute] Guid id, [FromBody] AddItemRequest request, CancellationToken ct)
- {
-     var command = new AddItemCommand(id, request.Name);
-     await addItemUseCase.ExecuteAsync(command, ct);
-     var query = new GetItemsQuery(id);
-     var result = await getItemsUseCase.ExecuteAsync(query, ct);
-     return Ok(new ApiResponse<IReadOnlyList<ItemDto>>(result));
- }
+ [HttpPost("{id:guid}/items")]
+ [Idempotent]
+ public async Task<IActionResult> AddItem([FromRoute] Guid id, [FromBody] AddItemRequest request, CancellationToken ct)
+ {
+     var command = new AddItemCommand(id, request.Name);
+     var result = await addItemUseCase.ExecuteAsync(command, ct);
+     return Ok(new ApiResponse<IReadOnlyList<ItemDto>>(result));
+ }
```
*Rationale: Calling a command UseCase followed by a query UseCase in a single controller action creates distributed transaction/isolation anomalies, breaks thin controller invariants (<= 5 LOC), and doubles database roundtrips. Command UseCases must return the updated collection or resource directly.*

### [TRAP-API-009]: Manual return NotFound() Bypassing RFC 9457 Global Exception Handler
```diff
- var result = await get{Aggregate}ByIdUseCase.ExecuteAsync(query, ct);
- if (result == null) return NotFound();
- return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
+ var result = await get{Aggregate}ByIdUseCase.ExecuteAsync(query, ct);
+ return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
```
*Rationale: Manual null-checks returning raw NotFound() produce inconsistent error schemas and bypass the centralized RFC 9457 ProblemDetails pipeline. Application UseCases must throw strongly-typed NotFoundException.*

### [TRAP-API-010]: Raw StatusCode(201/202) Returns Violating REST Location Contracts
```diff
- var result = await create{Aggregate}UseCase.ExecuteAsync(command, ct);
- return StatusCode(StatusCodes.Status201Created, new ApiResponse<{Aggregate}ResultDto>(result));
+ var result = await create{Aggregate}UseCase.ExecuteAsync(command, ct);
+ return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result));
```
*Rationale: Using raw StatusCode(201, ...) or StatusCode(202, ...) omits the mandatory RFC 7231 / RFC 9110 Location header. Typed helpers CreatedAtAction and AcceptedAtAction ensure standard discoverability for downstream REST clients.*

### [TRAP-API-011]: Unthrottled Public Controller Endpoint Missing [EnableRateLimiting]
```diff
- [ApiController]
- [Route("api/v1/{aggregate-kebab-plural}")]
- public sealed class {Aggregate}sController : ControllerBase
+ [ApiController]
+ [Route("api/v1/{aggregate-kebab-plural}")]
+ [EnableRateLimiting("api")]
+ public sealed class {Aggregate}sController : ControllerBase
```
*Rationale: Exposing public API controllers without declarative rate limiting leaves services vulnerable to volumetric denial-of-service, runaway automation scripts, and cross-tenant resource starvation. Controllers and sensitive endpoints must be bound to their designated rate limiting policy.*

### [TRAP-API-012]: Ad-Hoc ProblemDetails Instantiation or Typed Error [ProducesResponseType] Leaks
```diff
- [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
- var problem = new ProblemDetails { Status = 400, Title = "Bad Request", Detail = msg }; // Missing errorCode & traceId
+ [ProducesResponseType(StatusCodes.Status400BadRequest)]
+ var problem = ProblemDetailsBuilder.Create(StatusCodes.Status400BadRequest, "Bad Request", msg, "BAD_REQUEST", context.Request.Path, context.TraceIdentifier);
```
*Rationale: Instantiating raw ProblemDetails inline omits mandatory errorCode and traceId extensions. Declaring typeof(ApiResponse<object>) or typeof(ValidationProblemDetails) on 4xx attributes pollutes OpenAPI components.schemas with unused types that fail Redocly lint.*
