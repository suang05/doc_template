<ai_directive>
CRITICAL IMPLEMENTATION RULES (SSoT):
This document is the authoritative Single Source of Truth (SSoT) for all REST wire contracts, DTO payloads, and HTTP protocols across `smk-doc-server`.
1. WIRE FORMAT: All JSON keys MUST be strictly `camelCase`. Dates MUST be ISO-8601 UTC.
2. ENVELOPES: Wrap single responses in `ApiResponse<T>`, list responses in `PagedApiResponse<T>`. Binary outputs MUST stream raw bytes with `Content-Disposition`.
3. ERRORS: Errors MUST return `application/problem+json` conforming to RFC 9457 with a strongly-typed `errorCode`.
4. XML BOUNDARIES: Each endpoint is strictly enclosed within an `<endpoint id="..." method="..." route="..." auth="...">` tag. AI agents MUST respect these boundaries for targeted surgical updates.
5. STRICT NEGATIVE RULES: NEVER return anonymous objects (`new { }`). NEVER return `{ "success": true }`. NEVER return HTTP 200 on errors. NEVER leak stack traces or SQL in `detail`.
6. ARCHITECTURAL SSoT: System topology diagrams and middleware sequences are centralized in `ARCHITECTURE.md`. Do not duplicate visual diagrams in this wire contract document.
</ai_directive>

<doc_maintenance_rules>
MANDATORY AUTHORING SPECIFICATION (Zero-Drift Invariant):
When ADDING, MODIFYING, or EXTENDING endpoints in this document, you MUST strictly adhere to:
1. XML BOUNDARY ENCAPSULATION:
   Every endpoint MUST be wrapped in an individual `<endpoint id="..." method="..." route="..." auth="...">` block.
2. CANONICAL SKELETON (All slots are MANDATORY inside <endpoint>):
   #### `[HTTP_VERB] /api/v1/[resource]/[sub-path]`
   Short 1-line business description.
   * **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`) | Public
   * **Headers:** Edge headers (e.g. `Idempotency-Key`) or *None*
   * **Route Params:** Named route parameters with type and constraints, or *None*
   * **Query Params:** Query parameters with type, defaults, and constraints, or *None*
   * **Request Body (`application/json`):** TypeScript interface + concrete full JSON payload
   * **Response ([STATUS] [PHRASE]):** Envelope + concrete JSON payload
   * **Errors:** Explicit status code and domain `errorCode` mapping (e.g. `400 (VALIDATION_FAILED)`, `404 (RESOURCE_NOT_FOUND)`)
3. TIERED INVARIANTS POLICY:
   - Global Invariants (RFC 9110, RFC 9457, OWASP) are defined in Section 1.0 and inherited automatically by all endpoints.
   - The `<invariants>` tag MUST be reserved EXCLUSIVELY for architectural exceptions (e.g. stateless previews, ephemeral RAM cache, dual-mode downloads). Do NOT duplicate generic boilerplate.
4. POSITIVE DATA VALUES & ZERO TOLERANCE:
   - ZERO ELLIPSIS: Every property MUST contain realistic, production-like mock values (e.g. `"Host=db.internal;Database=app"`). NEVER write `...` or leave empty mock objects.
   - SINGLE VERB: NEVER group verbs or paths. Each route and method MUST have its own dedicated block.
   - ZERO NOISE: NEVER add generic HTTP 101 tutorials, marketing words, or conversational prose.
5. MANIFEST SYNC: Whenever an endpoint is added, modified, or removed, update the `<endpoint_manifest>` at the top of Section 1.
6. ERROR TAXONOMY: Every new business `errorCode` MUST be registered in Section 3 Taxonomy table.
</doc_maintenance_rules>

<api_contract_scope>

# 📡 API Contract Specification (SSoT)

---

<endpoint_manifest>
  <!-- 4.1 Document Generation & Rendering Gateway -->
  <route id="doc-generate" method="POST" path="/api/v1/documents/generate/{slug}" auth="channel_a" desc="Durable document generation" />
  <route id="doc-preview" method="POST" path="/api/v1/documents/preview" auth="channel_a,channel_b" desc="Ephemeral in-memory preview (ad-hoc HTML)" />
  <route id="doc-preview-slug" method="POST" path="/api/v1/documents/preview/{slug}" auth="channel_a,channel_b" desc="Ephemeral in-memory preview (template slug)" />
  <route id="doc-validate-slug" method="POST" path="/api/v1/documents/validate/{slug}" auth="channel_a,channel_b" desc="Pre-flight JSON schema validation probe" />
  <route id="doc-render" method="POST" path="/api/v1/documents/render" auth="channel_a" desc="Stateless document rendering from uploaded stream" />
  <route id="doc-render-stateless" method="POST" path="/api/v1/documents/render/stateless" auth="channel_a" desc="Stateless rendering alias route" />
  <route id="doc-download" method="GET" path="/api/v1/documents/download/{logId}" auth="channel_a,channel_b" desc="Download generated document" />
  <route id="doc-versions-list" method="GET" path="/api/v1/documents/{documentRef}/versions" auth="channel_a,channel_b" desc="Audit history of generated versions" />
  <route id="doc-version-download" method="GET" path="/api/v1/documents/{documentRef}/versions/{version}/download" auth="channel_a,channel_b" desc="Download specific document version" />

  <!-- 4.2 Template Management & Authoring -->
  <route id="template-list" method="GET" path="/api/v1/templates" auth="channel_a,channel_b" desc="List published templates" />
  <route id="template-get" method="GET" path="/api/v1/templates/{id}" auth="channel_a,channel_b" desc="Get template details" />
  <route id="template-create" method="POST" path="/api/v1/templates" auth="channel_b_admin" desc="Create template" />
  <route id="template-update" method="PUT" path="/api/v1/templates/{id}" auth="channel_b_admin" desc="Update template metadata" />
  <route id="template-delete" method="DELETE" path="/api/v1/templates/{id}" auth="channel_b_admin" desc="Archive template" />
  <route id="template-download" method="GET" path="/api/v1/templates/{id}/download" auth="channel_a,channel_b" desc="Download template source" />
  <route id="template-get-html" method="GET" path="/api/v1/templates/{id}/html" auth="channel_a,channel_b" desc="Stream raw HTML for Monaco Editor" />
  <route id="template-save-html" method="PUT" path="/api/v1/templates/{id}/html" auth="channel_a,channel_b" desc="Save modified HTML from Monaco Editor" />
  <route id="template-get-studio" method="GET" path="/api/v1/templates/{id}/studio" auth="channel_a,channel_b" desc="Studio bundle" />
  <route id="template-get-schema" method="GET" path="/api/v1/templates/{id}/schema" auth="channel_a,channel_b" desc="Template JSON Schema" />
  <route id="template-validate-html" method="POST" path="/api/v1/templates/{id}/validate" auth="channel_a,channel_b" desc="Validate HTML syntax" />
  <route id="template-versions-list" method="GET" path="/api/v1/templates/{id}/versions" auth="channel_a,channel_b" desc="Template version revisions" />
  <route id="template-rollback" method="POST" path="/api/v1/templates/{id}/rollback/{version}" auth="channel_a,channel_b" desc="Rollback template version" />
  <route id="template-datasets-list" method="GET" path="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b" desc="List template dataset links" />
  <route id="template-datasets-save" method="PUT" path="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b" desc="Save template dataset links" />
  <route id="template-mappings-list" method="GET" path="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b" desc="List template field mappings" />
  <route id="template-mappings-save" method="PUT" path="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b" desc="Save template field mappings" />
  <route id="template-mappings-preview" method="POST" path="/api/v1/templates/{id}/mappings/preview" auth="channel_a,channel_b" desc="In-memory rendered PDF preview with mappings" />

  <!-- 4.3 RAM Draft Upload Pipeline & Schemas -->
  <route id="draft-parse" method="POST" path="/api/v1/templates/draft/parse" auth="channel_a,channel_b" desc="Parse draft file in RAM cache" />
  <route id="draft-preview" method="POST" path="/api/v1/templates/draft/{draftId}/preview" auth="channel_a,channel_b" desc="Preview draft PDF from RAM cache" />
  <route id="draft-commit" method="POST" path="/api/v1/templates/draft/{draftId}/commit" auth="channel_a,channel_b" desc="Commit draft to MinIO and DB" />
  <route id="schema-validate" method="POST" path="/api/v1/schemas/validate" auth="channel_a,public" desc="Stateless JSON Schema validation" />
  <route id="scan-fields-upload" method="POST" path="/api/v1/templates/scan-fields" auth="channel_a,channel_b" desc="Scan fields from file stream" />
  <route id="scan-fields-template" method="GET" path="/api/v1/templates/{id}/scan-fields" auth="channel_a,channel_b" desc="Scan fields from saved template" />

  <!-- 4.4 Identity & Authentication -->
  <route id="auth-login" method="POST" path="/api/v1/auth/login" auth="channel_a_gated" desc="Authenticate portal user" />
  <route id="auth-refresh" method="POST" path="/api/v1/auth/refresh" auth="channel_a_gated" desc="Refresh Token Rotation" />
  <route id="auth-me" method="GET" path="/api/v1/auth/me" auth="channel_b" desc="Authenticated user profile" />

  <!-- 4.5 Projects & Member Administration -->
  <route id="project-list" method="GET" path="/api/v1/management/projects" auth="channel_b" desc="List tenant projects" />
  <route id="project-get" method="GET" path="/api/v1/management/projects/{projectId}" auth="channel_b" desc="Get tenant project details" />
  <route id="project-create" method="POST" path="/api/v1/management/projects" auth="channel_b_admin" desc="Create tenant project" />
  <route id="project-users-list" method="GET" path="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin" desc="List project member accounts" />
  <route id="project-users-invite" method="POST" path="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin" desc="Invite member to project" />
  <route id="project-users-role" method="PUT" path="/api/v1/management/projects/{projectId}/users/{userId}/role" auth="channel_b_admin" desc="Update member role" />
  <route id="project-users-status" method="PATCH" path="/api/v1/management/projects/{projectId}/users/{userId}/status" auth="channel_b_admin" desc="Set member active status" />
  <route id="project-users-delete" method="DELETE" path="/api/v1/management/projects/{projectId}/users/{userId}" auth="channel_b_admin" desc="Remove user from project" />

  <!-- 4.6 API Key Provisioning -->
  <route id="project-keys-list" method="GET" path="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin" desc="List project API keys" />
  <route id="project-keys-create" method="POST" path="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin" desc="Issue project API key" />
  <route id="project-keys-revoke" method="DELETE" path="/api/v1/management/projects/{projectId}/api-keys/{keyId}" auth="channel_b_admin" desc="Revoke project API key" />
  <route id="api-keys-list-legacy" method="GET" path="/api/api-keys" auth="channel_b" desc="List API keys (legacy compat)" />
  <route id="api-keys-create-legacy" method="POST" path="/api/api-keys" auth="channel_b" desc="Create API key (legacy compat)" />
  <route id="api-keys-revoke-legacy" method="DELETE" path="/api/api-keys/{id}" auth="channel_b" desc="Revoke API key (legacy compat)" />

  <!-- 4.7 Integration: Data Connections & Datasets -->
  <route id="conn-list" method="GET" path="/api/v1/data-connections" auth="channel_b" desc="List database connections" />
  <route id="conn-get" method="GET" path="/api/v1/data-connections/{id}" auth="channel_b" desc="Get database connection" />
  <route id="conn-create" method="POST" path="/api/v1/data-connections" auth="channel_b" desc="Create database connection" />
  <route id="conn-update" method="PUT" path="/api/v1/data-connections/{id}" auth="channel_b" desc="Update database connection" />
  <route id="conn-delete" method="DELETE" path="/api/v1/data-connections/{id}" auth="channel_b" desc="Delete database connection" />
  <route id="conn-test" method="POST" path="/api/v1/data-connections/test" auth="channel_b" desc="Test database connectivity" />
  <route id="dataset-list" method="GET" path="/api/v1/datasets" auth="channel_b" desc="List SQL extraction datasets" />
  <route id="dataset-get" method="GET" path="/api/v1/datasets/{id}" auth="channel_b" desc="Get SQL extraction dataset" />
  <route id="dataset-create" method="POST" path="/api/v1/datasets" auth="channel_b" desc="Create SQL extraction dataset" />
  <route id="dataset-update" method="PUT" path="/api/v1/datasets/{id}" auth="channel_b" desc="Update SQL extraction dataset" />
  <route id="dataset-delete" method="DELETE" path="/api/v1/datasets/{id}" auth="channel_b" desc="Delete SQL extraction dataset" />

  <!-- 4.8 Font Assets Management -->
  <route id="fonts-list" method="GET" path="/api/v1/management/settings/fonts" auth="channel_b" desc="List custom font filenames" />
  <route id="fonts-upload" method="POST" path="/api/v1/management/settings/fonts" auth="channel_b" desc="Upload TrueType/OpenType font" />

  <!-- 4.9 Audit Logs & Metrics -->
  <route id="logs-list" method="GET" path="/api/v1/logs" auth="channel_a,channel_b" desc="List document generation logs" />
  <route id="logs-metrics" method="GET" path="/api/v1/logs/metrics" auth="channel_a,channel_b" desc="Aggregated generation metrics" />
</endpoint_manifest>

---

## 1. 📦 Global Wire Protocols & Response Envelopes

### 1.0 Global Invariants Baseline (System-Wide RFC Standard)
Every endpoint in this document automatically inherits these universal architectural invariants:
* **RFC 9110 HTTP Verb & Representation Standard:**
  - `GET`: Returns `200 OK` with representation.
  - `POST` (Creation): Returns `201 Created` with `Location` header pointing to created resource.
  - `PUT` / `PATCH` (State Mutation): Returns `200 OK` with freshly updated state wrapped in `ApiResponse<T>`. Never return empty `204 NoContent` when updated state is meaningful.
  - `DELETE`: Returns `204 NoContent` with zero response body.
* **RFC 9457 Error Standard:** All client and server errors return `application/problem+json` conforming to RFC 9457 with a strongly-typed `errorCode`.
* **OWASP JSON Top-Level Array Safety:** All incoming request bodies containing lists MUST wrap items in a top-level record object (e.g. `{ "mappings": [...] }`), never a raw JSON array `[...]`.
* **IETF Idempotency Standard:** State-mutating endpoints (`POST`, `PUT`, `DELETE`) support the `Idempotency-Key` header with `Idempotency-Replayed: true` returned on replay.
* **IETF RateLimit Standard:** Rate-limited endpoints return `RateLimit-Limit`, `RateLimit-Remaining`, and `RateLimit-Reset` headers.

Every JSON API response emitted by `smk-doc-server` conforms to one of the canonical envelopes below.

### 1.1 Single Resource Envelope (`ApiResponse<T>`)
Used for single entity reads, mutations, and status responses (`200 OK`, `201 Created`).
```typescript
interface ApiResponse<T> {
  data: T;
}
```
```json
{
  "data": {
    "id": "01926b42-7c3a-7000-8000-123456789abc",
    "projectId": "01926b40-1111-7000-8000-000000000001",
    "name": "Commercial Tax Invoice",
    "slug": "tax-invoice-th",
    "category": "Finance",
    "isActive": true,
    "createdAt": "2026-10-10T12:00:00.000Z",
    "updatedAt": null
  }
}
```

### 1.2 Paged Collection Envelope (`PagedApiResponse<T>`)
Used for paginated queries. Query params default to `?page=1&limit=20` (capped at `maxLimit=100`).
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
      "name": "Commercial Tax Invoice",
      "slug": "tax-invoice-th",
      "category": "Finance",
      "isActive": true
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

### 1.3 Raw Binary Media Streams (Zero Envelope)
Endpoints returning rendered documents (`application/pdf`, `.docx`, `.xlsx`) bypass JSON wrappers and stream bytes directly:
* **In-Memory Preview:** `Content-Type: application/pdf`, `Content-Disposition: inline`
* **File Download:** `Content-Type: application/pdf`, `Content-Disposition: attachment; filename="{filename}"`

### 1.4 RFC 9457 Problem Details Schema (Error Envelope)
All error responses return `Content-Type: application/problem+json`.
```typescript
interface InvalidParam {
  name: string;    // JSON path in camelCase, e.g. "customer.taxId"
  reason: string;  // Explicit "Expected vs Actual" message
}

interface ProblemDetails {
  type: string;                  // e.g. "https://api.sammakorn.co.th/errors/slug-already-exists"
  title: string;                 // HTTP Status phrase, e.g. "Conflict"
  status: number;                // HTTP Status Code, e.g. 409
  detail: string;                // Specific instance explanation (zero internal leaks)
  instance: string;              // Request path, e.g. "/api/v1/templates"
  errorCode: string;             // SCREAMING_SNAKE_CASE domain token
  traceId: string;               // Distributed W3C trace ID
  invalidParams?: InvalidParam[]; // Validation failures
}
```
```json
{
  "type": "https://api.sammakorn.co.th/errors/slug-already-exists",
  "title": "Conflict",
  "status": 409,
  "detail": "Template with slug 'tax-invoice-th' already exists in Project '01926b40-1111-7000-8000-000000000001'.",
  "instance": "/api/v1/templates",
  "errorCode": "SLUG_ALREADY_EXISTS",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "invalidParams": [
    {
      "name": "slug",
      "reason": "Slug 'tax-invoice-th' is already registered within Project '01926b40-1111-7000-8000-000000000001'."
    }
  ]
}
```

---

## 2. 🔐 Authentication & Edge Headers Matrix

> 📌 **Architectural Reference:** สำหรับภาพไดอะแกรม C4 Topology, Edge Security, และ Middleware Pipeline ดูได้ที่ [`ARCHITECTURE.md#1--enterprise-system-topology--c4-container-architecture`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ARCHITECTURE.md#1--enterprise-system-topology--c4-container-architecture) และ [`ARCHITECTURE.md#4--end-to-end-request-pipeline--middleware-topology`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ARCHITECTURE.md#4--end-to-end-request-pipeline--middleware-topology)

| Channel | Auth Header | Intended Consumer | Scope & Behavior |
|---|---|---|---|
| **Channel A** | `X-API-Key: <key>` | M2M (ERP / CRM / Billing) | Validated via SHA-256 hash in DB. Hydrates `ProjectId` and `CallerApp`. |
| **Channel B** | `Authorization: Bearer <jwt>` | Portal UI (Next.js) | HMAC-SHA256 JWT (15-min TTL) with RTR refresh token (7-day TTL). |
| **Channel A Gated** | `X-API-Key: <key>` | Portal Login Gateway | `/api/v1/auth/login` and `/api/v1/auth/refresh` require API key header. |
| **Public** | *None* | Health & Monitoring | `/health`, `/swagger` |

### Edge Protocols
* **Idempotency Header:** `Idempotency-Key: <uuid>` on state-mutating endpoints (`POST`, `PUT`).
  * Request Fingerprint: deterministic SHA-256 hash of `Method + NormalizedPath + SerializedPayloadJson`.
  * Response Header on replay: `Idempotency-Replayed: true`.
  * Status: `409 Conflict` (`IDEMPOTENCY_IN_FLIGHT`) on concurrent duplicate; `422 Unprocessable Entity` (`IDEMPOTENCY_PAYLOAD_MISMATCH`) on payload tampering.
* **Rate Limiting Headers:** `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset`. Status `429 Too Many Requests` (`Retry-After: <sec>`).

---

## 3. 🛑 Global Domain Error Code Taxonomy

| HTTP Status | Error Code (`errorCode`) | Trigger Cause |
|---|---|---|
| **400** | `VALIDATION_FAILED` | FluentValidation input validation failed; fields listed in `invalidParams`. |
| **400** | `MISSING_IDEMPOTENCY_KEY` | Endpoint requires `Idempotency-Key` header (`Mandatory = true`). |
| **400** | `INVALID_PAYLOAD_STRUCTURE` | Request JSON payload is null, malformed, or missing required root object. |
| **401** | `INVALID_CREDENTIALS` | Incorrect username/password or invalid/revoked API key. |
| **401** | `TOKEN_EXPIRED` | Bearer JWT access token expired. Refresh required via `/api/v1/auth/refresh`. |
| **403** | `FORBIDDEN_TENANT_ACCESS` | Caller attempted cross-tenant access outside its `ProjectId` scope. |
| **403** | `INSUFFICIENT_ROLE` | Caller lacks requisite `SystemRole` (e.g. Member attempting Admin operation). |
| **404** | `RESOURCE_NOT_FOUND` | Target entity does not exist within the caller tenant scope. |
| **404** | `TEMPLATE_NOT_FOUND` | Specified template slug or ID does not exist in the active project. |
| **409** | `SLUG_ALREADY_EXISTS` | Unique constraint collision on template or project slug. |
| **409** | `RESOURCE_CONFLICT` | Target entity state collides with business constraints (e.g. removing last Admin, active references). |
| **409** | `IDEMPOTENCY_IN_FLIGHT` | An identical request is currently executing; lock is active. |
| **410** | `DRAFT_EXPIRED` | RAM template draft has expired beyond its 30-minute TTL and was evicted. |
| **422** | `IDEMPOTENCY_PAYLOAD_MISMATCH` | Reused `Idempotency-Key` with a different request payload body. |
| **422** | `SCHEMA_VALIDATION_FAILED` | Business data payload violates template's compiled Draft-07 JSON Schema. |
| **429** | `RATE_LIMIT_EXCEEDED` | Request quota exhausted; backoff indicated by `Retry-After` header. |
| **500** | `RENDER_FAILED` | Gotenberg Chromium or OpenXml engine crashed or timed out. |
| **502** | `STORAGE_SERVICE_UNAVAILABLE` | MinIO S3 object storage connection failed during upload/download. |

---

## 4. 📡 Exhaustive Endpoint Catalog

### 4.1 Document Operations & Rendering (`/api/v1/documents`)

<endpoint id="doc-generate" method="POST" route="/api/v1/documents/generate/{slug}" auth="channel_a,channel_b">

#### `POST /api/v1/documents/generate/{slug}`
Merges JSON payload into template, stores binary in MinIO, records audit log, and returns pre-signed URL.
* **Auth:** Channel A (`X-API-Key`) or Channel B (`Bearer JWT`)
* **Headers:** `Idempotency-Key: <uuid>` (Optional, 24h cache TTL)
* **Route Param:** `slug: string` (e.g. `"commercial-invoice"`)
* **Request Body (`application/json`):**
```typescript
interface GenerateDocumentRequest {
  payload: Record<string, unknown>; // Business JSON data
  outputFormat?: "pdf" | "docx" | "xlsx"; // Default: "pdf"
  documentRef?: string;             // External reference, e.g. "INV-2026-0001"
  changeNote?: string;              // Optional audit trail note
  skipValidation?: boolean;         // Default: false
}
```
```json
{
  "payload": {
    "invoiceNo": "INV-2026-001",
    "customer": { "name": "Acme Corp", "taxId": "0105558000000" },
    "totalAmount": 50000
  },
  "outputFormat": "pdf",
  "documentRef": "INV-2026-0001",
  "changeNote": "Initial billing",
  "skipValidation": false
}
```
* **Response (200 OK):** `ApiResponse<GenerateDocumentResultDto>`
```json
{
  "data": {
    "generationId": "019275a2-3b0c-7839-a9a8-e4b2d8ff98a1",
    "documentRef": "INV-2026-0001",
    "outputFormat": "pdf",
    "fileSizeBytes": 245100,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "url": "https://storage.sammakorn.co.th/outputs/inv.pdf?token=exp1760000000_sig8f9a2b3c4d5e",
    "expiresAt": "2026-10-11T15:40:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `404 (TEMPLATE_NOT_FOUND)`, `422 (SCHEMA_VALIDATION_FAILED)`

</endpoint>

---

<endpoint id="doc-preview" method="POST" route="/api/v1/documents/preview" auth="channel_a,channel_b">

#### `POST /api/v1/documents/preview`
Ephemeral in-memory preview using ad-hoc HTML and payload. Zero DB writes, zero MinIO uploads.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*

<invariants>
- ARCHITECTURAL INVARIANT (Stateless): 100% in-memory execution. MUST NOT invoke IUnitOfWork.CommitAsync or MinIO client.
- RFC 9110 (Media Stream): Stream Gotenberg bytes directly to response. Avoid buffering multi-MB documents into byte[].
</invariants>

* **Request Body (`application/json`):**
```typescript
interface PreviewDocumentRequest {
  payload: Record<string, unknown>;
  html?: string;
}
```
```json
{
  "payload": {
    "customer": {
      "name": "Acme Corp",
      "taxId": "0105551234567"
    }
  },
  "html": "<html><body><h1>Invoice Preview</h1><p>{{customer.name}}</p></body></html>"
}
```
* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `500 (RENDER_FAILED)`

</endpoint>

---

<endpoint id="doc-preview-slug" method="POST" route="/api/v1/documents/preview/{slug}" auth="channel_a,channel_b">

#### `POST /api/v1/documents/preview/{slug}`
Ephemeral in-memory preview against a published template slug. Merges payload with saved template without persisting.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `slug: string` (e.g. `commercial-invoice`)
* **Query Params:** *None*

<invariants>
- ARCHITECTURAL INVARIANT (Stateless): Ephemeral template render against published slug. Zero DB/MinIO side-effects.
- RFC 9110 (Media Stream): Raw binary stream with 'Content-Disposition: inline'.
</invariants>

* **Request Body (`application/json`):**
```typescript
interface PreviewDocumentBySlugRequest {
  payload: Record<string, unknown>;
}
```
```json
{
  "payload": {
    "customer": {
      "name": "Acme Corp",
      "taxId": "0105551234567"
    },
    "amount": 250000.50
  }
}
```
* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `404 (TEMPLATE_NOT_FOUND)`, `500 (RENDER_FAILED)`

</endpoint>

---

<endpoint id="doc-validate-slug" method="POST" route="/api/v1/documents/validate/{slug}" auth="channel_a,channel_b">

#### `POST /api/v1/documents/validate/{slug}`
Pre-flight JSON payload validation against template's compiled Draft-07 JSON Schema. Pure inspection probe; always returns `200 OK`.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `slug: string`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface ValidateTemplatePayloadRequest {
  customer?: {
    taxId?: string;
  };
}
```
```json
{
  "customer": {
    "taxId": "123"
  }
}
```
* **Response (200 OK):** `ApiResponse<ValidateTemplatePayloadResult>`
```json
{
  "data": {
    "valid": false,
    "templateSlug": "commercial-invoice",
    "version": 1,
    "message": "Validation failed: 1 schema error(s) found.",
    "errors": [
      {
        "field": "/customer/taxId",
        "rule": "minLength",
        "message": "Expected string of length 13."
      }
    ]
  }
}
```
* **Errors:** `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="doc-render" method="POST" route="/api/v1/documents/render" auth="channel_a">

#### `POST /api/v1/documents/render`
Stateless document rendering directly from an uploaded template file stream (.html, .docx, .xlsx) and optional JSON payload.
* **Auth:** Channel A (`X-API-Key`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `file`: Binary template file (`.html`, `.docx`, `.xlsx`) — Required
  * `jsonData`: JSON string payload — Optional
* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `500 (RENDER_FAILED)`

</endpoint>

---

<endpoint id="doc-render-stateless" method="POST" route="/api/v1/documents/render/stateless" auth="channel_a">

#### `POST /api/v1/documents/render/stateless`
Canonical alias route for stateless document rendering from uploaded template stream and JSON payload.
* **Auth:** Channel A (`X-API-Key`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `file`: Binary template file (`.html`, `.docx`, `.xlsx`) — Required
  * `jsonData`: JSON string payload — Optional
* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `500 (RENDER_FAILED)`

</endpoint>

---

<endpoint id="doc-download" method="GET" route="/api/v1/documents/download/{logId}" auth="channel_a,channel_b">

#### `GET /api/v1/documents/download/{logId}`
Downloads document by Generation Log ID.
* **Auth:** Channel A or Channel B
* **Route Param:** `logId: uuid`

<invariants>
- RFC 9110 (Content Negotiation Dual-Mode): If Accept contains application/json, return 200 OK with pre-signed URL payload. Otherwise, return HTTP 307 Temporary Redirect directly to MinIO storage.
</invariants>

* **Content Negotiation:**
  * `Accept: application/json` ➔ **200 OK** returning `ApiResponse<{ url: string; expiresInSeconds: number }>`
  * Default / Browser ➔ **307 Temporary Redirect** directly to pre-signed MinIO URL.
* **Errors:** `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="doc-versions-list" method="GET" route="/api/v1/documents/{documentRef}/versions" auth="channel_a,channel_b">

#### `GET /api/v1/documents/{documentRef}/versions`
Retrieves legal audit history of generated versions for a document reference.
* **Auth:** Channel A or Channel B
* **Route Param:** `documentRef: string`
* **Response (200 OK):** `ApiResponse<DocumentVersionDto[]>`
```json
{
  "data": [
    {
      "id": "019275a2-1111-7000-8000-123456789abc",
      "documentId": "019275a2-0000-7000-8000-123456789abc",
      "documentRef": "INV-2026-0001",
      "version": 1,
      "templateVersionId": "01926b40-2222-7000-8000-000000000002",
      "generationLogId": "019275a2-3b0c-7839-a9a8-e4b2d8ff98a1",
      "changeNote": "Initial generation",
      "createdBy": "crm-service",
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```

</endpoint>

---

<endpoint id="doc-version-download" method="GET" route="/api/v1/documents/{documentRef}/versions/{version}/download" auth="channel_a,channel_b">

#### `GET /api/v1/documents/{documentRef}/versions/{version}/download`
Downloads specific historical version of a document.
* **Auth:** Channel A or Channel B
* **Route Params:** `documentRef: string`, `version: int`
* **Response (200 OK):** Raw Binary Stream (`application/pdf`, `.docx`, `.xlsx`)
  * Header: `Content-Disposition: attachment; filename="{filename}"`

</endpoint>

---

### 4.2 Template Lifecycle & Studio Operations (`/api/v1/templates`)

<endpoint id="template-list" method="GET" route="/api/v1/templates" auth="channel_a,channel_b">

#### `GET /api/v1/templates`
Lists all templates belonging to the scoped project.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** `?projectId={uuid}` (Optional, defaults to caller context)
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateResultDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-7c3a-7000-8000-123456789abc",
      "projectId": "01926b40-1111-7000-8000-000000000001",
      "name": "Tax Invoice",
      "slug": "tax-invoice-th",
      "category": "Finance",
      "isActive": true,
      "currentVersionId": "01926b42-8888-7000-8000-123456789abc",
      "fileFormat": "Html",
      "createdAt": "2026-10-10T12:00:00.000Z",
      "updatedAt": null
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="template-get" method="GET" route="/api/v1/templates/{id}" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}`
Retrieves template metadata by ID.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateResultDto>`
```json
{
  "data": {
    "id": "01926b42-7c3a-7000-8000-123456789abc",
    "projectId": "01926b40-1111-7000-8000-000000000001",
    "name": "Tax Invoice",
    "slug": "tax-invoice-th",
    "category": "Finance",
    "isActive": true,
    "currentVersionId": "01926b42-8888-7000-8000-123456789abc",
    "fileFormat": "Html",
    "createdAt": "2026-10-10T12:00:00.000Z",
    "updatedAt": null
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-create" method="POST" route="/api/v1/templates" auth="channel_b_admin">

#### `POST /api/v1/templates`
Creates a new template with optional initial file upload.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `name: string` (2..100 chars) — Required
  * `slug: string` (pattern: `^[a-z0-9-]+$`) — Required
  * `category?: string` — Optional
  * `file?: binary` (`.html`, `.docx`, `.xlsx`, max 10MB) — Optional
* **Response (201 Created):** `ApiResponse<TemplateResultDto>`
  * Header: `Location: /api/v1/templates/01926b42-7c3a-7000-8000-123456789abc`
```json
{
  "data": {
    "id": "01926b42-7c3a-7000-8000-123456789abc",
    "projectId": "01926b40-1111-7000-8000-000000000001",
    "name": "Tax Invoice",
    "slug": "tax-invoice-th",
    "category": "Finance",
    "isActive": true,
    "currentVersionId": "01926b42-8888-7000-8000-123456789abc",
    "fileFormat": "Html",
    "createdAt": "2026-10-10T12:00:00.000Z",
    "updatedAt": null
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `409 (SLUG_ALREADY_EXISTS)`

</endpoint>

---

<endpoint id="template-update" method="PUT" route="/api/v1/templates/{id}" auth="channel_b_admin">

#### `PUT /api/v1/templates/{id}`
Updates template name and category. Returns updated template resource.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface UpdateTemplateRequest {
  name: string;
  category?: string;
}
```
```json
{
  "name": "Updated Tax Invoice",
  "category": "Billing"
}
```
* **Response (200 OK):** `ApiResponse<TemplateResultDto>`
```json
{
  "data": {
    "id": "01926b42-7c3a-7000-8000-123456789abc",
    "projectId": "01926b40-1111-7000-8000-000000000001",
    "name": "Updated Tax Invoice",
    "slug": "tax-invoice-th",
    "category": "Billing",
    "isActive": true,
    "currentVersionId": "01926b42-8888-7000-8000-123456789abc",
    "fileFormat": "Html",
    "createdAt": "2026-10-10T12:00:00.000Z",
    "updatedAt": "2026-10-11T14:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-delete" method="DELETE" route="/api/v1/templates/{id}" auth="channel_b_admin">

#### `DELETE /api/v1/templates/{id}`
Deactivates (soft deletes) a template.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-download" method="GET" route="/api/v1/templates/{id}/download" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/download`
Downloads the published template source file.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** Raw Binary Stream (`application/octet-stream` or MIME type).
  * Header: `Content-Disposition: attachment; filename="tax-invoice-th.html"`
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-get-html" method="GET" route="/api/v1/templates/{id}/html" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/html`
Streams raw HTML template source markup directly for Monaco Editor (RFC 9110 media stream).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** Raw Text Stream (`Content-Type: text/html; charset=utf-8`)
```html
<!DOCTYPE html><html><head><meta charset="utf-8"><title>Tax Invoice</title></head><body><h1>Tax Invoice</h1><p>{{customer.name}}</p></body></html>
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-save-html" method="PUT" route="/api/v1/templates/{id}/html" auth="channel_a,channel_b">

#### `PUT /api/v1/templates/{id}/html`
Saves modified HTML source markup from the Monaco Editor and creates an incremental version revision.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface SaveHtmlRequest {
  html: string;
  samplePayload?: string;
  changeNote?: string;
}
```
```json
{
  "html": "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Tax Invoice</title></head><body><h1>Tax Invoice</h1><p>{{customer.name}}</p></body></html>",
  "samplePayload": "{\"customer\":{\"name\":\"Acme Corp\"}}",
  "changeNote": "Updated company header"
}
```
* **Response (200 OK):** `ApiResponse<SaveHtmlResponse>`
```json
{
  "data": {
    "newVersion": 3
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-get-studio" method="GET" route="/api/v1/templates/{id}/studio" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/studio`
Retrieves the complete Studio bundle including HTML source, sample JSON payload, inferred JSON schema, and version number.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateStudioDto>`
```json
{
  "data": {
    "html": "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Tax Invoice</title></head><body><h1>Tax Invoice</h1><p>{{customer.name}}</p></body></html>",
    "samplePayload": "{\"customer\":{\"name\":\"Acme Corp\"}}",
    "dataSchema": "{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\",\"properties\":{\"customer\":{\"type\":\"object\"}}}",
    "version": 2
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-get-schema" method="GET" route="/api/v1/templates/{id}/schema" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/schema`
Retrieves compiled JSON Schema Draft-07 for a template.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateSchemaDto>`
```json
{
  "data": {
    "templateId": "01926b42-7c3a-7000-8000-123456789abc",
    "schemaContent": "{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\",\"properties\":{\"invoiceNo\":{\"type\":\"string\"}}}",
    "version": 2
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-validate-html" method="POST" route="/api/v1/templates/{id}/validate" auth="channel_a,channel_b">

#### `POST /api/v1/templates/{id}/validate`
Validates HTML template markup syntax and extracts detected placeholders.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface ValidateHtmlRequest {
  html: string;
}
```
```json
{
  "html": "<html><body><h1>{{invoiceNo}}</h1><p>{{customer.name}}</p></body></html>"
}
```
* **Response (200 OK):** `ApiResponse<TemplateValidationResultDto>`
```json
{
  "data": {
    "isValid": true,
    "placeholders": [
      "invoiceNo",
      "customer.name"
    ],
    "errors": []
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-versions-list" method="GET" route="/api/v1/templates/{id}/versions" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/versions`
Lists all historical version revisions for a template.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateVersionDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-8888-7000-8000-123456789abc",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "versionNumber": 2,
      "changeNote": "Updated footer layout",
      "createdAt": "2026-10-10T14:30:00.000Z"
    },
    {
      "id": "01926b40-2222-7000-8000-000000000002",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "versionNumber": 1,
      "changeNote": "Initial version",
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-rollback" method="POST" route="/api/v1/templates/{id}/rollback/{version}" auth="channel_a,channel_b">

#### `POST /api/v1/templates/{id}/rollback/{version}`
Reverts template source content to a historical version and increments new revision.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `id: uuid`, `version: int`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<RollbackVersionResponse>`
```json
{
  "data": {
    "version": 3
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-datasets-list" method="GET" route="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/datasets`
Lists external SQL datasets currently bound to a template.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<TemplateDatasetDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-aaaa-7000-8000-123456789abc",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "datasetId": "01926b40-1111-7000-8000-000000000001",
      "datasetName": "InvoiceLineItems",
      "alias": "items",
      "sortOrder": 1
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-datasets-save" method="PUT" route="/api/v1/templates/{id}/datasets" auth="channel_a,channel_b">

#### `PUT /api/v1/templates/{id}/datasets`
Atomically replaces all external dataset bindings for a template (wrapped in `{ datasets }` root object).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface SaveTemplateDatasetsRequest {
  datasets: SaveTemplateDatasetItemRequest[];
}

interface SaveTemplateDatasetItemRequest {
  datasetId: string;
  alias: string;
  sortOrder?: number;
}
```
```json
{
  "datasets": [
    {
      "datasetId": "01926b40-1111-7000-8000-000000000001",
      "alias": "items",
      "sortOrder": 1
    }
  ]
}
```
* **Response (200 OK):** `ApiResponse<TemplateDatasetDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-aaaa-7000-8000-123456789abc",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "datasetId": "01926b40-1111-7000-8000-000000000001",
      "datasetName": "InvoiceLineItems",
      "alias": "items",
      "sortOrder": 1
    }
  ]
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-mappings-list" method="GET" route="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/mappings`
Lists all field mappings and Thai transformer configurations for a template.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<FieldMappingDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-cccc-7000-8000-123456789abc",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "placeholder": "totalAmount",
      "sourcePath": "invoice.total",
      "label": "Total Billing Amount",
      "required": true,
      "defaultValue": "0.00",
      "transform": "thai_baht_text",
      "sortOrder": 1,
      "dataSourceType": "json",
      "datasetAlias": null,
      "resultPath": null,
      "mathExpression": null
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-mappings-save" method="PUT" route="/api/v1/templates/{id}/mappings" auth="channel_a,channel_b">

#### `PUT /api/v1/templates/{id}/mappings`
Atomically replaces all field mappings for a template (wrapped in `{ mappings }` root object).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface SaveTemplateMappingsRequest {
  mappings: SaveFieldMappingItemRequest[];
}

interface SaveFieldMappingItemRequest {
  placeholder: string;
  sourcePath: string;
  label: string;
  required?: boolean;
  defaultValue?: string | null;
  transform?: string | null;
  sortOrder?: number;
  dataSourceType?: "json" | "sql";
  datasetAlias?: string | null;
  resultPath?: string | null;
  mathExpression?: string | null;
}
```
```json
{
  "mappings": [
    {
      "placeholder": "totalAmount",
      "sourcePath": "invoice.total",
      "label": "Total Billing Amount",
      "required": true,
      "defaultValue": "0.00",
      "transform": "thai_baht_text",
      "sortOrder": 1,
      "dataSourceType": "json",
      "datasetAlias": null,
      "resultPath": null,
      "mathExpression": null
    }
  ]
}
```
* **Response (200 OK):** `ApiResponse<FieldMappingDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-cccc-7000-8000-123456789abc",
      "templateId": "01926b42-7c3a-7000-8000-123456789abc",
      "placeholder": "totalAmount",
      "sourcePath": "invoice.total",
      "label": "Total Billing Amount",
      "required": true,
      "defaultValue": "0.00",
      "transform": "thai_baht_text",
      "sortOrder": 1,
      "dataSourceType": "json",
      "datasetAlias": null,
      "resultPath": null,
      "mathExpression": null
    }
  ]
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

<endpoint id="template-mappings-preview" method="POST" route="/api/v1/templates/{id}/mappings/preview" auth="channel_a,channel_b">

#### `POST /api/v1/templates/{id}/mappings/preview`
Merges provided sample data with current field mappings and returns in-memory rendered PDF preview stream.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface PreviewMappingRequest {
  sampleData: Record<string, unknown>;
}
```
```json
{
  "sampleData": {
    "invoice": {
      "total": 500000
    }
  }
}
```
* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`, `500 (RENDER_FAILED)`

</endpoint>

---

### 4.3 RAM Draft Upload Pipeline & Schemas (`/api/v1/templates/draft`, `/api/v1/schemas`)

<endpoint id="draft-parse" method="POST" route="/api/v1/templates/draft/parse" auth="channel_a,channel_b">

#### `POST /api/v1/templates/draft/parse`
Uploads template file to RAM cache and extracts detected variable placeholders (Step 1: pure, zero DB/MinIO writes).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `file`: Binary template file (`.html`, `.docx`, `.xlsx`) — Required

<invariants>
- Stateless RAM Pipeline: Ingests file stream directly into memory cache (IMemoryCache). Zero writes to Postgres or MinIO. Generates temporary draftId with strict 30-minute sliding TTL.
</invariants>

* **Response (200 OK):** `ApiResponse<ParseDraftResponse>`
```json
{
  "data": {
    "draftId": "draft_01926b42-7c3a-7000-8000-123456789abc",
    "placeholders": [
      "invoiceNumber",
      "customerName",
      "totalAmount"
    ]
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="draft-preview" method="POST" route="/api/v1/templates/draft/{draftId}/preview" auth="channel_a,channel_b">

#### `POST /api/v1/templates/draft/{draftId}/preview`
Renders an in-memory PDF preview from cached draft and sample payload (Step 2: pure, zero side-effects).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `draftId: string`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface PreviewDraftRequest {
  dataJson?: string;
}
```
```json
{
  "dataJson": "{\"invoiceNumber\":\"INV-2026-001\",\"customerName\":\"Sammakorn PLC\",\"totalAmount\":1500000}"
}
```

<invariants>
- Stateless RAM Preview: Retrieves draft file directly from IMemoryCache and streams rendered PDF to client. Zero DB queries, zero MinIO persistence. Returns 410 Gone / DRAFT_EXPIRED if cache expired.
</invariants>

* **Response (200 OK):** Raw Binary Stream (`application/pdf`)
  * Header: `Content-Disposition: inline`
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `410 (DRAFT_EXPIRED)`, `500 (RENDER_FAILED)`

</endpoint>

---

<endpoint id="draft-commit" method="POST" route="/api/v1/templates/draft/{draftId}/commit" auth="channel_a,channel_b">

#### `POST /api/v1/templates/draft/{draftId}/commit`
Persists verified draft to MinIO and writes database record atomically (Step 3: durable commitment).
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Params:** `draftId: string`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CommitDraftRequest {
  name: string;
  slug: string;
  category: string;
  projectId?: string;
  mappings?: CommitDraftMappingItem[];
}

interface CommitDraftMappingItem {
  placeholder: string;
  sourcePath: string;
  transform?: string;
}
```
```json
{
  "name": "Tax Invoice 2026",
  "slug": "tax-invoice-2026",
  "category": "Finance",
  "projectId": "01926b40-1111-7000-8000-000000000001",
  "mappings": [
    {
      "placeholder": "invoiceNumber",
      "sourcePath": "invoice.no",
      "transform": null
    }
  ]
}
```
* **Response (201 Created):** `ApiResponse<CommitDraftResponse>`
  * Header: `Location: /api/v1/templates/01926b42-7c3a-7000-8000-123456789abc`
```json
{
  "data": {
    "templateId": "01926b42-7c3a-7000-8000-123456789abc"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `409 (SLUG_ALREADY_EXISTS)`, `410 (DRAFT_EXPIRED)`

</endpoint>

---

<endpoint id="schema-validate" method="POST" route="/api/v1/schemas/validate" auth="channel_a,public">

#### `POST /api/v1/schemas/validate`
Stateless JSON Schema Draft-07 engine. Zero DB queries, zero disk I/O.
* **Auth:** Channel A (`X-API-Key`) | Public
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface ValidateJsonSchemaRequest {
  schema: Record<string, unknown>;
  payload: Record<string, unknown>;
}
```
```json
{
  "schema": {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "required": ["amount"],
    "properties": {
      "amount": { "type": "number", "minimum": 0 }
    }
  },
  "payload": { "amount": -50 }
}
```
* **Response (200 OK):** `ApiResponse<SchemaValidationResultDto>`
```json
{
  "data": {
    "isValid": false,
    "errors": ["Value -50 is less than minimum 0."]
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`

</endpoint>

---

<endpoint id="scan-fields-upload" method="POST" route="/api/v1/templates/scan-fields" auth="channel_a,channel_b">

#### `POST /api/v1/templates/scan-fields`
Stateless scanning probe that extracts detected variable placeholders directly from an uploaded file stream without persistence.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `file`: Binary template file (`.html`, `.docx`, `.xlsx`) — Required
* **Response (200 OK):** `ApiResponse<ScanFieldsResponse>`
```json
{
  "data": {
    "placeholders": [
      "invoiceNumber",
      "customerName",
      "billingAddress",
      "totalAmount"
    ]
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="scan-fields-template" method="GET" route="/api/v1/templates/{id}/scan-fields" auth="channel_a,channel_b">

#### `GET /api/v1/templates/{id}/scan-fields`
Extracts and analyzes detected variable placeholders from an existing template stored in MinIO.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<ScanFieldsResponse>`
```json
{
  "data": {
    "placeholders": [
      "invoiceNumber",
      "customerName",
      "billingAddress",
      "totalAmount"
    ]
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (TEMPLATE_NOT_FOUND)`

</endpoint>

---

### 4.4 Identity & Authentication (`/api/v1/auth`)

<endpoint id="auth-login" method="POST" route="/api/v1/auth/login" auth="channel_a_gated">

#### `POST /api/v1/auth/login`
Authenticates portal user and issues access/refresh tokens.
* **Auth:** Channel A Gated (`X-API-Key: <key>`)
* **Headers:** `X-API-Key: <key>`
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface LoginRequest {
  email: string;
  password: string;
}
```
```json
{
  "email": "architect@sammakorn.co.th",
  "password": "Password123!"
}
```
* **Response (200 OK):** `ApiResponse<LoginResultDto>`
```json
{
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMTkyNmI0Mi0xMTExLTcwMDAtODAwMC0xMjM0NTY3ODlhYmMiLCJlbWFpbCI6ImFyY2hpdGVjdEBzYW1tYWtvcm4uY28udGgiLCJyb2xlIjoiU3VwZXJBZG1pbiJ9.dGhpcy1pcy1hLXZhbGlkLXNpZ25hdHVyZS1leGFtcGxl",
    "refreshToken": "7c3a70008000123456789abcdef0123456789abcdef0123456789abcdef01234567",
    "tokenType": "Bearer",
    "expiresIn": 86400,
    "defaultProjectId": "01926b40-1111-7000-8000-000000000001",
    "user": {
      "id": "01926b42-1111-7000-8000-123456789abc",
      "email": "architect@sammakorn.co.th",
      "firstName": "Senior",
      "lastName": "Architect",
      "systemRole": "SuperAdmin"
    },
    "accessibleProjects": [
      {
        "id": "01926b40-1111-7000-8000-000000000001",
        "name": "Finance",
        "role": "Admin"
      }
    ],
    "activeApiKeys": []
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="auth-refresh" method="POST" route="/api/v1/auth/refresh" auth="channel_a_gated">

#### `POST /api/v1/auth/refresh`
Performs Refresh Token Rotation (RTR). Invalidates old token and returns fresh access/refresh token pair.
* **Auth:** Channel A Gated (`X-API-Key: <key>`)
* **Headers:** `X-API-Key: <key>`
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface RefreshTokenRequest {
  refreshToken: string;
}
```
```json
{
  "refreshToken": "7c3a70008000123456789abcdef0123456789abcdef0123456789abcdef01234567"
}
```
* **Response (200 OK):** `ApiResponse<TokenResultDto>`
```json
{
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMTkyNmI0Mi0xMTExLTcwMDAtODAwMC0xMjM0NTY3ODlhYmMiLCJlbWFpbCI6ImFyY2hpdGVjdEBzYW1tYWtvcm4uY28udGgiLCJyb2xlIjoiU3VwZXJBZG1pbiJ9.dGhpcy1pcy1hLXZhbGlkLXNpZ25hdHVyZS1leGFtcGxl",
    "refreshToken": "8d4b8111911123456789bcdef0123456789bcdef0123456789bcdef012345678",
    "expiresIn": 86400,
    "tokenType": "Bearer"
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="auth-me" method="GET" route="/api/v1/auth/me" auth="channel_b">

#### `GET /api/v1/auth/me`
Retrieves claims and profile information of the authenticated user.
* **Auth:** Channel B (`Authorization: Bearer <jwt>`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<CurrentUserProfileResultDto>`
```json
{
  "data": {
    "id": "01926b42-1111-7000-8000-123456789abc",
    "email": "architect@sammakorn.co.th",
    "firstName": "Senior",
    "lastName": "Architect",
    "systemRole": "SuperAdmin",
    "defaultProjectId": "01926b40-1111-7000-8000-000000000001",
    "accessibleProjects": [
      {
        "id": "01926b40-1111-7000-8000-000000000001",
        "name": "Finance & Accounting",
        "role": "Admin"
      }
    ]
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

### 4.5 Projects & Member Administration (`/api/v1/management`)

*(Restricted to Channel B users with `SuperAdmin` or `Admin` role)*

<endpoint id="project-list" method="GET" route="/api/v1/management/projects" auth="channel_b">

#### `GET /api/v1/management/projects`
Lists all tenant projects accessible to the authenticated user with server-side pagination and search.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:**
  * `search: string` (Optional search filter by project name or slug)
  * `page: int` (Default: 1, Min: 1)
  * `pageSize: int` (Default: 20, Min: 1, Max: 100)
* **Request Body:** *None*
* **Response (200 OK):** `PagedApiResponse<ProjectResultDto>`
```json
{
  "data": [
    {
      "id": "01926b40-1111-7000-8000-000000000001",
      "name": "Finance & Accounting",
      "slug": "finance",
      "isActive": true,
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="project-get" method="GET" route="/api/v1/management/projects/{projectId}" auth="channel_b">

#### `GET /api/v1/management/projects/{projectId}`
Retrieves detailed profile of a specific project by its ID.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `projectId: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<ProjectResultDto>`
```json
{
  "data": {
    "id": "01926b40-1111-7000-8000-000000000001",
    "name": "Finance & Accounting",
    "slug": "finance",
    "isActive": true,
    "createdAt": "2026-10-10T12:00:00.000Z"
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="project-create" method="POST" route="/api/v1/management/projects" auth="channel_b_admin">

#### `POST /api/v1/management/projects`
Creates a new tenant project. Restricted to administrators.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CreateProjectRequest {
  name: string;
  slug: string;
}
```
```json
{
  "name": "Marketing & Communications",
  "slug": "marketing"
}
```
* **Response (201 Created):** `ApiResponse<ProjectResultDto>`
  * Header: `Location: /api/v1/management/projects/01926b42-9999-7000-8000-123456789abc`
```json
{
  "data": {
    "id": "01926b42-9999-7000-8000-123456789abc",
    "name": "Marketing & Communications",
    "slug": "marketing",
    "isActive": true,
    "createdAt": "2026-10-11T19:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `409 (RESOURCE_CONFLICT)`

</endpoint>

---

<endpoint id="project-users-list" method="GET" route="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin">

#### `GET /api/v1/management/projects/{projectId}/users`
Lists all project member user accounts and their assigned project roles.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** *None*
* **Route Param:** `projectId: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<UserResultDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-1111-7000-8000-123456789abc",
      "email": "architect@sammakorn.co.th",
      "firstName": "Senior",
      "lastName": "Architect",
      "role": "Admin",
      "isActive": true,
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`

</endpoint>

---

<endpoint id="project-users-invite" method="POST" route="/api/v1/management/projects/{projectId}/users" auth="channel_b_admin">

#### `POST /api/v1/management/projects/{projectId}/users`
Invites a new or existing user into the project membership with an assigned role.
* **Auth:** Channel B (`Bearer JWT`) with Admin role
* **Headers:** *None*
* **Route Param:** `projectId: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface InviteUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role?: string;
}
```
```json
{
  "email": "user@sammakorn.co.th",
  "password": "Password123!",
  "firstName": "Somsak",
  "lastName": "Rattanakul",
  "role": "Viewer"
}
```
* **Response (201 Created):** `ApiResponse<UserResultDto>`
  * Header: `Location: /api/v1/management/projects/{projectId}/users/{id}`
```json
{
  "data": {
    "id": "01926b42-5555-7000-8000-123456789abc",
    "email": "user@sammakorn.co.th",
    "firstName": "Somsak",
    "lastName": "Rattanakul",
    "role": "Viewer",
    "isActive": true,
    "createdAt": "2026-10-11T18:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `409 (RESOURCE_CONFLICT)`

</endpoint>

---

<endpoint id="project-users-role" method="PUT" route="/api/v1/management/projects/{projectId}/users/{userId}/role" auth="channel_b_admin">

#### `PUT /api/v1/management/projects/{projectId}/users/{userId}/role`
Updates a member's role assignment within the project. Returns the updated user membership resource (RFC 9110 representation).
* **Auth:** Channel B (`Bearer JWT`) with Admin role
* **Headers:** *None*
* **Route Params:** `projectId: uuid`, `userId: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface UpdateUserRoleRequest {
  role: "Admin" | "Developer" | "Viewer";
}
```
```json
{
  "role": "Developer"
}
```
* **Response (200 OK):** `ApiResponse<UserResultDto>`
```json
{
  "data": {
    "id": "01926b42-5555-7000-8000-123456789abc",
    "email": "user@sammakorn.co.th",
    "firstName": "Somsak",
    "lastName": "Rattanakul",
    "role": "Developer",
    "isActive": true,
    "createdAt": "2026-10-11T18:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`, `409 (RESOURCE_CONFLICT)`

</endpoint>

---

<endpoint id="project-users-status" method="PATCH" route="/api/v1/management/projects/{projectId}/users/{userId}/status" auth="channel_b_admin">

#### `PATCH /api/v1/management/projects/{projectId}/users/{userId}/status`
Activates or deactivates a member user account. Returns the updated user membership resource (RFC 9110 representation).
* **Auth:** Channel B (`Bearer JWT`) with Admin role
* **Headers:** *None*
* **Route Params:** `projectId: uuid`, `userId: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface SetUserStatusRequest {
  isActive: boolean;
}
```
```json
{
  "isActive": false
}
```
* **Response (200 OK):** `ApiResponse<UserResultDto>`
```json
{
  "data": {
    "id": "01926b42-5555-7000-8000-123456789abc",
    "email": "user@sammakorn.co.th",
    "firstName": "Somsak",
    "lastName": "Rattanakul",
    "role": "Developer",
    "isActive": false,
    "createdAt": "2026-10-11T18:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="project-users-delete" method="DELETE" route="/api/v1/management/projects/{projectId}/users/{userId}" auth="channel_b_admin">

#### `DELETE /api/v1/management/projects/{projectId}/users/{userId}`
Removes a user from the project membership.
* **Auth:** Channel B (`Bearer JWT`) with Admin role
* **Headers:** *None*
* **Route Params:** `projectId: uuid`, `userId: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`, `409 (RESOURCE_CONFLICT)`

</endpoint>

---

### 4.6 API Key Provisioning

<endpoint id="project-keys-list" method="GET" route="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin">

#### `GET /api/v1/management/projects/{projectId}/api-keys`
Lists all active and revoked API keys issued within a project. Admin only.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** *None*
* **Route Params:** `projectId: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<ApiKeyDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-9999-7000-8000-123456789abc",
      "name": "ERP-Integration",
      "callerApp": "SAP-Gateway",
      "maskedKey": "smk_live_a1b2c3d4****9z8y",
      "expiresAt": "2027-10-11T00:00:00.000Z",
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`

</endpoint>

---

<endpoint id="project-keys-create" method="POST" route="/api/v1/management/projects/{projectId}/api-keys" auth="channel_b_admin">

#### `POST /api/v1/management/projects/{projectId}/api-keys`
Creates and issues a new Channel A API key for machine-to-machine integrations. The plain-text key is returned only once upon creation.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Params:** `projectId: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CreateApiKeyRequest {
  name: string;
  callerApp: string;
  expiresAt?: string | null;
}
```
```json
{
  "name": "Billing-Key",
  "callerApp": "BillingEngine",
  "expiresAt": "2027-12-31T23:59:59.000Z"
}
```
* **Response (201 Created):** `ApiResponse<ApiKeyResponseDto>`
  * Header: `Location: /api/v1/management/projects/01926b40-1111-7000-8000-000000000001/api-keys/01926b42-9999-7000-8000-123456789abc`
```json
{
  "data": {
    "id": "01926b42-9999-7000-8000-123456789abc",
    "name": "Billing-Key",
    "callerApp": "BillingEngine",
    "plainTextKey": "smk_live_d7a8f9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6",
    "expiresAt": "2027-12-31T23:59:59.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`

</endpoint>

---

<endpoint id="project-keys-revoke" method="DELETE" route="/api/v1/management/projects/{projectId}/api-keys/{keyId}" auth="channel_b_admin">

#### `DELETE /api/v1/management/projects/{projectId}/api-keys/{keyId}`
Permanently revokes an API key, immediately disabling its ability to invoke Channel A endpoints. Admin only.
* **Auth:** Channel B (`Bearer JWT`) with Admin or SuperAdmin role
* **Headers:** *None*
* **Route Params:** `projectId: uuid`, `keyId: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `401 (INVALID_CREDENTIALS)`, `403 (INSUFFICIENT_ROLE)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="api-keys-list-legacy" method="GET" route="/api/api-keys" auth="channel_b">

#### `GET /api/api-keys`
Legacy Portal UI compatible endpoint for listing project API keys.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:**
  * `projectId: uuid` (Optional if scoped via user token)
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<ApiKeyDto[]>`
```json
{
  "data": [
    {
      "id": "01926b42-9999-7000-8000-123456789abc",
      "name": "ERP-Integration",
      "callerApp": "SAP-Gateway",
      "maskedKey": "smk_live_a1b2c3d4****9z8y",
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="api-keys-create-legacy" method="POST" route="/api/api-keys" auth="channel_b">

#### `POST /api/api-keys`
Legacy Portal UI compatible endpoint for issuing an API key.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CreateLegacyApiKeyRequest {
  name: string;
  callerApp: string;
  projectId?: string;
}
```
```json
{
  "name": "ERP-Integration",
  "callerApp": "SAP-Gateway",
  "projectId": "01926b40-1111-7000-8000-000000000001"
}
```
* **Response (201 Created):** `ApiResponse<ApiKeyResponseDto>`
  * Header: `Location: /api/api-keys/01926b42-9999-7000-8000-123456789abc`
```json
{
  "data": {
    "id": "01926b42-9999-7000-8000-123456789abc",
    "name": "ERP-Integration",
    "callerApp": "SAP-Gateway",
    "plainTextKey": "smk_live_d7a8f9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="api-keys-revoke-legacy" method="DELETE" route="/api/api-keys/{id}" auth="channel_b">

#### `DELETE /api/api-keys/{id}`
Legacy Portal UI compatible endpoint for revoking an API key.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** `id: uuid`
* **Query Params:**
  * `projectId: uuid` (Optional if scoped via user token)
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

### 4.7 Integration: Data Connections & Datasets

<endpoint id="conn-list" method="GET" route="/api/v1/data-connections" auth="channel_b">

#### `GET /api/v1/data-connections`
Lists all external database connection profiles configured for SQL dataset binding.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<DataConnectionDto[]>`
```json
{
  "data": [
    {
      "id": "01926b40-1111-7000-8000-000000000001",
      "name": "AccountingDB",
      "provider": "PostgreSQL",
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="conn-get" method="GET" route="/api/v1/data-connections/{id}" auth="channel_b">

#### `GET /api/v1/data-connections/{id}`
Retrieves a specific database connection profile by ID (credentials masked).
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<DataConnectionDto>`
```json
{
  "data": {
    "id": "01926b40-1111-7000-8000-000000000001",
    "name": "AccountingDB",
    "provider": "PostgreSQL",
    "createdAt": "2026-10-10T12:00:00.000Z"
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="conn-create" method="POST" route="/api/v1/data-connections" auth="channel_b">

#### `POST /api/v1/data-connections`
Registers a new database connection definition with encrypted connection string.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CreateDataConnectionRequest {
  name: string;
  provider: "PostgreSQL" | "SqlServer";
  connectionString: string;
}
```
```json
{
  "name": "AccountingDB",
  "provider": "PostgreSQL",
  "connectionString": "Host=postgres.internal;Port=5432;Database=smk_acc;Username=smk_app;Password=SecretAppPassword123"
}
```
* **Response (201 Created):** `ApiResponse<DataConnectionDto>`
  * Header: `Location: /api/v1/data-connections/01926b40-1111-7000-8000-000000000001`
```json
{
  "data": {
    "id": "01926b40-1111-7000-8000-000000000001",
    "name": "AccountingDB",
    "provider": "PostgreSQL",
    "createdAt": "2026-10-11T19:30:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="conn-update" method="PUT" route="/api/v1/data-connections/{id}" auth="channel_b">

#### `PUT /api/v1/data-connections/{id}`
Updates an existing database connection definition.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface UpdateDataConnectionRequest {
  name: string;
  provider: "PostgreSQL" | "SqlServer";
  connectionString: string;
}
```
```json
{
  "name": "AccountingDB-Primary",
  "provider": "PostgreSQL",
  "connectionString": "Host=postgres-primary.internal;Port=5432;Database=smk_acc;Username=smk_app;Password=UpdatedSecretPassword456"
}
```
* **Response (200 OK):** `ApiResponse<DataConnectionDto>`
```json
{
  "data": {
    "id": "01926b40-1111-7000-8000-000000000001",
    "name": "AccountingDB-Primary",
    "provider": "PostgreSQL",
    "createdAt": "2026-10-10T12:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="conn-delete" method="DELETE" route="/api/v1/data-connections/{id}" auth="channel_b">

#### `DELETE /api/v1/data-connections/{id}`
Deletes a database connection definition. Fails if active datasets reference this connection.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`, `409 (RESOURCE_CONFLICT)`

</endpoint>

---

<endpoint id="conn-test" method="POST" route="/api/v1/data-connections/test" auth="channel_b">

#### `POST /api/v1/data-connections/test`
Tests connectivity and credential validity against target database server without persisting credentials.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface TestDataConnectionRequest {
  provider: "PostgreSQL" | "SqlServer";
  connectionString: string;
}
```
```json
{
  "provider": "PostgreSQL",
  "connectionString": "Host=postgres.internal;Port=5432;Database=smk_acc;Username=smk_app;Password=SecretAppPassword123"
}
```
* **Response (200 OK):** `ApiResponse<TestDataConnectionResponseDto>`
```json
{
  "data": {
    "isSuccess": true,
    "message": "Connection successful."
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="dataset-list" method="GET" route="/api/v1/datasets" auth="channel_b">

#### `GET /api/v1/datasets`
Lists all configured SQL extraction query datasets.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<DatasetDto[]>`
```json
{
  "data": [
    {
      "id": "01926b40-2222-7000-8000-000000000001",
      "name": "InvoiceLineItems",
      "description": "Extracts billable line items for invoice generation",
      "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
      "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
      "cacheSeconds": 300,
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="dataset-get" method="GET" route="/api/v1/datasets/{id}" auth="channel_b">

#### `GET /api/v1/datasets/{id}`
Retrieves a specific SQL dataset definition by ID.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<DatasetDto>`
```json
{
  "data": {
    "id": "01926b40-2222-7000-8000-000000000001",
    "name": "InvoiceLineItems",
    "description": "Extracts billable line items for invoice generation",
    "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
    "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
    "cacheSeconds": 300,
    "createdAt": "2026-10-10T12:00:00.000Z"
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="dataset-create" method="POST" route="/api/v1/datasets" auth="channel_b">

#### `POST /api/v1/datasets`
Creates a new parameterized SQL extraction dataset linked to an existing database connection.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** `Idempotency-Key: <uuid>` (Optional)
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface CreateDatasetRequest {
  name: string;
  description?: string;
  dataConnectionId: string;
  sqlQuery: string;
  cacheSeconds?: number;
}
```
```json
{
  "name": "InvoiceLineItems",
  "description": "Extracts billable line items for invoice generation",
  "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
  "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
  "cacheSeconds": 300
}
```
* **Response (201 Created):** `ApiResponse<DatasetDto>`
  * Header: `Location: /api/v1/datasets/01926b40-2222-7000-8000-000000000001`
```json
{
  "data": {
    "id": "01926b40-2222-7000-8000-000000000001",
    "name": "InvoiceLineItems",
    "description": "Extracts billable line items for invoice generation",
    "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
    "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
    "cacheSeconds": 300,
    "createdAt": "2026-10-11T19:30:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="dataset-update" method="PUT" route="/api/v1/datasets/{id}" auth="channel_b">

#### `PUT /api/v1/datasets/{id}`
Updates an existing SQL dataset query or cache policy.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body (`application/json`):**
```typescript
interface UpdateDatasetRequest {
  name: string;
  description?: string;
  dataConnectionId: string;
  sqlQuery: string;
  cacheSeconds?: number;
}
```
```json
{
  "name": "InvoiceLineItems",
  "description": "Updated query with discount calculation",
  "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
  "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, discount, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
  "cacheSeconds": 600
}
```
* **Response (200 OK):** `ApiResponse<DatasetDto>`
```json
{
  "data": {
    "id": "01926b40-2222-7000-8000-000000000001",
    "name": "InvoiceLineItems",
    "description": "Updated query with discount calculation",
    "dataConnectionId": "01926b40-1111-7000-8000-000000000001",
    "sqlQuery": "SELECT item_id, item_name, quantity, unit_price, discount, total_amount FROM invoice_items WHERE invoice_id = @invoiceId ORDER BY sort_order ASC",
    "cacheSeconds": 600,
    "createdAt": "2026-10-10T12:00:00.000Z"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

<endpoint id="dataset-delete" method="DELETE" route="/api/v1/datasets/{id}" auth="channel_b">

#### `DELETE /api/v1/datasets/{id}`
Deletes a SQL dataset definition.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Param:** `id: uuid`
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (204 NoContent):** Zero response body.
* **Errors:** `401 (INVALID_CREDENTIALS)`, `404 (RESOURCE_NOT_FOUND)`

</endpoint>

---

### 4.8 Font Assets Management (`/api/v1/management/settings/fonts`)

Manages custom TrueType/OpenType Thai fonts loaded into Chromium Gotenberg rendering engine.

<endpoint id="fonts-list" method="GET" route="/api/v1/management/settings/fonts" auth="channel_b">

#### `GET /api/v1/management/settings/fonts`
Lists all uploaded custom font filenames available for document styling.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<string[]>`
```json
{
  "data": [
    "THSarabunNew-Regular.ttf",
    "THSarabunNew-Bold.ttf",
    "AngsanaNew.ttf"
  ]
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="fonts-upload" method="POST" route="/api/v1/management/settings/fonts" auth="channel_b">

#### `POST /api/v1/management/settings/fonts`
Uploads a new TrueType or OpenType font file (.ttf, .otf) into the shared font storage. Max 10 MB.
* **Auth:** Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:** *None*
* **Content-Type:** `multipart/form-data`
* **Form Fields:**
  * `file`: Binary font file (`.ttf`, `.otf`) — Required
* **Response (201 Created):** `ApiResponse<UploadFontResponse>`
```json
{
  "data": {
    "message": "Font uploaded successfully.",
    "fontName": "THSarabunNew-Regular.ttf"
  }
}
```
* **Errors:** `400 (VALIDATION_FAILED)`, `401 (INVALID_CREDENTIALS)`

</endpoint>

---

### 4.9 Audit Logs & Metrics (`/api/v1/logs`)

<endpoint id="logs-list" method="GET" route="/api/v1/logs" auth="channel_a,channel_b">

#### `GET /api/v1/logs`
Lists paginated document generation audit entries.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:**
  * `page: int` (Default: 1, Min: 1)
  * `limit: int` (Default: 50, Min: 1, Max: 100)
  * `app: string` (Optional filter by caller app)
* **Request Body:** *None*
* **Response (200 OK):** `PagedApiResponse<GenerationLogDto>`
```json
{
  "data": [
    {
      "id": "01926b42-7c3a-7000-8000-123456789abc",
      "templateId": "01926b40-1111-7000-8000-000000000001",
      "templateVersionId": "01926b40-2222-7000-8000-000000000002",
      "apiKeyId": "01926b40-3333-7000-8000-000000000003",
      "callerApp": "crm",
      "triggerSource": "Api",
      "outputFormat": "Pdf",
      "fileSizeBytes": 45120,
      "pageCount": 1,
      "durationMs": 350,
      "status": "Success",
      "errorMsg": null,
      "createdAt": "2026-10-10T12:00:00.000Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 50,
    "totalCount": 128,
    "totalPages": 3
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

<endpoint id="logs-metrics" method="GET" route="/api/v1/logs/metrics" auth="channel_a,channel_b">

#### `GET /api/v1/logs/metrics`
Retrieves aggregated throughput and duration telemetry.
* **Auth:** Channel A (`X-API-Key`) | Channel B (`Bearer JWT`)
* **Headers:** *None*
* **Route Params:** *None*
* **Query Params:**
  * `startDate: string` (ISO 8601 UTC timestamp)
  * `endDate: string` (ISO 8601 UTC timestamp)
* **Request Body:** *None*
* **Response (200 OK):** `ApiResponse<LogMetricsDto>`
```json
{
  "data": {
    "totalGenerations": 14200,
    "successfulGenerations": 14185,
    "failedGenerations": 15,
    "averageDurationMs": 284.5,
    "totalFileSizeBytes": 640825344,
    "topCallerApps": [
      { "callerApp": "crm", "generationCount": 8500 },
      { "callerApp": "billing-service", "generationCount": 5700 }
    ]
  }
}
```
* **Errors:** `401 (INVALID_CREDENTIALS)`

</endpoint>

---

</api_contract_scope>
