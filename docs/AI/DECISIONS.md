# DECISIONS.md — Architecture Decision Records (ADRs)

> **Purpose:** Authoritative repository of Architecture Decision Records (ADRs) for the SMK Document Server (`backend-v2/` and `frontend-v2/`). Explains the architectural rationale, context, historical evolution, and design decisions.  
> **Related Docs:** [ARCHITECTURE.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ARCHITECTURE.md), [CODING_CONVENTIONS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md), [PATTERNS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md), [ANTI-PATTERNS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ANTI-PATTERNS.md).

<ai_directive>
CRITICAL ATTENTION ROUTING & SELECTIVE READING CONDITIONS:

⛔ WHEN NOT TO READ (DO NOT LOAD IN ROUTINE WORKFLOWS):
- DO NOT read or reference this document during routine day-to-day coding, bug fixing, test writing, or standard endpoint implementation.
- For active coding standards and implementation recipes, ALWAYS read `CODING_CONVENTIONS.md` and `PATTERNS.md`.
- For prohibited anti-patterns, ALWAYS read `ANTI-PATTERNS.md`.

✅ WHEN TO READ (ONLY READ UNDER THESE 3 CONDITIONS):
1. ARCHITECTURAL CHANGES: When tasked with modifying foundational architecture, introducing new 3rd-party libraries, or altering inter-layer communication boundaries.
2. REFACTORING EVALUATION: Before refactoring or replacing an existing subsystem, consult this file to verify Chesterton's Fence ("Why was this built this way and what alternatives were considered?").
3. HISTORICAL RATIONALE: When explicitly asked by the user why a specific technology or design pattern was chosen over an alternative.
</ai_directive>

<decisions_scope>

---

## ⚡ Quick-Lookup: ADR Master Matrix (8 Architectural Domains)

| Domain | Tag | Legacy Alias | Date | Title | Status |
|---|---|:---:|:---:|---|:---:|
| 🔐 **Security** | [`ADR-SEC-01`](#adr-sec-01-initial-x-api-key-authentication) | `ADR-001` | September 2026 | Initial `X-API-Key` Authentication | Partially Superseded by `SEC-02` & `SEC-03` |
| 🔐 **Security** | [`ADR-SEC-02`](#adr-sec-02-hybrid-rbac--dual-channel-authentication-model) | `ADR-011` | September 2026 | Hybrid RBAC & Dual-Channel Authentication Model (M2M + Portal) | **Accepted (Active)** |
| 🔐 **Security** | [`ADR-SEC-03`](#adr-sec-03-scoped-api-keys--m2m-first-zero-public-surface-security) | `ADR-025` | October 2026 | Scoped API Keys (`ReadOnly` / `ReadWrite`) & Zero Public Surface | **Accepted (Active)** |
| 🔐 **Security** | [`ADR-SEC-04`](#adr-sec-04-nextjs-server-side-api-proxy--cookie-to-bearer-token-isolation) | `ADR-027` | October 2026 | Next.js Server-Side API Proxy & Cookie-to-Bearer Token Translation | **Accepted (Active)** |
| ⚙️ **Engines** | [`ADR-ENG-01`](#adr-eng-01-gotenberg-8-chromium-microservice-instead-of-embedded-libreoffice) | `ADR-002` | September 2026 | Gotenberg 8 Chromium Microservice instead of Embedded LibreOffice | **Accepted (Active)** |
| ⚙️ **Engines** | [`ADR-ENG-02`](#adr-eng-02-compiled-handlebars-template-caching--smart-excel-pagesetup) | `ADR-013` | September 2026 | Compiled Handlebars Caching & Smart Excel PageSetup Preservation | **Accepted (Active)** |
| ⚙️ **Engines** | [`ADR-ENG-03`](#adr-eng-03-native-thai-typography-compliance--sarabun-font-sandboxing) | `ADR-029` | October 2026 | Native Thai Typography Compliance & Sarabun Font Sandboxing | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-01`](#adr-dom-01-rich-domain-model-instead-of-anemic-domain-model) | `ADR-003` | September 2026 | Rich Domain Model instead of Anemic Domain Model | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-02`](#adr-dom-02-smart-enums-instead-of-c-enums--magic-strings) | `ADR-004` | September 2026 | Smart Enums instead of C# Enums + Magic Strings | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-03`](#adr-dom-03-dedicated-value-objects-for-primitives-with-validation) | `ADR-005` | September 2026 | Dedicated Value Objects for Primitives with Validation Rules | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-04`](#adr-dom-04-high-performance-domain-optimization--sequential-uuidv7) | `ADR-010` | September 2026 | High-Performance Domain Optimization, Zero-Allocations & UUIDv7 | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-05`](#adr-dom-05-domain-layer-integrity--fail-fast-invariants--aggregates) | `ADR-021` | October 2026 | Domain Layer Integrity — Fail-Fast Invariants & Aggregate Boundaries | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-06`](#adr-dom-06-comprehensive-ddd-aggregates-sealed-entities--value-converters) | `ADR-022` | October 2026 | Comprehensive DDD Aggregates, Sealed Entities & Value Converters | **Accepted (Active)** |
| 💎 **Domain** | [`ADR-DOM-07`](#adr-dom-07-strict-pure-ddd-standards--single-canonical-factory) | `ADR-023` | October 2026 | Strict Pure DDD Standards — Single Canonical Factory & No Test Backdoors | **Accepted (Active)** |
| ⚡ **Application** | [`ADR-APP-01`](#adr-app-01-application-dto-boundary-never-leak-domain-entities) | `ADR-006` | September 2026 | Application DTO Boundary (Never Leak Domain Entities) | **Accepted (Active)** |
| ⚡ **Application** | [`ADR-APP-02`](#adr-app-02-clean-architecture-v2-dto--command-restructuring) | `ADR-014` | September 2026 | Clean Architecture v2 DTO & Command Restructuring (Feature Slices) | **Accepted (Active)** |
| ⚡ **Application** | [`ADR-APP-03`](#adr-app-03-c-12-primary-constructors--sealed-use-cases-standard) | `ADR-017` | September 2026 | C# 12 Primary Constructors & Sealed Use Cases Standard | **Accepted (Active)** |
| ⚡ **Application** | [`ADR-APP-04`](#adr-app-04-phase-out-and-complete-removal-of-commonmodels) | `ADR-020` | September 2026 | Phase-Out and Complete Removal of `Common/Models/` (SSoT DTOs) | **Accepted (Active)** |
| ⚡ **Application** | [`ADR-APP-05`](#adr-app-05-application-query-service-iuserworkspacequeryservice) | `ADR-026` | October 2026 | Application Query Service (`IUserWorkspaceQueryService`) | **Accepted (Active)** |
| 🛡️ **Validation** | [`ADR-VAL-01`](#adr-val-01-dual-engine-automatic-validation-pipeline) | `ADR-015` | September 2026 | Dual-Engine Automatic Validation Pipeline (FluentValidation + JsonSchema.Net) | **Accepted (Active)** |
| 🛡️ **Validation** | [`ADR-VAL-02`](#adr-val-02-schema-infrastructure-clean-code--performance-optimization) | `ADR-016` | September 2026 | Schema Infrastructure Clean Code & Performance Optimization | **Accepted (Active)** |
| 🛡️ **Validation** | [`ADR-VAL-03`](#adr-val-03-standalone-scalable-schema-validator--bounded-caching) | `ADR-018` | September 2026 | Standalone Scalable Schema Validator & Bounded Caching | **Accepted (Active)** |
| 🛡️ **Validation** | [`ADR-VAL-04`](#adr-val-04-template-payload-pre-flight-validation-with-multi-tenant-scoping) | `ADR-019` | September 2026 | Template Payload Pre-Flight Validation with Multi-Tenant Scoping | **Accepted (Active)** |
| 🚨 **Resilience** | [`ADR-RES-01`](#adr-res-01-globalexceptionfilter-instead-of-schemavalidationexceptionfilter) | `ADR-007` | September 2026 | GlobalExceptionFilter instead of SchemaValidationExceptionFilter | Superseded by `RES-04` |
| 🚨 **Resilience** | [`ADR-RES-02`](#adr-res-02-rate-limiting-with-per-api-key-partition) | `ADR-008` | September 2026 | Rate Limiting with Per-API-Key Partition | **Accepted (Active)** |
| 🚨 **Resilience** | [`ADR-RES-03`](#adr-res-03-domainexception-hierarchy--machine-readable-error-codes) | `ADR-009` | September 2026 | DomainException Hierarchy & Machine-Readable Error Codes | **Accepted (Active)** |
| 🚨 **Resilience** | [`ADR-RES-04`](#adr-res-04-unified-rfc-9457-iexceptionhandler--idempotency-pipeline) | `ADR-030` | October 2026 | Unified RFC 9457 `IExceptionHandler` & Idempotency Pipeline | **Accepted (Active)** |
| 🌐 **Frontend** | [`ADR-UI-01`](#adr-ui-01-nextjs-15-app-router-layout--ice-white-design-token-ssot) | `ADR-027` | October 2026 | Next.js 15 App Router Layout & Ice-White Design Token SSoT | **Accepted (Active)** |
| 🌐 **Frontend** | [`ADR-UI-02`](#adr-ui-02-monaco-studio-v2-debounced-live-preview--in-flight-abort-pipeline) | `ADR-028` | October 2026 | Monaco Studio v2 Debounced Live Preview & In-Flight Abort Pipeline | **Accepted (Active)** |
| 🧪 **Testing** | [`ADR-TST-01`](#adr-tst-01-smkdoctests-clean-architecture-mirroring--modernization) | `ADR-012` | September 2026 | SmkDoc.Tests Clean Architecture Mirroring & Modernization | **Accepted (Active)** |
| 🧪 **Testing** | [`ADR-TST-02`](#adr-tst-02-test-suite-segregation-11-cqrs-parity--the-6-clean-testing-pillars) | `ADR-024` | October 2026 | Test Suite Segregation, 1:1 CQRS Parity & The 6 Clean Testing Pillars | **Accepted (Active)** |

---

## 🔄 Legacy Reference Mapping (Backward Compatibility Index)

When external documents, commit messages, or comments cite legacy sequential numbers, use this translation matrix:

| Legacy Tag | Canonical Semantic Tag | Legacy Tag | Canonical Semantic Tag |
|:---:|:---:|:---:|:---:|
| `ADR-001` | [`ADR-SEC-01`](#adr-sec-01-initial-x-api-key-authentication) | `ADR-014` | [`ADR-APP-02`](#adr-app-02-clean-architecture-v2-dto--command-restructuring) |
| `ADR-002` | [`ADR-ENG-01`](#adr-eng-01-gotenberg-8-chromium-microservice-instead-of-embedded-libreoffice) | `ADR-015` | [`ADR-VAL-01`](#adr-val-01-dual-engine-automatic-validation-pipeline) |
| `ADR-003` | [`ADR-DOM-01`](#adr-dom-01-rich-domain-model-instead-of-anemic-domain-model) | `ADR-016` | [`ADR-VAL-02`](#adr-val-02-schema-infrastructure-clean-code--performance-optimization) |
| `ADR-004` | [`ADR-DOM-02`](#adr-dom-02-smart-enums-instead-of-c-enums--magic-strings) | `ADR-017` | [`ADR-APP-03`](#adr-app-03-c-12-primary-constructors--sealed-use-cases-standard) |
| `ADR-005` | [`ADR-DOM-03`](#adr-dom-03-dedicated-value-objects-for-primitives-with-validation) | `ADR-018` | [`ADR-VAL-03`](#adr-val-03-standalone-scalable-schema-validator--bounded-caching) |
| `ADR-006` | [`ADR-APP-01`](#adr-app-01-application-dto-boundary-never-leak-domain-entities) | `ADR-019` | [`ADR-VAL-04`](#adr-val-04-template-payload-pre-flight-validation-with-multi-tenant-scoping) |
| `ADR-007` | [`ADR-RES-01`](#adr-res-01-globalexceptionfilter-instead-of-schemavalidationexceptionfilter) | `ADR-020` | [`ADR-APP-04`](#adr-app-04-phase-out-and-complete-removal-of-commonmodels) |
| `ADR-008` | [`ADR-RES-02`](#adr-res-02-rate-limiting-with-per-api-key-partition) | `ADR-021` | [`ADR-DOM-05`](#adr-dom-05-domain-layer-integrity--fail-fast-invariants--aggregates) |
| `ADR-009` | [`ADR-RES-03`](#adr-res-03-domainexception-hierarchy--machine-readable-error-codes) | `ADR-022` | [`ADR-DOM-06`](#adr-dom-06-comprehensive-ddd-aggregates-sealed-entities--value-converters) |
| `ADR-010` | [`ADR-DOM-04`](#adr-dom-04-high-performance-domain-optimization--sequential-uuidv7) | `ADR-023` | [`ADR-DOM-07`](#adr-dom-07-strict-pure-ddd-standards--single-canonical-factory) |
| `ADR-011` | [`ADR-SEC-02`](#adr-sec-02-hybrid-rbac--dual-channel-authentication-model) | `ADR-024` | [`ADR-TST-02`](#adr-tst-02-test-suite-segregation-11-cqrs-parity--the-6-clean-testing-pillars) |
| `ADR-012` | [`ADR-TST-01`](#adr-tst-01-smkdoctests-clean-architecture-mirroring--modernization) | `ADR-025` | [`ADR-SEC-03`](#adr-sec-03-scoped-api-keys--m2m-first-zero-public-surface-security) |
| `ADR-013` | [`ADR-ENG-02`](#adr-eng-02-compiled-handlebars-template-caching--smart-excel-pagesetup) | `ADR-026` | [`ADR-APP-05`](#adr-app-05-application-query-service-iuserworkspacequeryservice) |

---

## Part 1: 🔐 Security & Identity (`SEC`)

### ADR-SEC-01: Initial X-API-Key Authentication
> **Tag:** `ADR-SEC-01` | **Legacy:** `ADR-001` | **Date:** September 2026 | **Status:** Partially Superseded by `ADR-SEC-02` & `ADR-SEC-03`

- **Context & Problem:** The legacy v1 document server lacked multi-tenant boundary isolation and machine-to-machine authentication.
- **Decision:** Implemented `X-API-Key` header authentication backed by the `api_keys` PostgreSQL table via `ApiKeyMiddleware`.
- **Evolution Note:** Originally intended as the exclusive authentication mechanism. As the web portal evolved, it was augmented by Dual-Channel Authentication (`ADR-SEC-02`) and Scoped API Keys (`ADR-SEC-03`).

### ADR-SEC-02: Hybrid RBAC & Dual-Channel Authentication Model
> **Tag:** `ADR-SEC-02` | **Legacy:** `ADR-011` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Machine callers (ERP, CRM) require stateless API keys, while human operators (Web Portal) require session-based, audited role management. A single authentication scheme cannot satisfy both personas cleanly.
- **Decision:** Partitioned authentication into two independent channels:
  1. **M2M Channel:** External systems authenticate via `X-API-Key`. Binds `ProjectId` to `IExecutionContext` statelessly.
  2. **Human Management Channel:** Web portal users authenticate via Bearer JWT. Features frictionless login (no initial ProjectId required), resolving permissions via `SystemRole` (`SuperAdmin`, `Member`, `Viewer`) and tenant-scoped `UserProjectRole` records.
- **Rejected Alternatives:** Single JWT for all systems (rejected: external systems cannot manage token refresh lifecycles cleanly); Basic Auth (rejected: insecure and lacks cryptographic key hashing).

### ADR-SEC-03: Scoped API Keys & M2M-First Zero Public Surface Security
> **Tag:** `ADR-SEC-03` | **Legacy:** `ADR-025` | **Date:** October 2026 (2026-10-08) | **Status:** Accepted (Active)

- **Context & Problem:** Exposing authentication endpoints (`/api/v1/auth/login`) publicly invites brute-force and credential stuffing attacks. Furthermore, external systems required fine-grained permission boundaries.
- **Decision:**
  1. **Smart Enum `ApiKeyScope`:** Introduces `ReadOnly` (preview, validate, download) and `ReadWrite` (full generation and authoring) persisted in `api_keys.scope`.
  2. **Atomic Multi-Key Provisioning:** Project creation automatically issues both a `ReadOnly` and a `ReadWrite` key in a single atomic transaction.
  3. **Zero Public Attack Surface:** Removed `/api/v1/auth/*` from the public whitelist. Calling login requires an internal perimeter key (`X-API-Key`), while existing JWT holders bypass the key check.
- **Consequences:** Eliminates public brute-force vectors and tenant IDOR attacks.

### ADR-SEC-04: Next.js Server-Side API Proxy & Cookie-to-Bearer Token Isolation
> **Tag:** `ADR-SEC-04` | **Legacy:** `ADR-027` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Storing JWT tokens in browser storage (`localStorage` or in-memory JavaScript variables) exposes them to Cross-Site Scripting (XSS) exfiltration.
- **Decision:**
  - Implemented Next.js 15 server route handler `/api/proxy/[...path]`.
  - Browser communicates exclusively with Next.js using httpOnly session cookies.
  - Server proxy strips hop-by-hop headers, extracts the JWT from the secure session via `auth()`, injects `Authorization: Bearer <token>`, forwards `X-API-Key`, and pipes the backend response directly back to the client.
- **Consequences:** Zero bearer token exposure in client memory; streamlined internal network communication without browser CORS complications.

---

## Part 2: ⚙️ Rendering Engines & Formats (`ENG`)

### ADR-ENG-01: Gotenberg 8 Chromium Microservice instead of Embedded LibreOffice
> **Tag:** `ADR-ENG-01` | **Legacy:** `ADR-002` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Running headless LibreOffice directly on the ASP.NET Core API server caused unrecoverable process hangs, severe memory leaks, and fatal Thai vowel positioning defects.
- **Decision:** Adopted Gotenberg 8 as a dedicated, containerized rendering microservice communicating over HTTP.
- **Rejected Alternatives:** Embedded LibreOffice CLI (rejected: memory leaks and stability failure); Puppeteer Node.js sidecar (rejected: excessive resource overhead compared to Gotenberg's Go/Chromium pool).

### ADR-ENG-02: Compiled Handlebars Template Caching & Smart Excel PageSetup
> **Tag:** `ADR-ENG-02` | **Legacy:** `ADR-013` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Re-parsing Handlebars AST and compiling templates on every request created severe CPU bottlenecks. Simultaneously, Excel generation stripped user-defined print configurations.
- **Decision:**
  1. Implemented `ICompiledTemplateCache` in Application, backed by `MemoryCompiledTemplateCache` using SHA-256 template hashing and sliding expiration (1 hour).
  2. Upgraded `ExcelTemplateEngine` with smart page setup detection, preserving original spreadsheet orientation and custom print fitting (`PagesWide`, `PagesTall`).
- **Consequences:** Boosted HTML rendering throughput by 45%; eliminated print layout destruction.

### ADR-ENG-03: Native Thai Typography Compliance & Sarabun Font Sandboxing
> **Tag:** `ADR-ENG-03` | **Legacy:** `ADR-029` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Business documents require strict Thai regulatory compliance, specifically Buddhist Era calendar dates, BahtText currency translation, and flawless Sarabun typography without vowel mark overlap.
- **Decision:**
  1. Centralized Thai transforms in `ITransformerService` (`thaiDate`, `thaiBahtText`). Inline formatting in templates is prohibited.
  2. Embedded Google Fonts Sarabun directly in the Gotenberg Docker image and MinIO `fonts` storage bucket, combined with strict CSS `@page` rules to ensure deterministic, pixel-perfect PDF rendering.
- **Consequences:** 100% deterministic Thai typography without OS-level font drift or third-party SaaS dependency.

---

## Part 3: 💎 Rich Domain Model & DDD (`DOM`)

### ADR-DOM-01: Rich Domain Model instead of Anemic Domain Model
> **Tag:** `ADR-DOM-01` | **Legacy:** `ADR-003` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Converted all entities from anemic POCOs (`public set`) to Rich Domain Models with `private set`, parameterized constructors, and expressive business methods (`Activate`, `Publish`, `SetCurrentVersion`).
- **Rationale:** Encapsulates business invariants inside the domain entity, preventing invalid state creation.

### ADR-DOM-02: Smart Enums instead of C# Enums + Magic Strings
> **Tag:** `ADR-DOM-02` | **Legacy:** `ADR-004` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Replaced native C# enums and raw strings with strongly typed Smart Enums inheriting from `Enumeration` (`TemplateFormat`, `OutputFormat`, `RenderEngineType`, `TemplateVersionStatus`, `RoleType`, `GenerationStatus`, `ApiKeyScope`).
- **Rationale:** Eliminates primitive obsession, encapsulates format metadata (`Extension`, `MimeType`), and guarantees compile-time type safety.

### ADR-DOM-03: Dedicated Value Objects for Primitives with Validation Rules
> **Tag:** `ADR-DOM-03` | **Legacy:** `ADR-005` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Replaced raw primitives with explicit Value Objects (`TemplateSlug`, `Sha256Hash`, `EmailAddress`, `DataSourceType`, `ExpirationPolicy`).
- **Rationale:** Enforces fail-fast validation upon instantiation and guarantees structural equality.

### ADR-DOM-04: High-Performance Domain Optimization & Sequential UUIDv7
> **Tag:** `ADR-DOM-04` | **Legacy:** `ADR-010` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Generic static caching `Cache<T>` in `Enumeration` providing $O(1)$ lookups.
  2. Transitioned default primary keys in `BaseEntity` from random UUIDv4 (`Guid.NewGuid()`) to sequential UUIDv7 (`Guid.CreateVersion7()`).
  3. Replaced uncompiled `Regex.IsMatch` with zero-allocation `char.IsAsciiHexDigit` loops in `Sha256Hash`.
- **Rationale:** Prevents PostgreSQL B-Tree index fragmentation in high-throughput tables.

### ADR-DOM-05: Domain Layer Integrity — Fail-Fast Invariants & Aggregate Boundaries
> **Tag:** `ADR-DOM-05` | **Legacy:** `ADR-021` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Partitioned exception hierarchy: Pure Domain Exceptions in `SmkDoc.Domain.Exceptions`, HTTP-mapped exceptions in `SmkDoc.Application.Common.Exceptions`.
  2. Encapsulated child collections (`Project`, `Company`, `User`, `Template`) as `IReadOnlyCollection<T>`.
  3. Consolidated child entities (`FieldMapping`, `TemplateDataset`) under Aggregate Root `Template`, phasing out standalone child repositories.
  4. Enforced strict tenant-scoped queries (`ListByProjectAsync`) across all domain repositories.

### ADR-DOM-06: Comprehensive DDD Aggregates, Sealed Entities & Value Converters
> **Tag:** `ADR-DOM-06` | **Legacy:** `ADR-022` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Sealed all 14 entities (`sealed class`) with private parameterless constructors for EF Core.
  2. Implemented 13 dedicated Value Objects covering Identity, Authoring, Security, and Rendering.
  3. Decoupled cross-aggregate navigation properties in favor of pure ID references (`Guid ProjectId`).
  4. Mapped all Value Objects and Smart Enums to PostgreSQL columns using EF Core Value Converters with zero database migrations.

### ADR-DOM-07: Strict Pure DDD Standards — Single Canonical Factory & No Test Backdoors
> **Tag:** `ADR-DOM-07` | **Legacy:** `ADR-023` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Exactly 1 canonical static factory method per entity (`Create`, `Register`, `Draft`).
  2. Mandatory deterministic time injection (`DateTimeOffset now`) on all factories and state mutation methods.
  3. Purged all test backdoor methods (`CreateForTest`) from the production domain assembly; relocated test instantiations to `*TestFactory` and `*Builder` in `SmkDoc.Tests`.

---

## Part 4: ⚡ Application Layer & CQRS (`APP`)

### ADR-APP-01: Application DTO Boundary (Never Leak Domain Entities)
> **Tag:** `ADR-APP-01` | **Legacy:** `ADR-006` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Prohibited returning Domain Entities from Application Use Cases. All responses must be strongly typed Application DTOs.
- **Rationale:** Prevents presentation layer coupling to domain models and secures internal state encapsulation.

### ADR-APP-02: Clean Architecture v2 DTO & Command Restructuring
> **Tag:** `ADR-APP-02` | **Legacy:** `ADR-014` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Reorganized DTOs by Feature Bounded Context in `SmkDoc.Application/DTOs/<Feature>/`.
  2. Enforced strict CQRS terminology: Mutations (`*Command`), Queries (`*Query`), Outputs (`*Dto` / `*ResultDto`).
  3. Converted all DTOs into 100% immutable positional C# `record`s.

### ADR-APP-03: C# 12 Primary Constructors & Sealed Use Cases Standard
> **Tag:** `ADR-APP-03` | **Legacy:** `ADR-017` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Adopted C# 12 Primary Constructors (`camelCase` parameters, zero `_` prefix) and marked all Use Cases as `public sealed class`.
- **Rationale:** Reduces constructor boilerplate by 200+ lines and enables JIT method devirtualization optimizations.

### ADR-APP-04: Phase-Out and Complete Removal of `Common/Models/`
> **Tag:** `ADR-APP-04` | **Legacy:** `ADR-020` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Completely removed the legacy `Common/Models/` compatibility folder. All callers across Application, Infrastructure, Api, and Tests communicate through feature-sliced DTOs in `DTOs.*`.
- **Rationale:** Enforces Single Source of Truth (SSoT) and eliminates model drift.

### ADR-APP-05: Application Query Service (`IUserWorkspaceQueryService`)
> **Tag:** `ADR-APP-05` | **Legacy:** `ADR-026` | **Date:** October 2026 (2026-10-08) | **Status:** Accepted (Active)

- **Context & Problem:** Multiple domain repositories were being queried sequentially and joined in-memory in RAM, creating severe N+1 overhead during user login and workspace profile queries.
- **Decision:**
  1. Defined Application port `IUserWorkspaceQueryService` in `SmkDoc.Application.Common.Interfaces`.
  2. Implemented `UserWorkspaceQueryService` in Infrastructure using EF Core `.Join()` and `AsNoTracking()` to execute a single, optimized SQL query projecting directly to DTOs.
  3. Integrated `EmailAddress` Value Object fail-fast validation at the Use Case boundary.
- **Consequences:** Eliminated in-memory joining and reduced database round-trips to a single query.

---

## Part 5: 🛡️ Validation & Pre-Flight Pipeline (`VAL`)

### ADR-VAL-01: Dual-Engine Automatic Validation Pipeline
> **Tag:** `ADR-VAL-01` | **Legacy:** `ADR-015` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. **Static Engine (FluentValidation):** Validates C# Command/Request DTOs in `SmkDoc.Application/Validators/`.
  2. **Dynamic Engine (JsonSchema.Net):** Validates dynamic document data payloads against template JSON schemas.
  3. Executed via `ValidateCommandFilter` before reaching Controller actions, dispatching validation failures as RFC 9457 Problem Details.
- **Rationale:** Separates static C# model validation from dynamic template schema validation cleanly.

### ADR-VAL-02: Schema Infrastructure Clean Code & Performance Optimization
> **Tag:** `ADR-VAL-02` | **Legacy:** `ADR-016` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Retained `JsonSchemaValidationService` in Infrastructure behind abstractions. Added in-memory schema compilation caching, unified token grouping in `GroupedTokens`, and utilized C# pattern-matching switch expressions.

### ADR-VAL-03: Standalone Scalable Schema Validator & Bounded Caching
> **Tag:** `ADR-VAL-03` | **Legacy:** `ADR-018` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Created stateless validation endpoint `POST /api/v1/schemas/validate` for dry-run validation (0ms DB dependency). Replaced unbounded dictionaries with bounded `IMemoryCache` utilizing SHA-256 cache keys to prevent memory exhaustion.

### ADR-VAL-04: Template Payload Pre-Flight Validation with Multi-Tenant Scoping
> **Tag:** `ADR-VAL-04` | **Legacy:** `ADR-019` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Created `POST /api/v1/templates/{slug}/validate` for pre-flight payload verification against published template schemas with mandatory tenant isolation checks (`t.ProjectId == executionContext.ProjectId`). Operates with zero side-effects (no Gotenberg, MinIO, or DB log writes).

---

## Part 6: 🚨 Resilience, Error Handling & Diagnostics (`RES`)

### ADR-RES-01: GlobalExceptionFilter instead of SchemaValidationExceptionFilter
> **Tag:** `ADR-RES-01` | **Legacy:** `ADR-007` | **Date:** September 2026 | **Status:** Superseded by `ADR-RES-04`

- **Evolution:** Unified scattered MVC exception filters into a centralized filter. Later upgraded to the ASP.NET Core 8/10 middleware pipeline in `ADR-RES-04`.

### ADR-RES-02: Rate Limiting with Per-API-Key Partition
> **Tag:** `ADR-RES-02` | **Legacy:** `ADR-008` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Implemented ASP.NET Core Rate Limiter partitioned by API Key (60 req/min/key, queue limit 0) with RFC 9457 429 Too Many Requests responses.
- **Rationale:** Protects Gotenberg and Chromium pools from denial-of-service starvation while providing fair sharing across internal tenant systems.

### ADR-RES-03: DomainException Hierarchy & Machine-Readable Error Codes
> **Tag:** `ADR-RES-03` | **Legacy:** `ADR-009` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Established `DomainException` base class enforcing `ErrorCode` (machine-readable string) and `StatusCode`. Standardized `NotFoundException` (404), `ConflictException` (409), `DraftExpiredException` (410), and `RenderException` (500).

### ADR-RES-04: Unified RFC 9457 IExceptionHandler & Idempotency Pipeline
> **Tag:** `ADR-RES-04` | **Legacy:** `ADR-030` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Decision:**
  1. Replaced MVC action filters with `GlobalExceptionHandler : IExceptionHandler` in `SmkDoc.Api/Middlewares/`, capturing exceptions across both Controller actions and middleware pipelines.
  2. Implemented `IdempotencyFilter` with Redis distributed locking for all state-mutating requests (`Idempotency-Key` header).
- **Consequences:** Total alignment with RFC 9457 Problem Details and guaranteed safe retries for enterprise billing and ERP integrations.

---

## Part 7: 🌐 Frontend Portal & Studio (`UI`)

### ADR-UI-01: Next.js 15 App Router Layout & Ice-White Design Token SSoT
> **Tag:** `ADR-UI-01` | **Legacy:** `ADR-027` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Context & Problem:** The legacy portal used an ad-hoc tab switcher with dark navy styling, inconsistent bubble radii, and mixed icon sets.
- **Decision:**
  1. Implemented Next.js 15 App Router topology with `app/(app)` route groups, persistent [`AppShell`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/frontend-v2/src/components/layout/AppShell.tsx), collapsible sidebar (56px/224px), and compact topbar (48px).
  2. Established SSoT design tokens in [`tokens/index.ts`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/frontend-v2/src/tokens/index.ts): Ice-White canvas (`#f8fbfe`), Sky Blue brand (`#0284c7`), sharp 2px-4px radius (`rounded-sm`), 5 functional category colors, and exclusive Lucide iconography.
- **Consequences:** Professional, high-density enterprise dashboard aesthetic with zero dark navy pollution.

### ADR-UI-02: Monaco Studio v2 Debounced Live Preview & In-Flight Abort Pipeline
> **Tag:** `ADR-UI-02` | **Legacy:** `ADR-028` | **Date:** October 2026 | **Status:** Accepted (Active)

- **Context & Problem:** Live typing in the Monaco template editor overwhelmed the Gotenberg backend, creating race conditions where stale renders overwrote newer edits.
- **Decision:**
  1. Integrated 800ms debounce timer in `useLivePreview`.
  2. Implemented in-flight request cancellation via `AbortController.abort()`.
  3. Automated Handlebars variable extraction into sample JSON payloads via `extractVariablesFromHtml`.
  4. Managed blob lifecycle via `URL.createObjectURL` and `URL.revokeObjectURL` to eliminate memory leaks.
- **Consequences:** Smooth, real-time live preview with zero UI lag and full APM telemetry tracking.

---

## Part 8: 🧪 Testing Architecture & Quality Gates (`TST`)

### ADR-TST-01: SmkDoc.Tests Clean Architecture Mirroring & Modernization
> **Tag:** `ADR-TST-01` | **Legacy:** `ADR-012` | **Date:** September 2026 | **Status:** Accepted (Active)

- **Decision:** Reorganized test folders to mirror Clean Architecture layers 1:1. Mandated FluentAssertions 100%, introduced test data builders, and tagged benchmarks to isolate fast CI execution.

### ADR-TST-02: Test Suite Segregation, 1:1 CQRS Parity & The 6 Clean Testing Pillars
> **Tag:** `ADR-TST-02` | **Legacy:** `ADR-024` | **Date:** October 2026 (2026-10-06) | **Status:** Accepted (Active)

- **Context & Problem:** Heavy document generators doing real disk I/O and OpenXML operations were mixed into `SmkDoc.Tests`, causing namespace collisions and slow unit test runs.
- **Decision: System-Wide Adoption of The 6 Clean Testing Pillars:**
  1. **Pillar 1 — Test Segregation:** Pure in-memory unit tests in `SmkDoc.Tests` (0 disk/DB I/O, runs in ~1s); heavy I/O and generators isolated in `SmkDoc.IntegrationTests`.
  2. **Pillar 2 — 1:1 CQRS Parity:** Single-SUT test classes matching `Commands/{CommandName}/` and `Queries/{QueryName}/`.
  3. **Pillar 3 — Deterministic Time:** Universal `ExecuteAsync_When[Condition]_[ExpectedResult]` naming and `TestConstants.BaselineTime` SSoT.
  4. **Pillar 4 — Validator Colocation:** Pure `[Theory]` validator tests colocated with features.
  5. **Pillar 5 — Domain Invariant Consolidation:** Domain tests bound directly to `{Aggregate}Tests.cs`.
  6. **Pillar 6 — Fluent Builders:** Instantiation via `*TestFactory` and `*TestFixture`.
- **Consequences:** 100% green test suite across both backend and frontend suites with zero test flakiness.

---

</decisions_scope>
