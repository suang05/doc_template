# [CORE-006]: 06_api_contract_standards.md — Universal API Contract & Wire Protocol SSoT

<context>
Scope: Universal Response Envelopes (`ApiResponse<T>`, `PagedApiResponse<T>`), Pagination Metadata, RFC 9457 `ProblemDetails` Wire Schema, JSON Serialization Invariants (camelCase, ISO-8601 UTC with `Z`), HTTP Status & Mutation Semantics Matrix, Header Protocol (`Idempotency-Key`, `If-Match`, `ETag`, `x-project-id`), Pagination/Query Taxonomy, and OpenAPI Synchronization Governance.
SSoT_For: Cross-Boundary API Wire Contracts between .NET 10 Web API and Next.js 15 Client, OpenAPI Schema Alignment (`specs/openapi.json`), Response Envelope Schema, and Error Schema Protocol.
Out_Of_Scope: Controller implementation details (see: ../01_clean_arch_patterns/04_api_presentation_layer.md), Next.js Route Handlers and React query caching (see: ../02_frontend_patterns/01_app_router_and_data_fetching.md), CQRS internal variable naming (see: 04_naming_standards.md).
Target: Cross-boundary contracts in `backend-v2/` (`SmkDoc.Api/Contracts/`, `SmkDoc.Api/Common/Responses/`, `specs/openapi.json`) and `frontend-v2/` (`src/types/api.ts`, `src/lib/api/client.ts`, `src/features/*/schemas/`).
ArchitecturalSignals: [ARCH_EVOLUTION_CONTRACT]
</context>

---

## 1. Level 1: Affirmative Wire Protocol & Contract Invariants Matrix

### 1.1 Architectural Wiring & Cross-Boundary Contract Flow
| Direction | Upstream Origin | Current Layer Role (Wire Contract Boundary) | Downstream Destination |
|---|---|---|---|
| **Client Wire Request** | Next.js API Proxy / Client | Enforces Headers (`x-project-id`, `If-Match`, `Idempotency-Key`) | ASP.NET Core Request Pipeline |
| **Command Mutation** | HTTP Wire Body (JSON) | Validates JSON against camelCase Contract Schema | Application Command Handler |
| **Single Resource Egress**| Application DTO | Envelopes payload strictly inside `{ "data": T }` | HTTP Client (`200 OK`, `201 Created` + Location) |
| **Paginated Egress** | Application Paged Collection | Envelopes items inside `{ "data": T[], "pagination": { ... } }` | HTTP Client (`200 OK`) |
| **Binary Stream Egress** | Storage / Render Stream | Bypasses JSON; streams live binary chunks (`application/pdf`) | HTTP Wire Body (`81,920`-byte buffer) |
| **Error Egress** | Exception / Validation Failure | Dispatches RFC 9457 `ProblemDetails` (`application/problem+json`)| Client Error Handler (`ApiError`) |

### 1.2 Affirmative Wire Protocol Invariants
| Contract Architectural Concern | Affirmative Wire Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **JSON Field Casing** | Wire JSON serialization is strictly standard `camelCase`. `PascalCase` and `snake_case` on the HTTP wire are prohibited. | Serialization Pipeline |
| **Single Entity Envelope** | All successful non-streaming single-resource endpoints encapsulate payloads in `{ "data": T }`. Unwrapped raw JSON returns are prohibited. | Wire Payload Schema |
| **Paginated Collection Envelope**| All collection endpoints return `{ "data": T[], "pagination": PaginationMetadata }`. Flat pagination fields at root or unwrapped arrays are prohibited. | Collection Egress |
| **RFC 9457 Error Wire Schema** | Errors emit standard RFC 9457 `application/problem+json` containing `type`, `title`, `status`, `detail`, `instance`, and structured field errors in `errors`. Ad-hoc `{ error: "msg" }` JSON is prohibited. | Error Handling Pipeline |
| **Temporal Wire Format** | All datetime values on the wire serialize to ISO-8601 UTC strings terminated with the `Z` suffix (`YYYY-MM-DDTHH:mm:ssZ`). Unspecified local times and Unix epoch integers are prohibited. | Temporal Serialization |
| **Null vs Undefined Semantics** | Missing optional values serialize explicitly as `null` or are omitted per schema. Emitting string `"undefined"` or `"null"` is prohibited. | Wire Payload Fidelity |
| **Mutation Deduplication** | Non-idempotent state mutations (`POST`) require `Idempotency-Key: <uuidv7>`. Duplicate requests replay cached responses without re-execution. | Mutation Boundary |
| **Optimistic Concurrency Wire** | Resource mutations (`PUT`, `DELETE`) require `If-Match: "<token>"` carrying PostgreSQL `xmin` rowversion. Server returns updated token in `ETag` header. | Concurrency Control |
| **Tenant Scope Header** | Multi-tenant context passes explicitly via `x-project-id: <guid>` header or JWT `projectId` claim. Tenant identity in request JSON body is prohibited. | Multi-Tenant Perimeter |
| **RESTful Route Taxonomy** | Public routes use plural kebab-case nouns prefixed with `api/v1/{resources}`. Verb-based routes (`/api/createTemplate`) and legacy `/api/*` are prohibited. | Wire URL Addressing |
| **OpenAPI SSoT Fidelity** | `specs/openapi.json` is the sole machine-readable schema authority, generated deterministically during `.NET` build. Manual edits to `specs/openapi.json` are prohibited. | Schema Governance |

### 1.3 HTTP Status Codes & REST Verb Semantics Matrix
| HTTP Verb | Route Target | Success Status | Egress Contract Shape | Mandatory Headers |
|---|---|---|---|---|
| `GET` | `api/v1/{resources}` | `200 OK` | `PagedApiResponse<T>` (`{ data: [...], pagination: { ... } }`) | `x-project-id` |
| `GET` | `api/v1/{resources}/{id}` | `200 OK` | `ApiResponse<T>` (`{ data: T }`) | `ETag` (response), `x-project-id` |
| `GET` | `api/v1/{resources}/{id}/download` | `200 OK` | Raw Stream (`application/pdf`, `application/vnd.openxmlformats-...`) | `Content-Disposition`, `Content-Type` |
| `POST` | `api/v1/{resources}` | `201 Created` | `ApiResponse<T>` (`{ data: T }`) | `Location: api/v1/{resources}/{id}`, `Idempotency-Key` |
| `POST` | `api/v1/documents/generate-async` | `202 Accepted`| `ApiResponse<JobStatusDto>` (`{ data: { jobId, status } }`) | `Location: api/v1/documents/jobs/{jobId}` |
| `POST` | `api/v1/{resources}/test` | `200 OK` | `ApiResponse<TestResultDto>` (`{ data: { success, message, latencyMs } }`)| `x-project-id` |
| `PUT` | `api/v1/{resources}/{id}` | `200 OK` | `ApiResponse<T>` (`{ data: T }`) | `If-Match` (request), `ETag` (response) |
| `DELETE` | `api/v1/{resources}/{id}` | `204 NoContent`| Empty Body | `If-Match` (request) |

---

## 2. Level 2: Core Wire Contracts & Cognitive Rationale

### 2.1 Engineering Rationale & Architectural Decisions
* **Why the Envelope Pattern (`{ data: ... }`):** Prevents JSON top-level array security vulnerabilities in older clients, ensures consistent consumption contracts across diverse frontend libraries, and leaves extensible room for top-level wire metadata without breaking schemas.
* **Why Nested `pagination` Metadata (`data` + `pagination`):** Separates resource items from collection telemetry (`page`, `limit`, `totalCount`, `totalPages`). Flat pagination fields directly on the root object pollute entity schema contracts and make generic client typing (`ApiResponse<T>`) brittle.
* **Why RFC 9457 ProblemDetails over Ad-Hoc Errors:** Ad-hoc formats like `{ "error": "something went wrong" }` lack machine-parsable semantics. RFC 9457 provides an IETF standard with typed URIs, standardized HTTP status mapping, and structured validation dictionaries under `errors`.
* **Why HTTP 200 for Diagnostic Connection Probes:** An unreachable database or invalid credential is a valid diagnostic business result, NOT an HTTP transport failure. Emitting HTTP 400 confuses automated monitoring and breaks client error handlers.
* **Why Dedicated Machine-Readable `specs/openapi.json`:** Eliminates documentation drift by generating contracts directly from the compiled ASP.NET Core API reflection pipeline, providing frontend TypeScript code-generators a single source of truth.

### 2.2 Proactive Wire Protocol Firewalls
```yaml
WireCasingFirewall:
  Invariant: "All JSON wire properties must be camelCase."
  ActionOnViolation: "Configure System.Text.Json naming policy to JsonNamingPolicy.CamelCase."

ResponseEnvelopeFirewall:
  Invariant: "All non-streaming JSON responses must wrap output in ApiResponse<T> or PagedApiResponse<T>."
  ActionOnViolation: "Wrap raw object or list in new ApiResponse<T>(result) or PagedApiResponse<T>(...)."

PaginationContractFirewall:
  Invariant: "Paginated endpoints must emit { data: T[], pagination: PaginationMetadata }."
  ActionOnViolation: "Nest pagination fields inside PaginationMetadata record; prohibit flat root fields."

ErrorEnvelopeFirewall:
  Invariant: "Errors must serialize as application/problem+json adhering to RFC 9457."
  ActionOnViolation: "Dispatch unhandled and domain exceptions via GlobalExceptionHandler."

HeaderSemanticsFirewall:
  Invariant: "Enforce Idempotency-Key on state-mutating POST; enforce If-Match on PUT/DELETE."
  ActionOnViolation: "Attach [Idempotent] filter to POST; add [FromHeader(Name = 'If-Match')] to PUT/DELETE."
```

### 2.3 Universal Pagination, Sorting & Filtering Taxonomy
All collection query endpoints must conform strictly to these standardized query string parameters:

| Query Parameter | Type | Default Value | Description / Constraint |
|---|---|---|---|
| `page` | `integer` | `1` | 1-based page index ($\ge 1$). |
| `pageSize` | `integer` | `20` | Items per page (clamped strictly between $1 \le \text{pageSize} \le 100$). Accepts alias `limit`. |
| `sortBy` | `string` | `"createdAt"` | Name of property to order by (must be validated against allowed schema fields). |
| `sortDir` | `string` | `"desc"` | Sort direction: `"asc"` (Ascending) or `"desc"` (Descending). |
| `search` | `string?` | `null` | Sanitized substring search query. |
| `isActive` | `boolean?`| `null` | Filter by lifecycle state: `true`, `false`, or omitted for all. |

* **Ingress vs Egress Parameter Parity:** The HTTP Query String accepts either `pageSize` or `limit` (canonical default: `pageSize`). The HTTP Egress Response strictly envelopes items per page under `pagination.limit` conforming to `PaginationMetadata`.

---

## 3. Level 3: Canonical Contract AST Blueprints (C# & TypeScript 1:1 Parity)

### 3.1 Standard Response Envelope Blueprint (`ApiResponse<T>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-CONTRACT-001]:
* Encapsulates single-resource payload in `data`.

**C# SSoT Definition (`backend-v2/src/SmkDoc.Api/Common/Responses/ApiResponse.cs`):**
```csharp
public record ApiResponse<T>(T Data);
```

**TypeScript SSoT Definition (`frontend-v2/src/types/api.ts`):**
```typescript
export interface ApiResponse<T> {
  data: T;
}
```

**JSON Wire Representation:**
```json
{
  "data": {
    "id": "0192e2c8-72b1-7000-8000-111111111111",
    "name": "Standard Invoice",
    "slug": "standard-invoice",
    "category": "Billing",
    "isActive": true,
    "createdAt": "2026-11-06T12:00:00Z"
  }
}
```

---

### 3.2 Paginated Collection Envelope Blueprint (`PagedApiResponse<T>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-CONTRACT-002]:
* Encapsulates items array in `data` and telemetry metadata in `pagination`.

**C# SSoT Definition (`backend-v2/src/SmkDoc.Api/Common/Responses/ApiResponse.cs`):**
```csharp
public record PaginationMetadata(
    int Page,
    int Limit,
    int TotalCount,
    int TotalPages
)
{
    public static PaginationMetadata Create(int totalCount, int page, int limit)
    {
        // Step 1: Clamp bounds defensively <Why: prevent division by zero, negative page indices, and runaway allocations>
        var safeLimit = Math.Max(1, limit);
        var safePage = Math.Max(1, page);
        var safeTotal = Math.Max(0, totalCount);

        // Step 2: Compute ceiling total pages <Why: ensure trailing partial records occupy a dedicated final page>
        var totalPages = (int)Math.Ceiling((double)safeTotal / safeLimit);

        // Step 3: Instantiate immutable telemetry record <Why: guarantee thread-safe deterministic serialization across wire>
        return new PaginationMetadata(safePage, safeLimit, safeTotal, totalPages);
    }
}

public record PagedApiResponse<T>(
    IEnumerable<T> Data,
    PaginationMetadata Pagination
) : ApiResponse<IEnumerable<T>>(Data)
{
    public PagedApiResponse(IEnumerable<T> data, int totalCount, int page, int limit)
        : this(data, PaginationMetadata.Create(totalCount, page, limit))
    {
    }
}
```

**TypeScript SSoT Definition (`frontend-v2/src/types/api.ts`):**
```typescript
export interface PaginationMetadata {
  page: number;
  limit: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedApiResponse<T> extends ApiResponse<T[]> {
  data: T[];
  pagination: PaginationMetadata;
}
```

**JSON Wire Representation:**
```json
{
  "data": [
    {
      "id": "0192e2c8-72b1-7000-8000-111111111111",
      "name": "Standard Invoice",
      "slug": "standard-invoice"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "totalCount": 42,
    "totalPages": 3
  }
}
```

---

### 3.3 RFC 9457 ProblemDetails Error Schema Blueprint
>> MANDATORY CANONICAL BLUEPRINT [RULE-CONTRACT-003]:
* Centralized exception translation producing IETF RFC 9457 compliant error schemas with field-level diagnostics.

**C# SSoT Definition (`backend-v2/src/SmkDoc.Api/ExceptionHandlers/GlobalExceptionHandler.cs`):**
```csharp
// Standard ASP.NET Core RFC 9457 ProblemDetails schema with extensions dictionary for errorCode and field errors
public class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
```

**TypeScript Error Client Definition (`frontend-v2/src/lib/api/client.ts`):**
```typescript
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}
```

**JSON Wire Representation (Validation Failure):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Validation failed.",
  "instance": "/api/v1/templates",
  "errors": {
    "name": [
      "The Name field is required.",
      "The length of 'Name' must be at least 3 characters."
    ],
    "slug": [
      "Slug must contain only lowercase alphanumeric characters and hyphens."
    ]
  }
}
```

---

### 3.4 Diagnostic Probe Result Blueprint (`ApiResponse<TestResultDto>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-CONTRACT-004]:
* Connectivity and diagnostic checks emit HTTP `200 OK` with standardized result payload.

**JSON Wire Representation:**
```json
{
  "data": {
    "success": false,
    "message": "Connection refused: Host 10.0.0.1:5432 unreachable after 3000ms timeout.",
    "latencyMs": 3005,
    "checkedAt": "2026-11-06T12:00:00Z"
  }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-CONTRACT-001]: Flat Pagination Properties Leaking into Root Envelope
```diff
- { "data": [...], "page": 1, "pageSize": 20, "totalCount": 100 }
+ { "data": [...], "pagination": { "page": 1, "limit": 20, "totalCount": 100, "totalPages": 5 } }
```
*Rationale: Flat pagination fields on the root envelope break client generic envelope parsing (`ApiResponse<T>`) and couple pagination telemetry directly to the resource envelope.*

### [TRAP-CONTRACT-002]: Ad-Hoc Error JSON Bypassing RFC 9457 ProblemDetails
```diff
- { "error": "Template not found", "code": 404 }
+ { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Resource Not Found", "status": 404, "detail": "Template with id '...' was not found.", "instance": "/api/v1/templates/..." }
```
*Rationale: Ad-hoc error formats prevent automated client error handling, break typing in client SDKs, and omit machine-readable validation dictionaries.*

### [TRAP-CONTRACT-003]: Raw Unenveloped JSON or Top-Level Arrays
```diff
- [ { "id": "1", "name": "Item 1" }, { "id": "2", "name": "Item 2" } ]
+ { "data": [ { "id": "1", "name": "Item 1" }, { "id": "2", "name": "Item 2" } ], "pagination": { "page": 1, "limit": 20, "totalCount": 2, "totalPages": 1 } }
```
*Rationale: Returning raw top-level JSON arrays exposes endpoints to JSON array hijacking in legacy environments and prevents future wire metadata expansion.*

### [TRAP-CONTRACT-004]: PascalCase or snake_case Property Leaks on the Wire
```diff
- { "Project_Id": "...", "CreatedAt": "2026-11-06T12:00:00Z" }
+ { "projectId": "...", "createdAt": "2026-11-06T12:00:00Z" }
```
*Rationale: Leaking C# PascalCase or database snake_case across the HTTP wire violates JavaScript/TypeScript client ergonomics and breaks OpenAPI contract generation.*

### [TRAP-CONTRACT-005]: Unspecified Local DateTime Strings Lacking ISO-8601 UTC `Z` Suffix
```diff
- { "createdAt": "2026-11-06 12:00:00" }
+ { "createdAt": "2026-11-06T12:00:00Z" }
```
*Rationale: Omitting the UTC timezone specifier (`Z`) causes browsers in different timezones to parse the timestamp into inconsistent local times.*

### [TRAP-CONTRACT-006]: Split-Brain Mutation Semantics (Applying Idempotency-Key on PUT/DELETE)
```diff
- PUT /api/v1/templates/123 [Headers: Idempotency-Key: ...]
+ PUT /api/v1/templates/123 [Headers: If-Match: "54218"]
```
*Rationale: PUT and DELETE are naturally idempotent; applying Idempotency-Key incurs unnecessary distributed lock overhead while failing to protect against lost updates.*

### [TRAP-CONTRACT-007]: Manual Out-of-Band Modification of `specs/openapi.json`
```diff
- // Manually editing specs/openapi.json
+ // Run dotnet build backend-v2/src/SmkDoc.Api to re-generate specs/openapi.json automatically
```
*Rationale: Manual edits to openapi.json are overwritten on the next API build and introduce silent schema drift.*
