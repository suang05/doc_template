# AGENTS.md — SMK Document Server for AI Agents

Guidance and operational reference for AI coding agents (Claude, Cursor, Copilot, Antigravity, etc.) maintaining, extending, or integrating with **smk-doc-server**.

---

## 🎯 System Overview

`smk-doc-server` is an enterprise document generation microservice written in **C# ASP.NET Core (.NET 10)** for **SAMMAKORN**. It accepts structured JSON payloads and merges them into **HTML**, **DOCX** (Word), and **XLSX** (Excel) templates, producing **PDF**, **DOCX**, or **XLSX** output via REST API, replacing legacy SSRS systems.

### Key Architectural Traits (v2 Clean Architecture — SDD v1.3)
1. **Multi-Project Solution (`backend-v2/`):** Strict compilation-enforced layering (`SmkDoc.Domain`, `SmkDoc.Application`, `SmkDoc.Infrastructure`, `SmkDoc.Api`).
2. **Docker V2 Runtime:** All V2 infrastructure (Database, MinIO, API, Portal) MUST be orchestrated using `docker-compose.v2.yml` with the project name `smk-v2` to avoid conflicts with legacy V1 containers.
   * **Run Command:** `docker compose -p smk-v2 -f docker-compose.v2.yml up -d`
3. **Template Engines (Strategy Pattern):**
   - **HTML-First (Chromium):** HTML templates with `<Field>` tags written via Monaco Editor, rendered to pixel-perfect PDF via Gotenberg Chromium with full Thai word-breaking (`Sarabun` font).
   - **OpenXML Engine:** Modular, composable pipeline (`WordTextReplacer`, `WordTableExpander`, `WordMediaInjector`) replacing legacy God-classes.
   - **ClosedXML Engine:** Excel templates formatted to standard A4 printing.
4. **Storage & Delivery:** Dual persistence in **MinIO** object storage (buckets: `outputs` for generated documents, `templates` for template files) and **PostgreSQL** (`smkdoc` database). Pre-signed S3 download URLs (24-hour expiry) are issued for secure delivery.
5. **Document Versioning:** Complete legal audit trail via `document_versions` storing input JSON snapshot, document reference, and output artifacts.
6. **No Dead Weight:** Zero unused libraries (`Google.Cloud.Storage`, `MimeTypesMap`, and `ExcelSnapshotService` have been completely removed).

---

## 🏛️ Core Engineering Principles & Architecture Rules

All developments and extensions MUST strictly adhere to the following software engineering standards:

### 1. Clean Architecture Layering (`backend-v2/`)
- **Domain Layer (`SmkDoc.Domain`):** Pure C# POCO entities, Enums, Value Objects, Domain Exceptions (`SchemaValidationException`, `NotFoundException`, `RenderException`). Zero external dependencies (no EF Core, no OpenXml, no ASP.NET).
- **Application Layer (`SmkDoc.Application`):** Use cases (`GenerateDocumentUseCase`, `ValidatePayloadUseCase`, `PreviewDocumentUseCase`, `TemplateManagementUseCase`, `FieldMappingUseCase`, `DocumentVersionUseCase`), DTOs, Pipeline Interfaces (`IRepository<T>`, `IUnitOfWork`, `IStorageService`, `IPdfRenderer`, `IRenderEngine`, `IJsonSchemaValidationService`, `IExecutionContext`). Depends ONLY on `Domain`.
- **Infrastructure Layer (`SmkDoc.Infrastructure`):** Adapters implementing Application ports:
  - `Persistence/`: EF Core `AppDbContext`, Repositories, Migrations
  - `Storage/`: MinIO S3 adapter
  - `Pdf/`: Gotenberg 8 client (Chromium & LibreOffice)
  - `Engines/`: `HtmlTemplateEngine`, `DocxTemplateEngine`, `ExcelTemplateEngine`
  - `Schema/`: `JsonSchemaValidationService` (Draft-07 validation via `JsonSchema.Net`), `SchemaInferenceService`
- **Presentation Layer (`SmkDoc.Api`):** HTTP Controllers strictly matching SDD v1.3 routes, `ApiKeyMiddleware`, `SchemaValidationExceptionFilter` (RFC 7807 Problem Details), global exception handling, and DI container configuration.

### 2. Standard Software Design Patterns
- **Dependency Inversion (DIP):** Depend on abstractions (`IRepository`, `IStorageService`, `IPdfRenderer`, `IRenderEngine`), never concrete classes. Application Layer must NEVER reference `AppDbContext` or `MinioStorageService` directly.
- **Strategy Pattern (OCP):** Route document rendering through `IRenderEngine` implementations (`HtmlTemplateEngine`, `DocxTemplateEngine`, `ExcelTemplateEngine`) resolved by `RenderEngineType`. NEVER use `if/else` or `switch` on engine types inside orchestrators.
- **Pipeline Pattern (SRP):** Decompose complex Word/Excel processing into single-responsibility steps (`WordTextReplacer`, `WordTableExpander`, `WordMediaInjector`).
- **Options Pattern:** Strongly-typed configuration injection (e.g., `IOptions<MinioSettings>`, `IOptions<GotenbergSettings>`).

### 3. SOLID, KISS, DRY Rules
- **S — Single Responsibility:** 1 Service = 1 Responsibility. Do not bundle storage, versioning, database queries, and regex parsing into a single monster class.
- **O — Open/Closed:** Open for extension (add new `IRenderEngine` or `ITransformFunction`), closed for modification.
- **L — Liskov Substitution:** Subtypes must be fully substitutable for their interface contracts.
- **I — Interface Segregation:** Narrow, purpose-driven interfaces. No monolithic interfaces forcing unused methods.
- **D — Dependency Inversion:** Inward-pointing dependencies. Core has zero framework coupling.
- **KISS:** Avoid over-abstraction. Do not introduce CQRS/MediatR unless complexity genuinely warrants it. Use clean Use-Case services.
- **DRY:** Single Source of Truth for placeholder regex (`PlaceholderHelper.Pattern`) and Thai data transformations (`ThaiDataTransformer`).
- **Security:** Use `IDocxSecurityScanner` in the pipeline to enforce security scanning of uploaded documents.

---

## 🗄️ Database Schema (PostgreSQL — SDD v1.3)

```sql
-- 0. Multi-Tenancy & Auth
CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE user_project_roles (
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    project_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL, -- 'Admin', 'Editor', 'Viewer'
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (user_id, project_id)
);

-- 1. Templates
CREATE TABLE templates (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id         UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    name               VARCHAR(200) NOT NULL,
    slug               VARCHAR(100) UNIQUE NOT NULL,
    category           VARCHAR(50),
    is_active          BOOLEAN DEFAULT true,
    current_version_id UUID,
    created_at         TIMESTAMPTZ DEFAULT NOW(),
    updated_at         TIMESTAMPTZ DEFAULT NOW()
);

-- 2. Field Mappings
CREATE TABLE field_mappings (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id      UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    placeholder      VARCHAR(100) NOT NULL,
    data_source_type VARCHAR(20) DEFAULT 'json',
    source_path      VARCHAR(200) NOT NULL,
    dataset_alias    VARCHAR(50),
    result_path      VARCHAR(300),
    math_expression  VARCHAR(500),
    label            VARCHAR(200) NOT NULL,
    required         BOOLEAN DEFAULT false,
    default_value    TEXT,
    transform        VARCHAR(50),
    sort_order       INTEGER DEFAULT 0,
    UNIQUE(template_id, placeholder)
);

-- 3. Template Versions
CREATE TABLE template_versions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id       UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    version           INTEGER NOT NULL,
    storage_key       VARCHAR(500) NOT NULL,
    status            INTEGER DEFAULT 0,
    file_format       VARCHAR(10),
    data_schema       JSONB,
    sample_payload    JSONB,
    mappings_snapshot TEXT,
    change_note       TEXT,
    commit_message    VARCHAR(500),
    created_by        VARCHAR(100),
    created_at        TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(template_id, version)
);

-- 4. API Keys
CREATE TABLE api_keys (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id   UUID REFERENCES projects(id) ON DELETE CASCADE,
    name         VARCHAR(100) NOT NULL,
    caller_app   VARCHAR(50)  NOT NULL,
    key_hash     VARCHAR(255) NOT NULL,
    is_active    BOOLEAN DEFAULT true,
    last_used_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 5. Generation Logs
CREATE TABLE generation_logs (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id           UUID REFERENCES templates(id) ON DELETE SET NULL,
    template_version_id   UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    api_key_id            UUID REFERENCES api_keys(id) ON DELETE SET NULL,
    caller_app            VARCHAR(50),
    trigger_source        VARCHAR(20),
    input_data            JSONB,
    payload_hash_sha256   VARCHAR(64),
    output_key            VARCHAR(500),
    output_format         VARCHAR(10),
    file_size_bytes       BIGINT,
    page_count            INTEGER,
    duration_ms           INTEGER,
    status                VARCHAR(20) NOT NULL,
    error_msg             TEXT,
    created_at            TIMESTAMPTZ DEFAULT NOW()
);

-- 6. Documents (Legal Audit Trail Base)
CREATE TABLE documents (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_ref  VARCHAR(100) NOT NULL,
    template_id   UUID REFERENCES templates(id) ON DELETE SET NULL,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(document_ref)
);

-- 6.1. Document Versions (Legal Audit Trail Snapshots)
CREATE TABLE document_versions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id         UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    version             INTEGER NOT NULL,
    template_version_id UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    generation_log_id   UUID REFERENCES generation_logs(id) ON DELETE SET NULL,
    change_note         TEXT,
    created_by          VARCHAR(100),
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(document_id, version)
);

-- 7. Data Connections (Datasources V2)
CREATE TABLE data_connections (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         VARCHAR(200) NOT NULL,
    provider     VARCHAR(50) NOT NULL,
    encrypted_connection_string TEXT NOT NULL,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ
);

-- 8. Datasets
CREATE TABLE datasets (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         VARCHAR(200) NOT NULL,
    description  TEXT,
    data_connection_id UUID NOT NULL REFERENCES data_connections(id) ON DELETE CASCADE,
    sql_query    TEXT NOT NULL,
    cache_seconds INTEGER DEFAULT 0,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ
);

-- 9. Template Datasets
CREATE TABLE template_datasets (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id  UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    dataset_id   UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
    alias        VARCHAR(50) NOT NULL,
    sort_order   INTEGER DEFAULT 0,
    UNIQUE(template_id, alias)
);
```

---

## 🔌 API Endpoints & Auth (SDD v1.3 Standards)

All endpoints require authentication, using dual-mode auth depending on the caller context:
* **System / External API callers:** `X-API-Key: <plainTextKey>` mapped to a `ProjectId` and tracked via DB hashes (SHA-256).
* **Portal UI users (Human):** `Authorization: Bearer <jwtToken>`. Stateless JWT signed via `Jwt:Secret`, containing `sub` (UserId), `email`, `ProjectId`, and `role` claims.

### 0. Authentication
* **Login (Portal):** `POST /api/auth/login`
  * Body: `{ "email": "...", "password": "...", "projectId": "uuid" }`
  * Response: `{ "accessToken": "jwt...", "expiresIn": 86400 }`

### 1. Document Operations
* **Generate Document:** `POST /api/documents/generate/:slug`
  * Body: `{ "data": { ... }, "output": "pdf" | "docx" | "xlsx", "skipValidation": false }`
  * Response: `{ "url": "https://...", "expiresAt": "...", "generationId": "uuid" }`
  * Schema Validation: Enforces template's Draft-07 JSON Schema. Returns `400 Bad Request` (RFC 7807 Problem Details) on violation with `{ "errors": [{ "path": "...", "message": "...", "rule": "..." }] }`. Can be bypassed with `"skipValidation": true`.
* **Pre-flight Payload Validation:** `POST /api/documents/validate/:slug`
  * Body: `{ "data": { ... } }`
  * Response (200 / 400): `{ "valid": true | false, "templateSlug": "...", "schemaVersion": 1, "errors": [...] }`
  * Behavior: Validates payload against template Draft-07 JSON schema with zero side-effects (no MinIO upload, no DB audit log).
* **Live Preview:** `POST /api/documents/preview/:slug`
  * Body: `{ "data": { ... }, "html": "optional current editor content" }`
  * Behavior: Streams PDF bytes directly (`application/pdf`) with `Content-Disposition: inline`.
  * **Rule:** NEVER upload to MinIO, NEVER write to `generation_logs`, NEVER increment versions (Zero side-effects).
* **Document Version History:** `GET /api/documents/:ref/versions`
* **Download Historical Version:** `GET /api/documents/:ref/versions/:v/download`
* **Stateless Document Rendering:** `POST /api/documents/render`
  * Body: `multipart/form-data` with `file` and `jsonData`
  * Behavior: Renders PDF byte stream directly from file stream without touching storage/logs.

### 2. Template Operations
* **List Templates:** `GET /api/templates`
* **Create Template:** `POST /api/templates` (multipart/form-data)
* **Get HTML for Monaco Editor:** `GET /api/templates/:id/html`
* **Save HTML from Editor:** `PUT /api/templates/:id/html` (Auto-increments version +1)
* **Update Template Metadata:** `PUT /api/templates/:id`
* **Deactivate / Soft Delete:** `DELETE /api/templates/:id`
* **Get Field Mappings:** `GET /api/templates/:id/mappings`
* **Save Field Mappings:** `PUT /api/templates/:id/mappings`
* **Validate Template:** `POST /api/templates/:id/validate`
* **List Template Versions:** `GET /api/templates/:id/versions`

### 3. Settings & Logs
* **List Generation Logs:** `GET /api/logs?page=1&limit=50&app=sales`
* **List API Keys:** `GET /api/api-keys`
* **Create API Key:** `POST /api/api-keys`
* **Revoke API Key:** `DELETE /api/api-keys/:id`

### 4. Datasources & Datasets
* **List Data Connections:** `GET /api/dataconnections`
* **Create Data Connection:** `POST /api/dataconnections`
* **List Datasets:** `GET /api/datasets`
* **Create Dataset:** `POST /api/datasets`
* **Link Dataset to Template:** `POST /api/templates/:id/datasets`

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
│       └── SmkDoc.Tests/           # Unit tests (198/198 passing)
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

### 6. Consolidated Reusable UI Blocks (17 Atomic Components)
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

