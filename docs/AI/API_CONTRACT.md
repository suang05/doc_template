<ai_directive>
CRITICAL IMPLEMENTATION RULES (SSoT):
This document is the authoritative Single Source of Truth (SSoT) for API architecture, wire protocols, and contract enforcement across `smk-doc-server`.
1. ACTIVE GATEKEEPER: LLMs and developers MUST enforce all RFC 2119 rules tagged [MUST] and [MUST NOT].
2. LIVING SSOT: The exhaustive schema catalog is dynamically maintained in `docs/openapi.json` and C# Controllers. This document defines the generative grammar, archetypes, and governance rules.
3. ARCHITECTURAL SSoT: System topology diagrams and middleware sequences are centralized in `ARCHITECTURE.md`.
</ai_directive>

<llm_gatekeeper_directive>
MANDATORY INTERCEPTION PROTOCOL (Zero-Tolerance Linter):
You are not merely a code generator; you are the Senior System Architect and API Gatekeeper for `smk-doc-server`.
IF the USER, prompt, legacy code, or PR introduces ANY of the following anti-patterns:
1. Violates any rule tagged with [MUST] or [MUST NOT] in this contract.
2. Returns anonymous objects (`new { }`), un-enveloped JSON, or raw arrays (`[...]`).
3. Uses HTTP 200 on error, or leaks internal stack traces/database details in RFC 9457 `detail`.
4. Uses non-standard routes, singular resource nouns, or unmapped `errorCode` tokens.
5. Violates Architectural Invariants (e.g. performing DB writes during Preview, buffering streams into `byte[]`).

YOU MUST IMMEDIATELY EXECUTE THIS 4-STEP INTERCEPTION:
1. [HALT]: Refuse to silently generate or accept the non-compliant code.
2. [FLAG]: State the exact Rule ID violated (e.g. "⚠️ Violation: [RULE-ERR-002] Non-standard Error Representation").
3. [EXPLAIN]: Detail why this breaks system integrity (e.g. breaks RFC 9457 telemetry indexing or OWASP safety).
4. [REMEDIATE]: Propose the 100% compliant C# / TypeScript implementation immediately.
</llm_gatekeeper_directive>

<doc_maintenance_rules>
MANDATORY AUTHORING SPECIFICATION (Zero-Drift Living Contract Invariant):
When ADDING, MODIFYING, or EXTENDING endpoints in this system:
1. ARCHETYPE MAPPING: Map the endpoint to one of the 6 Canonical Archetypes in Section 3.
2. MANIFEST REGISTRATION: Register the route inside `<endpoint_manifest>` in Section 7 with its id, method, path, auth, and archetype.
3. ERROR TAXONOMY: Register any new domain business `errorCode` inside Section 5 Taxonomy.
4. LIVING OPENAPI RE-EMIT: Run `dotnet build backend-v2/SmkDocServerV2.slnx` so `docs/openapi.json` is automatically synchronized from code attributes without writing manual Markdown JSON mocks.
</doc_maintenance_rules>

<api_contract_scope>

# 📡 API Contract Specification & Generative Protocol (SSoT)

---

## 1. 📜 Numbered Rule Taxonomy (RFC 2119 Standards)

All endpoints, DTOs, and controllers across `smk-doc-server` MUST adhere strictly to these numbered invariant rules.

### 1.1 Wire & Data Types (`RULE-WIRE`)
* `[RULE-WIRE-001] [MUST]`: All JSON property keys MUST be strictly `camelCase`. `snake_case` or `PascalCase` on the wire are strictly prohibited.
* `[RULE-WIRE-002] [MUST]`: All timestamps MUST be formatted as ISO-8601 UTC strings terminating with `Z` (`YYYY-MM-DDTHH:mm:ss.sssZ`).
* `[RULE-WIRE-003] [MUST NOT]`: Request bodies MUST NOT be top-level JSON arrays (`[...]`). All lists MUST be encapsulated inside a named object property (e.g. `{ "items": [...] }`) to prevent OWASP JSON array vulnerabilities.
* `[RULE-WIRE-004] [MUST]`: Primary and foreign entity identifiers MUST be UUIDv7 strings formatted with standard hyphens (36 characters).
* `[RULE-WIRE-005] [MUST]`: Nullable properties in TypeScript contracts MUST be typed as `T | null`. `undefined` MUST NOT be emitted on the wire.

### 1.2 Response Envelopes & Streaming (`RULE-ENV`)
* `[RULE-ENV-001] [MUST]`: All single-resource JSON reads, state mutations, and RPC actions MUST be encapsulated in `ApiResponse<T>`.
* `[RULE-ENV-002] [MUST]`: All paginated collection queries MUST be encapsulated in `PagedApiResponse<T>` containing `pagination: PaginationMetadata`.
* `[RULE-ENV-003] [MUST]`: Binary media streams (PDF, DOCX, XLSX) MUST bypass JSON envelopes and stream raw bytes directly with explicit `Content-Type` and `Content-Disposition`.
* `[RULE-ENV-004] [MUST NOT]`: NEVER return anonymous objects (e.g. `new { success = true }` or `new { id }`). All responses MUST be strongly-typed Application DTOs.

### 1.3 Error Handling & RFC 9457 (`RULE-ERR`)
* `[RULE-ERR-001] [MUST]`: All client (4xx) and server (5xx) errors MUST return `application/problem+json` conforming strictly to RFC 9457 `ProblemDetails`.
* `[RULE-ERR-002] [MUST NOT]`: NEVER return HTTP 200 OK for error scenarios (Zero GraphQL-style errors).
* `[RULE-ERR-003] [MUST NOT]`: NEVER leak database connection strings, EF Core query snippets, or CLR stack traces inside the `detail` field in production.
* `[RULE-ERR-004] [MUST]`: Every RFC 9457 error payload MUST include a domain-specific `errorCode` registered in Section 5 Taxonomy.
* `[RULE-ERR-005] [MUST]`: Input validation failures (HTTP 400) MUST populate `invalidParams: InvalidParam[]` indicating exact JSON path and reason.

### 1.4 RESTful Routing & HTTP Semantics (`RULE-REST`)
* `[RULE-REST-001] [MUST]`: Route URIs MUST use lowercase kebab-case plural nouns (e.g. `/api/v1/data-connections`, `/api/v1/templates`).
* `[RULE-REST-002] [MUST]`: Resource creation (`POST /api/v1/{resources}`) MUST return `201 Created` with a canonical `Location` header.
* `[RULE-REST-003] [MUST]`: State mutation (`PUT /api/v1/{resources}/{id}`) MUST return `200 OK` with the freshly updated DTO. Empty `204 NoContent` is prohibited when mutated state is meaningful to callers.
* `[RULE-REST-004] [MUST]`: Resource deletion (`DELETE /api/v1/{resources}/{id}`) MUST return `204 NoContent` with an empty response body.
* `[RULE-REST-005] [MUST]`: Route hierarchy depth MUST NOT exceed 2 levels (e.g. `/api/v1/projects/{id}/users/{userId}`). Deeper relations MUST be flattened to root resources with query filters.

### 1.5 Edge Security & Architectural Invariants (`RULE-INVAR`)
* `[RULE-INVAR-001] [MUST]`: Preview endpoints MUST remain 100% in-memory (ephemeral). Writing to PostgreSQL or MinIO during preview is strictly prohibited.
* `[RULE-INVAR-002] [MUST]`: Gotenberg/MinIO binary payloads MUST be streamed directly to HTTP responses (`PipeWriter` / `Stream.CopyToAsync`). Buffering multi-MB documents into memory (`byte[]`) is strictly prohibited.
* `[RULE-INVAR-003] [MUST]`: State-mutating endpoints (`POST`, `PUT`) MUST support the `Idempotency-Key` header with deterministic SHA-256 payload fingerprinting.
* `[RULE-INVAR-004] [MUST]`: High-throughput document generation endpoints MUST be protected by standard IETF rate-limiting headers.

---

## 2. 📦 Universal Wire Envelopes & Schemas

### 2.1 Single Resource Envelope (`ApiResponse<T>`)
```typescript
interface ApiResponse<T> {
  data: T;
}
```
```json
{
  "data": {
    "id": "01926b42-7c3a-7000-8000-123456789abc",
    "name": "Commercial Tax Invoice",
    "slug": "tax-invoice-th",
    "isActive": true,
    "createdAt": "2026-10-10T12:00:00.000Z",
    "updatedAt": null
  }
}
```

### 2.2 Paged Collection Envelope (`PagedApiResponse<T>`)
Default pagination: `?page=1&limit=20` (maximum `limit=100`).
```typescript
interface PaginationMetadata {
  page: number;
  limit: number;
  totalCount: number;
  totalPages: number;
}

interface PagedApiResponse<T> {
  data: T[];
  pagination: PaginationMetadata;
}
```
```json
{
  "data": [
    {
      "id": "01926b42-7c3a-7000-8000-123456789abc",
      "slug": "tax-invoice-th",
      "name": "Commercial Tax Invoice"
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

### 2.3 RFC 9457 Problem Details (`application/problem+json`)
```typescript
interface InvalidParam {
  name: string;    // JSON path in camelCase, e.g. "customer.taxId"
  reason: string;  // Explicit validation reason
}

interface ProblemDetails {
  type: string;                  // e.g. "https://api.sammakorn.co.th/errors/slug-already-exists"
  title: string;                 // HTTP Status phrase, e.g. "Conflict"
  status: number;                // HTTP Status code, e.g. 409
  detail: string;                // Safe, instance-specific message
  instance: string;              // Request URI path
  errorCode: string;             // Registered SCREAMING_SNAKE_CASE domain token
  traceId: string;               // W3C Traceparent ID
  invalidParams?: InvalidParam[]; // Present on 400 VALIDATION_FAILED
}
```

---

## 3. 🏛️ The 6 Canonical Endpoint Archetypes (Golden Patterns)

Every endpoint in `smk-doc-server` MUST strictly map to one of these 6 archetypes. Do NOT invent ad-hoc patterns.

### Archetype 1: Single Resource Query (Read-One)
* **Verb & Route:** `GET /api/v1/{resources}/{id}`
* **Headers:** Standard Auth (`Channel A` or `Channel B`)
* **Status:** `200 OK` → `ApiResponse<TResponseDto>` | `404 Not Found` → `RESOURCE_NOT_FOUND`
```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(ApiResponse<TemplateDetailsDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<Ok<ApiResponse<TemplateDetailsDto>>> GetById(
    Guid id,
    [FromServices] GetTemplateByIdHandler handler,
    CancellationToken cancellationToken)
{
    var query = new GetTemplateByIdQuery(id);
    var result = await handler.ExecuteAsync(query, cancellationToken);
    return TypedResults.Ok(new ApiResponse<TemplateDetailsDto>(result));
}
```

### Archetype 2: Paged Collection Query (Read-Many)
* **Verb & Route:** `GET /api/v1/{resources}?page=1&limit=20&search={q}&sortBy={col}&sortOrder=asc|desc`
* **Status:** `200 OK` → `PagedApiResponse<TItemDto>`
```csharp
[HttpGet]
[ProducesResponseType(typeof(PagedApiResponse<TemplateListItemDto>), StatusCodes.Status200OK)]
public async Task<Ok<PagedApiResponse<TemplateListItemDto>>> List(
    [FromQuery] int page = 1,
    [FromQuery] int limit = 20,
    [FromQuery] string? search = null,
    [FromServices] ListTemplatesHandler handler,
    CancellationToken cancellationToken = default)
{
    var query = new ListTemplatesQuery(page, limit, search);
    var result = await handler.ExecuteAsync(query, cancellationToken);
    return TypedResults.Ok(result);
}
```

### Archetype 3: Resource Creation (Create)
* **Verb & Route:** `POST /api/v1/{resources}`
* **Headers:** `Idempotency-Key` (Optional)
* **Status:** `201 Created` with `Location` header → `ApiResponse<TCreatedDto>`
```csharp
[HttpPost]
[ProducesResponseType(typeof(ApiResponse<TemplateCreatedDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<Created<ApiResponse<TemplateCreatedDto>>> Create(
    [FromBody] CreateTemplateRequest request,
    [FromServices] CreateTemplateHandler handler,
    CancellationToken cancellationToken)
{
    var command = new CreateTemplateCommand(request.Name, request.Slug);
    var result = await handler.ExecuteAsync(command, cancellationToken);
    return TypedResults.Created($"/api/v1/templates/{result.Id}", new ApiResponse<TemplateCreatedDto>(result));
}
```

### Archetype 4: State Mutation / Upsert (Update)
* **Verb & Route:** `PUT /api/v1/{resources}/{id}`
* **Headers:** `Idempotency-Key` (Recommended)
* **Status:** `200 OK` → `ApiResponse<TUpdatedDto>` | `404 Not Found`
```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(typeof(ApiResponse<TemplateUpdatedDto>), StatusCodes.Status200OK)]
public async Task<Ok<ApiResponse<TemplateUpdatedDto>>> Update(
    Guid id,
    [FromBody] UpdateTemplateRequest request,
    [FromServices] UpdateTemplateHandler handler,
    CancellationToken cancellationToken)
{
    var command = new UpdateTemplateCommand(id, request.Name, request.Category);
    var result = await handler.ExecuteAsync(command, cancellationToken);
    return TypedResults.Ok(new ApiResponse<TemplateUpdatedDto>(result));
}
```

### Archetype 5: Action / RPC Execution (Execute)
* **Verb & Route:** `POST /api/v1/{resources}/{action}` or `POST /api/v1/{resources}/{id}/{action}`
* **Headers:** `Idempotency-Key` (Mandatory if state mutating or billable)
* **Status:** `200 OK` → `ApiResponse<TActionResultDto>`
```csharp
[HttpPost("generate/{slug}")]
[ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
public async Task<Ok<ApiResponse<GenerateDocumentResultDto>>> Generate(
    string slug,
    [FromBody] GenerateDocumentRequest request,
    [FromServices] GenerateDocumentHandler handler,
    CancellationToken cancellationToken)
{
    var command = new GenerateDocumentCommand(slug, request.Payload, request.OutputFormat);
    var result = await handler.ExecuteAsync(command, cancellationToken);
    return TypedResults.Ok(new ApiResponse<GenerateDocumentResultDto>(result));
}
```

### Archetype 6: Raw Ephemeral Stream (Binary Media)
* **Verb & Route:** `POST /api/v1/{resources}/preview` or `GET /api/v1/{resources}/download/{id}`
* **Headers:** Content negotiation; zero JSON envelope
* **Status:** `200 OK` → Raw Stream (`Content-Disposition: inline` or `attachment`)
* **Invariants:** 100% ephemeral in RAM, zero DB writes, zero MinIO uploads for preview.
```csharp
[HttpPost("preview")]
[ProducesResponseType(typeof(FileStreamHttpResult), StatusCodes.Status200OK)]
public async Task<FileStreamHttpResult> Preview(
    [FromBody] PreviewDocumentRequest request,
    [FromServices] PreviewDocumentHandler handler,
    CancellationToken cancellationToken)
{
    var command = new PreviewDocumentCommand(request.Payload, request.Html);
    var streamResult = await handler.ExecuteAsync(command, cancellationToken);
    return TypedResults.Stream(streamResult.Stream, "application/pdf", enableRangeProcessing: true);
}
```

---

## 4. 🔤 Unified Field Vocabulary & Full-Stack Typing

To guarantee 100% field consistency across C# backend and TypeScript frontend, strict naming tokens MUST be utilized without synonyms.

### 4.1 Field Naming Vocabulary Standard
| Concept | Mandatory Token Pattern | Compliant Examples | Strictly Prohibited Synonyms |
|---|---|---|---|
| **Primary / Foreign Keys** | `*Id` (UUIDv7 string) | `templateId`, `projectId`, `userId` | `id_template`, `template_uuid`, `tid`, `pk` |
| **Human Semantic Slug** | `slug` (kebab-case alphanumeric) | `slug` | `code`, `key`, `shortName`, `alias` |
| **Timestamps** | `*At` (ISO-8601 UTC) | `createdAt`, `updatedAt`, `expiresAt` | `createdDate`, `createTime`, `timestamp`, `dt` |
| **Booleans** | `is*`, `has*`, `can*`, `skip*` | `isActive`, `hasHeader`, `skipValidation` | `active`, `enabled`, `flag`, `status` |
| **File / Media Metrics** | `*Bytes`, `*Count` | `fileSizeBytes`, `totalCount`, `pageCount` | `size`, `length`, `total`, `num_pages` |
| **Audit Trails** | Fixed canonical names | `version`, `changeNote`, `createdBy` | `revision`, `v`, `comment`, `author`, `user` |

### 4.2 Full-Stack Primitive Type Mapping
| Wire JSON Type | C# Application Domain Type | TypeScript Frontend Type | Notes |
|---|---|---|---|
| `string` | `System.Guid` | `string` | Must be standard UUID hyphenated format |
| `string` | `System.DateTimeOffset` | `string` | Must terminate with `Z` (ISO-8601 UTC) |
| `string` | `enum TEnum` | `"val1" \| "val2"` | Serialized as string enum, never integer |
| `number` | `decimal` | `number` | Currency and precise business amounts |
| `number` | `int` / `long` | `number` | Pagination counts, file sizes |
| `boolean` | `bool` | `boolean` | Strict boolean true/false |
| `object` | `Record<string, unknown>` | `Record<string, unknown>` | Dynamic business payload schema |

---

## 5. 🛑 Algorithmic Error Code Decision Tree & Global Taxonomy

When handling errors, LLMs and developers MUST traverse this deterministic 7-step decision flow to select the HTTP status and domain `errorCode`:

```
[Request Processing]
  │
  ├── 1. Is payload/header malformed, null, or failing FluentValidation?
  │      └── 400 Bad Request ────────> errorCode: "VALIDATION_FAILED"
  │                                    (Populate invalidParams: [{ name, reason }])
  │      └── 400 Bad Request ────────> errorCode: "MISSING_IDEMPOTENCY_KEY"
  │
  ├── 2. Is caller unauthenticated, token expired, or API key invalid?
  │      └── 401 Unauthorized ───────> errorCode: "INVALID_CREDENTIALS" or "TOKEN_EXPIRED"
  │
  ├── 3. Is caller authenticated but lacks project access or role permission?
  │      └── 403 Forbidden ──────────> errorCode: "FORBIDDEN_TENANT_ACCESS" or "INSUFFICIENT_ROLE"
  │
  ├── 4. Does the requested ID or slug exist in caller's tenant project?
  │      └── 404 Not Found ──────────> errorCode: "RESOURCE_NOT_FOUND" or "TEMPLATE_NOT_FOUND"
  │
  ├── 5. Does the action collide with existing state, unique key, or active lock?
  │      └── 409 Conflict ───────────> errorCode: "SLUG_ALREADY_EXISTS" or "RESOURCE_CONFLICT"
  │      └── 409 Conflict ───────────> errorCode: "IDEMPOTENCY_IN_FLIGHT"
  │
  ├── 6. Has RAM draft expired or payload violated compiled JSON Schema / Hash?
  │      └── 410 Gone ───────────────> errorCode: "DRAFT_EXPIRED"
  │      └── 422 Unprocessable ─────> errorCode: "SCHEMA_VALIDATION_FAILED"
  │      └── 422 Unprocessable ─────> errorCode: "IDEMPOTENCY_PAYLOAD_MISMATCH"
  │
  └── 7. Did an external rendering or storage dependency fail?
         └── 500 Internal Error ─────> errorCode: "RENDER_FAILED" (Gotenberg failure)
         └── 502 Bad Gateway ────────> errorCode: "STORAGE_SERVICE_UNAVAILABLE" (MinIO failure)
```

---

## 6. 🔐 Authentication Channels & Edge Protocols

| Channel | Header Format | Consumer | Scope & Hydration |
|---|---|---|---|
| **Channel A** | `X-API-Key: <key>` | M2M (ERP / CRM / Billing) | Validated via SHA-256 hash in DB. Hydrates `ProjectId` and `CallerApp`. |
| **Channel B** | `Authorization: Bearer <jwt>` | Portal UI (Next.js) | HMAC-SHA256 JWT (15-min TTL) with RTR refresh token (7-day TTL). |
| **Channel A Gated** | `X-API-Key: <key>` | Portal Login Gateway | `/api/v1/auth/login` and `/api/v1/auth/refresh` require API key header. |
| **Public** | *None* | Health & Probes | `/health`, `/swagger` |

### Edge Protocols
* **Idempotency Header:** `Idempotency-Key: <uuid>` on state-mutating endpoints (`POST`, `PUT`).
  * Request Fingerprint: deterministic SHA-256 hash of `Method + NormalizedPath + SerializedPayloadJson`.
  * Response Header on replay: `Idempotency-Replayed: true`.
* **Rate Limiting Headers:** `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset`. Status `429 Too Many Requests` (`Retry-After: <sec>`).

---

## 7. 🗺️ Compact Endpoint Manifest (O(1) Route Index)

The authoritative index of existing system endpoints categorized by Archetype:

<endpoint_manifest>
  <!-- 7.1 Document Generation & Rendering -->
  <route id="doc-generate" method="POST" path="/api/v1/documents/generate/{slug}" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="doc-preview" method="POST" path="/api/v1/documents/preview" auth="channel_a,channel_b" archetype="raw-stream" />
  <route id="doc-preview-slug" method="POST" path="/api/v1/documents/preview/{slug}" auth="channel_a,channel_b" archetype="raw-stream" />
  <route id="doc-validate-slug" method="POST" path="/api/v1/documents/validate/{slug}" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="doc-render" method="POST" path="/api/v1/documents/render" auth="channel_a" archetype="raw-stream" />
  <route id="doc-render-stateless" method="POST" path="/api/v1/documents/render/stateless" auth="channel_a" archetype="raw-stream" />
  <route id="doc-download" method="GET" path="/api/v1/documents/download/{logId}" auth="channel_a,channel_b" archetype="raw-stream" />
  <route id="doc-versions-list" method="GET" path="/api/v1/documents/{documentRef}/versions" auth="channel_a,channel_b" archetype="read-many" />
  <route id="doc-version-download" method="GET" path="/api/v1/documents/{documentRef}/versions/{version}/download" auth="channel_a,channel_b" archetype="raw-stream" />

  <!-- 7.2 Template Management & Authoring -->
  <route id="template-list" method="GET" path="/api/v1/templates" auth="channel_a,channel_b" archetype="read-many" />
  <route id="template-get" method="GET" path="/api/v1/templates/{id}" auth="channel_a,channel_b" archetype="read-one" />
  <route id="template-create" method="POST" path="/api/v1/templates" auth="channel_b_admin" archetype="create" />
  <route id="template-update" method="PUT" path="/api/v1/templates/{id}" auth="channel_b_admin" archetype="update" />
  <route id="template-delete" method="DELETE" path="/api/v1/templates/{id}" auth="channel_b_admin" archetype="delete" />
  <route id="template-download" method="GET" path="/api/v1/templates/{id}/download" auth="channel_a,channel_b" archetype="raw-stream" />
  <route id="template-get-html" method="GET" path="/api/v1/templates/{id}/html" auth="channel_a,channel_b" archetype="read-one" />
  <route id="template-save-html" method="PUT" path="/api/v1/templates/{id}/html" auth="channel_a,channel_b" archetype="update" />
  <route id="template-get-studio" method="GET" path="/api/v1/templates/{id}/studio" auth="channel_a,channel_b" archetype="read-one" />
  <route id="template-get-schema" method="GET" path="/api/v1/templates/{id}/schema" auth="channel_a,channel_b" archetype="read-one" />
  <route id="template-validate-html" method="POST" path="/api/v1/templates/{id}/validate" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="template-versions-list" method="GET" path="/api/v1/templates/{id}/versions" auth="channel_a,channel_b" archetype="read-many" />
  <route id="template-rollback" method="POST" path="/api/v1/templates/{id}/rollback/{version}" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="template-datasets-list" method="GET" path="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b" archetype="read-many" />
  <route id="template-datasets-save" method="PUT" path="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b" archetype="update" />
  <route id="template-mappings-list" method="GET" path="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b" archetype="read-many" />
  <route id="template-mappings-save" method="PUT" path="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b" archetype="update" />
  <route id="template-mappings-preview" method="POST" path="/api/v1/templates/{id}/mappings/preview" auth="channel_a,channel_b" archetype="raw-stream" />

  <!-- 7.3 RAM Draft Upload Pipeline & Schemas -->
  <route id="draft-parse" method="POST" path="/api/v1/templates/draft/parse" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="draft-preview" method="POST" path="/api/v1/templates/draft/{draftId}/preview" auth="channel_a,channel_b" archetype="raw-stream" />
  <route id="draft-commit" method="POST" path="/api/v1/templates/draft/{draftId}/commit" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="schema-validate" method="POST" path="/api/v1/schemas/validate" auth="channel_a,public" archetype="action-rpc" />
  <route id="scan-fields-upload" method="POST" path="/api/v1/templates/scan-fields" auth="channel_a,channel_b" archetype="action-rpc" />
  <route id="scan-fields-template" method="GET" path="/api/v1/templates/{id}/scan-fields" auth="channel_a,channel_b" archetype="read-one" />

  <!-- 7.4 Identity & Authentication -->
  <route id="auth-login" method="POST" path="/api/v1/auth/login" auth="channel_a_gated" archetype="action-rpc" />
  <route id="auth-refresh" method="POST" path="/api/v1/auth/refresh" auth="channel_a_gated" archetype="action-rpc" />
  <route id="auth-me" method="GET" path="/api/v1/auth/me" auth="channel_b" archetype="read-one" />

  <!-- 7.5 Projects & Member Administration -->
  <route id="project-list" method="GET" path="/api/v1/management/projects" auth="channel_b" archetype="read-many" />
  <route id="project-get" method="GET" path="/api/v1/management/projects/{projectId}" auth="channel_b" archetype="read-one" />
  <route id="project-create" method="POST" path="/api/v1/management/projects" auth="channel_b_admin" archetype="create" />
  <route id="project-users-list" method="GET" path="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin" archetype="read-many" />
  <route id="project-users-invite" method="POST" path="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin" archetype="create" />
  <route id="project-users-role" method="PUT" path="/api/v1/management/projects/{projectId}/users/{userId}/role" auth="channel_b_admin" archetype="update" />
  <route id="project-users-status" method="PATCH" path="/api/v1/management/projects/{projectId}/users/{userId}/status" auth="channel_b_admin" archetype="update" />
  <route id="project-users-delete" method="DELETE" path="/api/v1/management/projects/{projectId}/users/{userId}" auth="channel_b_admin" archetype="delete" />

  <!-- 7.6 API Key Provisioning -->
  <route id="project-keys-list" method="GET" path="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin" archetype="read-many" />
  <route id="project-keys-create" method="POST" path="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin" archetype="create" />
  <route id="project-keys-revoke" method="DELETE" path="/api/v1/management/projects/{projectId}/api-keys/{keyId}" auth="channel_b_admin" archetype="delete" />

  <!-- 7.7 Integration: Data Connections & Datasets -->
  <route id="conn-list" method="GET" path="/api/v1/data-connections" auth="channel_b" archetype="read-many" />
  <route id="conn-get" method="GET" path="/api/v1/data-connections/{id}" auth="channel_b" archetype="read-one" />
  <route id="conn-create" method="POST" path="/api/v1/data-connections" auth="channel_b" archetype="create" />
  <route id="conn-update" method="PUT" path="/api/v1/data-connections/{id}" auth="channel_b" archetype="update" />
  <route id="conn-delete" method="DELETE" path="/api/v1/data-connections/{id}" auth="channel_b" archetype="delete" />
  <route id="conn-test" method="POST" path="/api/v1/data-connections/test" auth="channel_b" archetype="action-rpc" />
  <route id="dataset-list" method="GET" path="/api/v1/datasets" auth="channel_b" archetype="read-many" />
  <route id="dataset-get" method="GET" path="/api/v1/datasets/{id}" auth="channel_b" archetype="read-one" />
  <route id="dataset-create" method="POST" path="/api/v1/datasets" auth="channel_b" archetype="create" />
  <route id="dataset-update" method="PUT" path="/api/v1/datasets/{id}" auth="channel_b" archetype="update" />
  <route id="dataset-delete" method="DELETE" path="/api/v1/datasets/{id}" auth="channel_b" archetype="delete" />

  <!-- 7.8 Font Assets Management -->
  <route id="fonts-list" method="GET" path="/api/v1/management/settings/fonts" auth="channel_b" archetype="read-many" />
  <route id="fonts-upload" method="POST" path="/api/v1/management/settings/fonts" auth="channel_b" archetype="action-rpc" />

  <!-- 7.9 Audit Logs & Metrics -->
  <route id="logs-list" method="GET" path="/api/v1/logs" auth="channel_a,channel_b" archetype="read-many" />
  <route id="logs-metrics" method="GET" path="/api/v1/logs/metrics" auth="channel_a,channel_b" archetype="read-one" />
</endpoint_manifest>

---

## 8. 🔍 Living SSoT & Machine-Readable Discovery Protocol

To inspect the concrete payload schemas of existing endpoints, developers and LLMs MUST query the machine-readable ground truth rather than consulting static Markdown:

1. **Detailed Schema Lookup:** Inspect `docs/openapi.json` using `grep_search` or open the target controller in `backend-v2/src/SmkDoc.Api/Controllers/`.
2. **Synchronizing Contracts:** Whenever an endpoint is added or modified in C#, execute:
   ```bash
   dotnet build backend-v2/SmkDocServerV2.slnx
   ```
   This automatically updates `docs/openapi.json` with zero manual Markdown synchronization overhead.
3. **Adding New Endpoints:**
   - Map the new requirement to one of the **6 Canonical Archetypes** in Section 3.
   - Register the route in `<endpoint_manifest>` (Section 7).
   - If introducing a new business error, register the `errorCode` in Section 5.
</api_contract_scope>
