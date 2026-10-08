# API_CONTRACT.md — REST Endpoints & Authentication Specification

> **Purpose:** Authoritative OpenAPI/REST specifications, Request/Response DTO envelopes, RFC 7807 error codes, and Dual-Channel Auth rules.  
> **Related Docs:** [ARCHITECTURE.md](ARCHITECTURE.md) (Auth channels), [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (DTO rules).

### ⚡ Quick-Lookup: Authentication Channels & Core Routes

| Route Category | Primary Auth Channel | Header Required | Target Controllers |
|---|---|---|---|
| **M2M Document Generation** | Channel A (API Key) | `X-API-Key: <key>` | `DocumentController`, `TemplateController` |
| **Portal Administration** | Channel B (Bearer JWT) | `Authorization: Bearer <jwt>` | `UserManagementController`, `ApiKeyManagementController`, `DataConnectionsController` |
| **Public Endpoints** | Anonymous | None | `POST /api/v1/auth/login`, `GET /health` |

---

## 📦 Standard API Response Envelopes

Every successful JSON API response MUST adhere to one of the following authoritative schemas:

### 1. Single Resource Envelope (`ApiResponse<T>`)
```json
{
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Invoice Template",
    "slug": "invoice"
  }
}
```

### 2. Paged Collection Envelope (`PagedApiResponse<T>`)
```json
{
  "data": [
    { "id": "3fa85f64-...", "name": "Item 1" },
    { "id": "4fb96a75-...", "name": "Item 2" }
  ],
  "total": 42,
  "page": 1,
  "limit": 20
}
```

### 3. Binary & Media Streams (No Envelope)
Endpoints returning documents (`application/pdf`, `application/vnd.openxmlformats-officedocument...`) or raw markup (`text/html`) MUST return the direct binary stream with `Content-Disposition: inline` (preview) or `attachment; filename="..."` (download). **Zero JSON envelope wrapper.**

### 4. Error Responses (RFC 7807 Problem Details)
All error responses emitted by `GlobalExceptionFilter` follow RFC 7807:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Resource Conflict",
  "status": 409,
  "detail": "Slug 'invoice' is already in use.",
  "errorCode": "RESOURCE_CONFLICT"
}
```

---

## 🔌 API Endpoints & Auth (SDD v1.3 Standards)

All endpoints require authentication, using dual-channel auth depending on the caller context:
* **Machine-to-Machine (M2M External Services):** `X-API-Key: <plainTextKey>` mapped directly to a `ProjectId` and tracked via DB hashes (SHA-256). Stateless, no session or user identity required.
* **Portal UI users (Human):** `Authorization: Bearer <jwtToken>`. Stateless JWT signed via `Jwt:Secret`, containing `sub` (UserId), `email`, `given_name`, `family_name`, `SystemRole` (SuperAdmin/Member/Viewer), `role` (Admin/Developer/Viewer), and optional `ProjectId` claims.

### 0. Authentication & Portal Identity
* **Login (Portal):** `POST /api/v1/auth/login` (Alias: `POST /api/auth/login`) (Public)
  * Body: `{ "email": "...", "password": "...", "projectId": "optional-uuid" }` (`projectId` is optional)
  * Response:
    ```json
    {
      "success": true,
      "data": {
        "accessToken": "jwt...",
        "refreshToken": "secure-random-token...",
        "token": "jwt...",
        "tokenType": "Bearer",
        "expiresIn": 86400,
        "user": {
          "id": "uuid",
          "email": "user@sammakorn.co.th",
          "firstName": "...",
          "lastName": "...",
          "systemRole": "SuperAdmin | Member | Viewer"
        },
        "accessibleProjects": [
          { "id": "uuid", "name": "ERP System", "slug": "erp", "role": "Admin" }
        ],
        "defaultProjectId": "uuid"
      }
    }
    ```
* **Refresh Token:** `POST /api/v1/auth/refresh` (Alias: `POST /api/auth/refresh`) (Public)
  * Body: `{ "refreshToken": "..." }`
  * Response:
    ```json
    {
      "success": true,
      "data": {
        "accessToken": "new-jwt...",
        "refreshToken": "new-rotated-refresh-token...",
        "expiresIn": 86400,
        "tokenType": "Bearer"
      }
    }
    ```
  * Security: Implements Token Rotation (RTR). Reuse detection immediately revokes all active sessions for the compromised user.
* **Get Current User:** `GET /api/v1/auth/me` (Alias: `GET /api/auth/me`) (JWT)
  * Response:
    ```json
    {
      "success": true,
      "data": {
        "id": "uuid",
        "email": "user@sammakorn.co.th",
        "firstName": "...",
        "lastName": "...",
        "systemRole": "SuperAdmin | Member | Viewer",
        "accessibleProjects": [
          { "id": "uuid", "name": "ERP System", "slug": "erp", "role": "Admin" }
        ],
        "defaultProjectId": "uuid"
      }
    }
    ```


### 1. Document Operations (X-API-Key)
* **Generate Document:** `POST /api/v1/documents/generate/:slug`
  * Body: `{ "data": { ... }, "output": "pdf" | "docx" | "xlsx", "skipValidation": false }`
  * Response: `{ "url": "https://...", "expiresAt": "...", "generationId": "uuid" }`
  * Schema Validation: Enforces template's Draft-07 JSON Schema. Returns `400 Bad Request` (RFC 7807 Problem Details) on violation with `{ "errors": [{ "path": "...", "message": "...", "rule": "..." }] }`. Can be bypassed with `"skipValidation": true`.
* **Pre-flight Payload Validation:** `POST /api/v1/documents/validate/:slug`
  * Body: `{ "data": { ... } }`
  * Response (200 / 400): `{ "valid": true | false, "templateSlug": "...", "schemaVersion": 1, "errors": [...] }`
  * Behavior: Validates payload against template Draft-07 JSON schema with zero side-effects (no MinIO upload, no DB audit log).
* **Live Preview:** `POST /api/v1/documents/preview/:slug`
  * Body: `{ "data": { ... }, "html": "optional current editor content" }`
  * Behavior: Streams PDF bytes directly (`application/pdf`) with `Content-Disposition: inline`.
  * **Rule:** NEVER upload to MinIO, NEVER write to `generation_logs`, NEVER increment versions (Zero side-effects).
* **Document Version History:** `GET /api/v1/documents/:ref/versions`
* **Download Historical Version:** `GET /api/v1/documents/:ref/versions/:v/download`
* **Stateless Document Rendering:** `POST /api/v1/documents/render`
  * Body: `multipart/form-data` with `file` and `jsonData`
  * Behavior: Renders PDF byte stream directly from file stream without touching storage/logs.
* **Raw HTML to PDF Rendering:** `POST /api/v1/documents/html-to-pdf`
  * Body: `{ "html": "<h1>Hello</h1>", "headerHtml": "", "footerHtml": "" }`
  * Behavior: Renders raw HTML string to PDF bytes with zero side-effects.


### 2. Template Operations (X-API-Key)
* All responses are enveloped via `ApiResponse<T>` with Application `*ResultDto` models.
* Request bodies bind to decoupled Presentation Request contracts in `SmkDoc.Api/Contracts/Authoring/`.
* **List Templates:** `GET /api/v1/templates` (Query: `?projectId=<guid>` optional; fallback to caller's execution context) → `ApiResponse<IEnumerable<TemplateResultDto>>`
* **Get Template by ID:** `GET /api/v1/templates/:id` → `ApiResponse<TemplateResultDto>`
* **Create Template:** `POST /api/v1/templates` (multipart/form-data) → `ApiResponse<object>` (`{ id, slug }`)
* **Get HTML for Monaco Editor:** `GET /api/v1/templates/:id/html`
* **Save HTML from Editor:** `PUT /api/v1/templates/:id/html` (Body: `SaveHtmlRequest`) → `ApiResponse<SaveHtmlResponse>` (Auto-increments version +1)
* **Update Template Metadata:** `PUT /api/v1/templates/:id` (Body: `UpdateTemplateMetadataRequest`) → `ApiResponse<TemplateResultDto>`
* **Deactivate / Soft Delete:** `DELETE /api/v1/templates/:id` → `ApiResponse<object>`
* **Get Field Mappings:** `GET /api/v1/templates/:id/mappings` → `ApiResponse<IEnumerable<FieldMappingDto>>`
* **Save Field Mappings:** `PUT /api/v1/templates/:id/mappings` (Body: `List<SaveFieldMappingItemRequest>`) → `ApiResponse<object>`
* **Preview Field Mappings:** `POST /api/v1/templates/:id/mappings/preview` (Body: `PreviewMappingRequest`) → PDF stream
* **Link / Save Template Datasets:** `PUT /api/v1/templates/:id/datasets` (Body: `List<SaveTemplateDatasetItemRequest>`) → `ApiResponse<object>`
* **Validate Template HTML:** `POST /api/v1/templates/:id/validate` (Body: `ValidateHtmlRequest`) → `ApiResponse<TemplateValidationResultDto>`
* **Validate Template Payload:** `POST /api/v1/templates/:slug/validate` (Alias: `POST /api/v1/templates/:slug/validate-payload`)
  * Body: `{ ... }` (JSON payload to check)
  * Response (`200 OK`):
    ```json
    {
      "data": {
        "valid": true,
        "templateSlug": "invoice-th",
        "version": 1,
        "message": "Payload conforms to template schema.",
        "errors": null
      }
    }
    ```
  * Behavior: Enforces `ProjectId` tenant scoping from `IExecutionContext`. Validates against published `data_schema` with zero side-effects (no DB write, no MinIO upload, no rendering).
* **List Template Versions:** `GET /api/v1/templates/:id/versions`

### 3. Management & Settings (JWT Bearer)
* **List Projects:** `GET /api/v1/management/projects?search=&page=1&pageSize=20` → `PagedApiResponse<ProjectResultDto>` (Server-side paginated & search filtered)
* **Get Project by ID:** GET /api/v1/management/projects/:projectId
* **Create Project:** POST /api/v1/management/projects (Admin only — Returns 201 Created with Location header)
* **List Project Users:** GET /api/v1/management/projects/:projectId/users
* **Invite User:** POST /api/v1/management/projects/:projectId/users (Admin only — Returns 201 Created)
* **Update User Role:** PUT /api/v1/management/projects/:projectId/users/:userId/role (Admin only — Returns 204 NoContent)
* **Remove User:** DELETE /api/v1/management/projects/:projectId/users/:userId (Admin only — Returns 204 NoContent)
* **Set User Status:** PATCH /api/v1/management/projects/:projectId/users/:userId/status (Admin only — Returns 204 NoContent)
* **List API Keys:** GET /api/v1/management/projects/:projectId/api-keys (Admin only)
* **Create API Key:** POST /api/v1/management/projects/:projectId/api-keys (Admin only — Returns 201 Created)
* **Revoke API Key:** DELETE /api/v1/management/projects/:projectId/api-keys/:keyId (Admin only — Returns 204 NoContent)
* **List Fonts:** GET /api/v1/management/settings/fonts
* **Upload Font:** POST /api/v1/management/settings/fonts

### 4. Datasources & Datasets (X-API-Key / JWT)
* All responses are enveloped via `ApiResponse<T>` according to AP-003 standard.
* Request bodies bind to decoupled Presentation Request contracts in `SmkDoc.Api/Contracts/Integration/` before explicit mapping to UseCase commands.
* **List Data Connections:** `GET /api/v1/datasources/connections` → `ApiResponse<IReadOnlyList<DataConnectionResultDto>>`
* **Get Data Connection by ID:** `GET /api/v1/datasources/connections/:id` → `ApiResponse<DataConnectionResultDto>`
* **Create Data Connection:** `POST /api/v1/datasources/connections` (Body: `CreateDataConnectionRequest`) → `ApiResponse<DataConnectionResultDto>` (201 Created)
* **Update Data Connection:** `PUT /api/v1/datasources/connections/:id` (Body: `UpdateDataConnectionRequest`) → `ApiResponse<DataConnectionResultDto>`
* **Delete Data Connection:** `DELETE /api/v1/datasources/connections/:id` → `204 NoContent`
* **Test Data Connection:** `POST /api/v1/datasources/connections/test` (Body: `TestDataConnectionRequest`) → `ApiResponse<TestDataConnectionResultDto>`
* **List Datasets:** `GET /api/v1/datasources/datasets` → `ApiResponse<IReadOnlyList<DatasetResultDto>>`
* **Get Dataset by ID:** `GET /api/v1/datasources/datasets/:id` → `ApiResponse<DatasetResultDto>`
* **Create Dataset:** `POST /api/v1/datasources/datasets` (Body: `CreateDatasetRequest`) → `ApiResponse<DatasetResultDto>` (201 Created)
* **Update Dataset:** `PUT /api/v1/datasources/datasets/:id` (Body: `UpdateDatasetRequest`) → `ApiResponse<DatasetResultDto>`
* **Delete Dataset:** `DELETE /api/v1/datasources/datasets/:id` → `204 NoContent`
* **Link Dataset to Template:** `POST /api/v1/templates/:id/datasets`

### 5. Schema Validation Operations (Public / X-API-Key)
* **Standalone Schema Validation:** `POST /api/v1/schemas/validate` (Alias: `POST /api/schemas/validate`)
  * Request Body: `ValidateSchemaRequest` (enveloping `schema` and `payload`)
  * Response: `ApiResponse<SchemaValidationResultDto>`
  * Body Example:
    ```json
    {
      "schema": {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "type": "object",
        "required": ["doc_no", "amount"],
        "properties": {
          "doc_no": { "type": "string" },
          "amount": { "type": "number", "minimum": 1 }
        }
      },
      "payload": {
        "doc_no": "INV-001",
        "amount": 100
      }
    }
    ```
  * Response (`200 OK`):
    ```json
    {
      "data": {
        "valid": true,
        "message": "Schema validation passed successfully.",
        "errors": null
      }
    }
    ```
  * Response (`200 OK` on validation failure):
    ```json
    {
      "data": {
        "valid": false,
        "message": "Payload does not conform to the provided JSON Schema.",
        "errors": [
          {
            "propertyPath": "/tax_id",
            "message": "Value should be at least 13 characters",
            "schemaRule": "/properties/tax_id/minLength"
          }
        ]
      }
    }
    ```
  * Response (`400 Bad Request` on malformed request body):
    ```json
    {
      "data": {
        "code": "INVALID_REQUEST",
        "message": "Both 'schema' and 'payload' must be provided in the request body."
      }
    }
    ```
  * Behavior: Pure stateless, zero database dependencies, zero MinIO I/O. Uses bounded in-memory compilation caching (`IMemoryCache` with SHA-256 keys, 2h sliding expiration, 5,000 entry limit) to protect against memory growth during live Monaco Editor linting. Suitable for both Monaco Studio live contract validation and external M2M pre-flight validations.

---

## 🛠️ Project Structure

```
smk-doc-server/
├── backend-v2/                 # C# .NET 10 Clean Architecture Solution
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   ├── SmkDocServerV2.slnx
│   ├── src/
│   │   ├── SmkDoc.Domain/          # Pure C# POCOs (Templates, FieldMappings, ApiKeys, Logs, DocVersions)
│   │   ├── SmkDoc.Application/     # Use Cases, Ports (IRepository, IStorageService), DTOs
│   │   ├── SmkDoc.Infrastructure/  # EF Core, MinIO, Gotenberg 8, Engines (Html, Word, Excel)
│   │   └── SmkDoc.Api/             # Controllers, Middlewares, Program.cs
│   └── tests/
│       ├── SmkDoc.Tests/           # Pure in-memory unit tests (100% passing, zero I/O)
│       └── SmkDoc.IntegrationTests/# Integration, benchmarks & generators (100% passing)
│
└── frontend-v2/                # Next.js App Router Clean Architecture Portal
    ├── DESIGN.md               # Design Tokens Specification
    ├── src/
    │   ├── tokens/             # SSoT Design Tokens (Zero hardcoding)
    │   ├── schemas/            # Domain Zod Schemas & Inferred Types
    │   ├── lib/api/            # Infrastructure API Adapters (RESTful gateways)
    │   ├── hooks/              # Application Use-Case Hooks (business logic)
    │   ├── components/
    │   │   ├── ui/             # Atomic Reusable UI Blocks (2-4px radius, compact)
    │   │   ├── layout/         # Shell, Topbar, Sidebar (Lucide icons only)
    │   │   └── features/       # Feature Blocks (Studio, Generator, Mappings, Audit)
    │   └── app/                # App Router (Single Page Tab Switcher via AppShell)
```

---

## 🎨 Frontend Architecture & Design Rules (`frontend-v2/`)

All developments in `frontend-v2/` MUST strictly adhere to the following standards:

### 1. Single Source of Truth (SSoT) for Design Tokens
- All visual values (colors, category accents, radii, typography, spacing) MUST be defined in `src/tokens/index.ts` and mirrored in `DESIGN.md`.
- **STRICT PROHIBITION:** Never hardcode hex colors (e.g. `#10b981`) or arbitrary radii in components. Always use semantic token utilities or token references.
- **NO SAMMAKORN Navy:** Never use dark navy. The base canvas is Modern Minimalist **ขาวอมฟ้า (Ice-White `#f8fbfe` / `#f0f7ff`)**.
- **Category Colorful Accents:** Document types must use designated functional colors:
  - 💜 **Contracts / Legal:** Indigo (`#6366f1`)
  - 💚 **Financial / Invoices:** Emerald (`#10b981`)
  - 💙 **Official Letters:** Sky Blue (`#0284c7`)
  - 🧡 **HR / Personnel:** Amber (`#f59e0b`)
  - 🩵 **Operations / General:** Cyan / Slate (`#0891b2`)

### 2. Geometry, Icons & Micro-Copy Rules
- **Border Radius:** Strictly **2px to 4px** (`--radius-sm: 2px; --radius-md: 4px;` / `rounded-[2px]`, `rounded-[4px]`, `rounded-sm`). No bubble or circular rounded shapes.
- **Iconography:** Use **Lucide icons ONLY** (`lucide-react`). No mixing with other icon sets.
- **Layout & Typography:** High-density, compact dashboard layout (`text-xs`, `text-sm`, `h-8` action buttons). Do not use oversized banners.
- **Concise Micro-Copy:** Rely on symbolic communication (colored status dots, icons, badges). Keep Thai text crisp, short, and natural. Do NOT use redundant English brackets (e.g., use `"เอกสาร"` instead of `"เอกสาร (Documents)"`, `"ผู้ดูแลระบบ"` instead of `"ผู้ดูแล [ADMIN]"`). Avoid robotic or "AI-generated" phrasing.

### 3. Zod-First Validation (Runtime Type-Safety)
- Every API request, response, and form input must be validated via Zod schemas in `src/schemas/`.
- Never use raw `any` types for document payloads or API responses.

### 4. SOLID / KISS / DRY in Frontend
- **SRP:** UI components only render presentation; business logic lives in `src/hooks/`; API calls live in `src/lib/api/`.
- **OCP:** UI blocks (e.g. `Badge`, `CardBlock`) accept category variant props mapped to tokens.
- **DIP:** Hooks depend on API abstractions and Zod schemas, not raw fetch calls inside UI.
- **DRY:** Single source of truth for API routes, tokens, and schemas.
- **KISS:** Keep React state simple, predictable, and clean.

### 5. Navigation & Layout Architecture (SPA Tab Switcher)
The frontend implements a Single Page Application (SPA) architecture for layout navigation. Instead of using native Next.js App Router navigation (`/app/[route]`), the main page (`app/page.tsx`) uses an `<AppShell>` that manages an `activeTab` state and renders views using a `switch` statement.
- **Topbar (`Topbar.tsx`):**
  - Left: Toggle Sidebar (`PanelLeftClose`), Navigation history back/forward (`ChevronLeft`, `ChevronRight`), Home (`Home`).
  - Right: Quick Master API Key input box with mono font, Quick Search (`Search` / `Ctrl+K`), Notifications (`Bell`), Help (`HelpCircle`).
- **Sidebar (`Sidebar.tsx`):**
  - **เอกสาร:** `templates` (แม่แบบทั้งหมด), `generator` (สร้างเอกสาร), `audit` (ประวัติการสร้าง), `logs` (ประวัติการใช้งาน).
  - **จัดการ:** `studio` (Template Editor v2), `upload` (อัปโหลด Template), `mapping` (กำหนดฟิลด์), `version-history` (Version History), `analytics` (Analytics).
  - **ผู้ดูแลระบบ:** `datasources` (Datasources v2), `projects` (API Keys), `users` (จัดการผู้ใช้), `settings` (ตั้งค่าระบบ), `apidocs` (API Docs).
  - **Footer:** User Profile + Popover (เปลี่ยน API Key, ออกจากระบบ).

### 5.1 Card UI Structure
- Card main actions (e.g., Primary Button "สร้างเอกสาร") MUST be aligned to the **bottom-right**.
- Secondary/Context menus (e.g., Dropdown `⋮`) MUST be aligned to the **bottom-left** to prevent dropdown clipping.
- Do NOT use massive Modals for complex forms (e.g., Upload Template); always use split-screen layouts.

### 6. Consolidated Reusable UI Blocks (Atomic Design Components)
Eliminate legacy duplication (`Badge` + `StatusBadge` + `Pill` -> `Badge.tsx`; `Tabs` + `FilterTabs` -> `Tabs.tsx`):
1. **`Button.tsx`**: Semantic actions (`primary` [Sky Blue], `secondary`, `outline`, `ghost`, `danger`, `success`), loading spinner, 2-4px radius. Zero `navy` variant.
2. **`Badge.tsx`**: Consolidated for format tags (`pdf`, `docx`, `xlsx`, `html`), status dots (`success`, `failed`, `pending`), and category accents (`contract`, `financial`, `official`, `hr`, `operations`).
3. **`CardBlock.tsx`**: HyperUI-style card block on Ice-White canvas with 1px border (`border-border`).
4. **`StatBlock.tsx`**: HyperUI metric tile with value, label, trend badge, and colored icon box.
5. **`Input.tsx`**: Form input with Zod validation error integration, icon slot, clear button.
6. **`Select.tsx`**: Dropdown select with Zod validation.
7. **`Modal.tsx`**: Accessible dialog overlay with backdrop and 2-4px radius.
8. **`Table.tsx`**: Data grid with column alignment, sorting indicators, and striped/hover rows.
9. **`Pagination.tsx`**: Page navigator with item counter and page size selector.
10. **`Tabs.tsx`**: Consolidated tabs with optional count badges.
11. **`CodeBlock.tsx`**: Syntax-highlighted code viewer with 1-click copy.
12. **`EmptyState.tsx`**: Symbolic empty indicator with icon, title, and action button.
13. **`Toast.tsx`**: Floating notification alerts.
14. **`Toolbar.tsx`**: Action bar container combining search, category filters, and action buttons.
15. **`Dropdown.tsx`**: Accessible dropdown menu component with customizable triggers.
16. **`PdfPreviewPanel.tsx`**: Integrated PDF previewer for studio and generator.
17. **`Pill.tsx`**: Specialized tag-like pill component.

---

## 🚫 Anti-Patterns & Pitfalls to Avoid

1. **NO Hardcoded Colors/Radii in UI:** Never use raw hex codes or random border-radii in JSX.
2. **NO Leaking Web Framework to Application in Backend:** Never pass `IFormFile` or `IHttpContextAccessor` into Use Cases. Pass pure `Stream` or DTOs.
3. **NO Direct Database Queries in Application:** Application must use `IRepository<T>` or `IUnitOfWork`. Never import `AppDbContext` in `SmkDoc.Application`.
4. **NO God Classes for OpenXML:** Always decompose Word operations into focused classes (`WordTextReplacer`, `WordTableExpander`, `WordMediaInjector`).
5. **DrawingML ID Requirement:** Microsoft Word Desktop strictly requires `pic:cNvPr Id` to be a non-zero positive integer (`Id > 0`). Setting `Id = 0` causes Word to reject the drawingML node.
6. **Pre-signed URL Signature Integrity:** Always use the public endpoint (`http://localhost:9000` or configured external domain) for signing client URLs. Never perform string replacement on signed URLs.
7. **NO Touching V1 (Legacy):** Never modify files in `frontend/` or `backend/`. All active development strictly occurs in `frontend-v2/` and `backend-v2/`.
8. **NO Broken Tests (Frontend/Backend):** If you add new services, hooks, or modify constructors in either the Application/Infrastructure layers (backend) or React components/hooks (frontend), you MUST update their respective test files immediately to ensure 100% passing tests.
9. **Next.js 15 ESLint Flat Config:** The frontend uses ESLint 9 Flat Config (`eslint.config.mjs`). DO NOT create legacy `.eslintrc.json` files. If ESLint blocks Docker builds due to pre-existing code debt, you may downgrade specific noisy rules (e.g., `@typescript-eslint/no-unused-vars`, `@typescript-eslint/no-explicit-any`) to `warn` in `eslint.config.mjs`, but new code MUST be strictly typed and clean.

---

## 🤖 AI Agent Behavior & Workflow Rules

### 0. Core Persona & Domain Expertise
You are acting as a **Senior System Architect, Tech Lead, and Domain Expert in Enterprise Document Generation** for the `smk-doc-server` project. 
Your primary goal is NOT just to reactively fix bugs or generate code blindly. You MUST act as a proactive thinking partner who understands the high stakes of enterprise reporting.

**Domain Context & Architectural Benchmarks:**
You must deeply understand that `smk-doc-server` is a state-of-the-art enterprise template reporting engine. Your mindset and proposed solutions must aim to be **superior, more scalable, more maintainable, and highly performant** compared to legacy or alternative solutions such as:
- **Jasper Reports**
- **Legacy SSRS (SQL Server Reporting Services) + C#**
- **carbone.io**
- **qorstack/qorstack-report**

When interacting with the user, you MUST:
1. **Analyze First:** Before writing code, analyze the request against our Clean Architecture rules. Identify root causes, not just surface-level symptoms. Always consider how changes impact the document rendering pipeline, latency, and memory footprint.
2. **Be Proactive & Guide:** Suggest architectural improvements, flag potential anti-patterns, and warn the user if their request violates any rules in this `AGENTS.md` document (e.g., breaking DIP, using wrong UI tokens).
3. **Design Before Coding:** When asked to build a new feature, always propose a brief architectural design (Which layer? What interfaces? Any side-effects?) and get alignment before generating the actual code.
4. **Maintain Standards:** Ensure 100% adherence to SOLID principles, Zod-first validation in frontend, and our strict UI Geometry rules (2-4px radius, Ice-White canvas).

### General Behavioral Protocols

As an AI agent working on this project, you MUST strictly adhere to the following behavioral protocols:

1. **Reference Docs First:** Before implementing new features, you MUST read and reference any relevant Architecture Standards and Design Rules located in `docs\AI`.
2. **Auto Test & Build (No Docker):** You MUST automatically run build and test commands (e.g., `dotnet build`, `dotnet test`, `npm run test`) to verify your changes. However, DO NOT autonomously run Docker commands (e.g., `docker compose up`). For Docker operations, provide the exact command and instruct the user to run it.
3. **Ask for Architecture Updates:** Upon finishing a task or feature implementation, you MUST ask the user: *"Do you want me to update the architecture documentation/structure to reflect these changes?"*

---

## ✅ AI Agent & Developer Checklist (Pre-Delivery)

Every agent MUST review this checklist mentally before concluding a task and notifying the user:

- [ ] **Docs Reference:** Did I read and apply the standards from `docs\AI`?
- [ ] **V1/V2 Isolation Check:** Did I strictly modify only `-v2` directories, leaving legacy V1 entirely untouched?
- [ ] **Unit Test Sync & Run:** Did I update the unit tests for any modified code, and proactively run the build and test commands to verify them?
- [ ] **UI Component DRY:** Did I use the Design System tokens and shared atomic components (e.g., `Dropdown`, `Badge`) rather than writing raw HTML/CSS?
- [ ] **KISS Layouts:** Are complex forms properly broken out into Split-screen pages instead of massive Modals?
- [ ] **No Auto-Docker:** Did I refrain from running Docker commands autonomously, and instead provided the commands for the user to execute?
- [ ] **Update Prompt:** Did I explicitly ask the user if they want to update the architecture documentation/structure?

