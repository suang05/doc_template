<ai_directive>
CRITICAL ATTENTION ROUTING: 
This file (`PROJECT_STRUCTURE.md`) defines the Canonical Baseline Folder Map for `smk-doc-server`.
1. **Enforce the Baseline:** Do not revert to legacy structures. Maintain modern .NET patterns with modular grouping, Action Filters for fail-fast validation (`ValidateCommandFilter`), and centralized pipeline error handling (`GlobalExceptionHandler` via `IExceptionHandler`).
2. **Open for Evolution (The Override Rule):** While this structure strictly enforces Clean Architecture & Modular Monolith patterns, it is NOT a static prison. If you identify a genuinely superior architectural evolution (e.g., newer .NET paradigms, better Vertical Slice optimizations, or advanced performance patterns) that improves the system beyond this baseline, you MUST proactively propose it to the user. Do not let these rules blind you to better engineering choices.
</ai_directive>

<project_structure_scope>

# 🏗️ PROJECT_STRUCTURE.md — Solution Layout & Canonical Folder Map

> **Purpose:** Comprehensive directory tree and complete catalog of all Entities, Enums, Value Objects, UseCases, and Adapters.  
> **Related Docs:** [ARCHITECTURE.md](ARCHITECTURE.md) (Layer rules), [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Naming conventions).

### ⚡ Quick-Lookup: Projects & Namespaces

| Project | Namespace | Layer Responsibility | Key Directories |
|---|---|---|---|
| `SmkDoc.Domain` | `SmkDoc.Domain` | Business invariants & Core Models | `Entities/`, `Enums/`, `ValueObjects/`, `Exceptions/` |
| `SmkDoc.Application` | `SmkDoc.Application` | Workflows, UseCases & DTOs | `Modules/`, `Common/Interfaces/`, `Common/Exceptions/` |
| `SmkDoc.Infrastructure` | `SmkDoc.Infrastructure` | Database, MinIO, Engines, Gotenberg | `Persistence/`, `Storage/`, `Engines/`, `Pdf/`, `Schema/` |
| `SmkDoc.Api` | `SmkDoc.Api` | HTTP Controllers & Middleware | `Controllers/`, `Middleware/`, `ExceptionHandlers/`, `Filters/`, `Contracts/` |
| `frontend-v2` | — | Next.js 15 App Router Portal | `src/app/`, `src/components/`, `src/schemas/`, `src/lib/api/` |

---

## 🌍 Global Design Patterns & Standards Applied

This folder structure is not arbitrary. It strictly enforces the following modern enterprise software architecture patterns (popularized by Microsoft MVPs and standard .NET 8/10 best practices):

1. **Clean Architecture (The Dependency Rule):** Dependencies always point inwards toward `SmkDoc.Domain`. The Core has zero knowledge of EF Core, ASP.NET, or MinIO.
2. **Domain-Driven Design (DDD):** Evident in `SmkDoc.Domain` via the use of Rich Entities (encapsulated logic), Value Objects (structural equality), and Smart Enums (behavioral enums rather than primitive integers).
3. **Modular Monolith & Vertical Slicing:** Notice how `SmkDoc.Application` and `SmkDoc.Api/Controllers` are heavily grouped by Bounded Contexts (e.g., `Rendering`, `Authoring`, `Integration`) rather than traditional technical folders. This prevents the unmaintainable "Giant Services Folder" anti-pattern.
4. **CQRS (Command Query Responsibility Segregation):** Inside each Application Module, operations are strictly separated into `Commands/` (mutations) and `Queries/` (reads), providing targeted optimization.
5. **Automatic Validation (Fail-Fast):** Utilizing `ValidateCommandFilter` (Action Filter) to automatically intercept and validate all incoming commands via FluentValidation *before* they ever reach the UseCase, keeping the Application layer completely free of pipeline boilerplate.

---

<backend_scope>
## ⚙️ Backend (backend-v2/)

```text
backend-v2/
├── Directory.Build.props           # .NET 10, C# 13, Nullable enable, ImplicitUsings
├── Directory.Packages.props        # Central Package Management (no duplicate versions)
├── SmkDocServerV2.slnx
├── src/
│   ├── SmkDoc.Domain/              # 🟢 Core — Zero external dependencies
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs       # Entity base class (UUIDv7, CreatedAt, UpdatedAt)
│   │   │   ├── Enumeration.cs      # Smart Enum base class (Id + Name + behavior)
│   │   │   └── ValueObject.cs      # Value Object base class (structural equality)
│   │   ├── Entities/               # 15 Rich Domain Models (private set + business methods)
│   │   │   ├── Template.cs
│   │   │   ├── TemplateVersion.cs
│   │   │   ├── FieldMapping.cs
│   │   │   ├── GenerationLog.cs
│   │   │   ├── ApiKey.cs
│   │   │   ├── Document.cs
│   │   │   ├── DocumentVersion.cs
│   │   │   ├── User.cs
│   │   │   ├── RefreshToken.cs
│   │   │   ├── Project.cs
│   │   │   ├── Company.cs
│   │   │   ├── DataConnection.cs
│   │   │   ├── Dataset.cs
│   │   │   ├── TemplateDataset.cs
│   │   │   └── UserProjectRole.cs
│   │   ├── Enums/                  # Smart Enums (สืบทอดจาก Enumeration, O(1) Cache)
│   │   │   ├── TemplateFormat.cs   # Html / Docx / Xlsx
│   │   │   ├── OutputFormat.cs     # Pdf / Docx / Xlsx
│   │   │   ├── RenderEngineType.cs # Html / Docx / Excel
│   │   │   ├── TemplateVersionStatus.cs
│   │   │   ├── ApiKeyScope.cs
│   │   │   ├── RoleType.cs
│   │   │   ├── SystemRole.cs
│   │   │   └── GenerationStatus.cs
│   │   ├── ValueObjects/           # Immutable types with structural equality (13 Value Objects)
│   │   │   ├── Validation/         # SchemaValidationResult, ValidationErrorItem
│   │   │   └── ...                 # ApiKeyName, Sha256Hash, TemplateSlug, etc.
│   │   ├── Exceptions/             # Domain Exceptions (Pure POCO — mapped by GlobalExceptionHandler to RFC 9457)
│   │   │   ├── DomainException.cs           # Abstract base class (ErrorCode)
│   │   │   ├── DomainValidationException.cs # Invariant violation (format, required, range)
│   │   │   └── BusinessRuleViolationException.cs # Aggregate/state transition rule violation
│   │   └── Interfaces/
│   │       ├── IMustHaveProject.cs              # Multi-tenant isolation contract
│   │       ├── IUnitOfWork.cs
│   │       └── ITemplateRepository.cs           # Domain repository contracts...
│   │
│   ├── SmkDoc.Application/         # 🔵 Use Cases & Ports — Depends on Domain only
│   │   ├── Common/
│   │   │   ├── Exceptions/         # Application Exceptions (mapped to HTTP by GlobalExceptionHandler to RFC 9457)
│   │   │   │   ├── NotFoundException.cs         # → HTTP 404 (RESOURCE_NOT_FOUND)
│   │   │   │   ├── ConflictException.cs         # → HTTP 409 (RESOURCE_CONFLICT)
│   │   │   │   ├── SchemaValidationException.cs # → HTTP 400 (RFC 9457)
│   │   │   │   └── ...
│   │   │   ├── Interfaces/         # Ports (Abstractions ที่ Infrastructure จะ Implement)
│   │   │   │   ├── IUseCase.cs             # Standard Single-Responsibility Use Case contract
│   │   │   │   ├── IRepository<T>.cs
│   │   │   │   ├── IStorageService.cs
│   │   │   │   ├── IIdempotencyStore.cs    # IETF Idempotency contract & records (IdempotencyRecord, AcquisitionResult)
│   │   │   │   └── ...
│   │   │   └── Helpers/
│   │   │       ├── ThaiDataTransformer.cs  # SSoT สำหรับ Thai formatting
│   │   │       └── ...
│   │   ├── Modules/                # 🎯 4 Bounded Contexts (Modular Monolith with Total Colocation)
│   │   │   ├── Rendering/          # High-Throughput & Stateless Document Generation Core
│   │   │   │   ├── Documents/      # Commands/, Queries/, Services/, DTOs/, Validators/
│   │   │   │   └── Logs/           # Queries/, DTOs/
│   │   │   ├── Authoring/          # Template Studio, Version Lifecycle, Form/Schema Design
│   │   │   │   ├── Templates/      # Commands/, Queries/, DTOs/, Validators/
│   │   │   │   ├── FieldMappings/
│   │   │   │   ├── Fonts/
│   │   │   │   └── Schemas/
│   │   │   ├── Integration/        # External Data Sources & Dynamic Data Pipeline
│   │   │   │   ├── DataConnections/
│   │   │   │   └── Datasets/
│   │   │   └── IdentityAccess/     # IAM, Multi-tenancy Isolation & Security
│   │   │       ├── Users/
│   │   │       ├── Projects/
│   │   │       └── Security/
│   │   └── DependencyInjection.cs  # Aggregates 4 modules into AddApplicationServices()
│   │
│   ├── SmkDoc.Infrastructure/      # 🟡 Adapters — Implements Application Ports
│   │   ├── Contexts/
│   │   │   └── ExecutionContextImpl.cs  # Scoped: CallerApp, ApiKeyId, ClientIp
│   │   ├── Cache/
│   │   │   ├── InMemoryTemplateDraftCache.cs
│   │   │   ├── MemoryCompiledTemplateCache.cs
│   │   │   └── MemoryIdempotencyStore.cs    # Thread-safe in-memory IIdempotencyStore (IMemoryCache)
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs     # EF Core + Fluent API mappings
│   │   │   ├── EfRepository<T>.cs  # Generic Repository
│   │   │   ├── Repositories/       # Specific Interface Implementations
│   │   │   └── Migrations/
│   │   ├── Storage/
│   │   │   └── MinioStorageService.cs
│   │   ├── Pdf/
│   │   │   └── GotenbergPdfRenderer.cs # Chromium & LibreOffice endpoints (Polly v8)
│   │   ├── Engines/
│   │   │   ├── Html/               # HtmlTemplateEngine (Handlebars)
│   │   │   ├── Word/               # DocxTemplateEngine (OpenXML)
│   │   │   └── Excel/              # ExcelTemplateEngine (ClosedXML)
│   │   ├── Security/               # BcryptPasswordHasher, JwtTokenGenerator
│   │   ├── Schema/                 # JsonSchemaValidationService
│   │   └── Observability/          # DocumentMetrics (System.Diagnostics.Metrics)
│   │
│   └── SmkDoc.Api/                 # 🔴 Presentation — HTTP surface only
│       ├── Controllers/            # Thin HTTP Controllers organized by Bounded Context
│       │   ├── Rendering/          # DocumentController, AuditLogController
│       │   ├── Authoring/          # TemplateController, SchemaController...
│       │   ├── Integration/        # DataConnectionsController, DatasetController
│       │   └── IdentityAccess/     # AuthController, UserManagementController...
│       ├── Contracts/              # Decoupled Request/Response records organized by Module
│       │   ├── IdentityAccess/
│       │   ├── Authoring/
│       │   ├── Rendering/
│       │   └── Integration/
│       ├── Common/
│       │   ├── Context/            # ExecutionContextImpl.cs (IExecutionContext provider)
│       │   └── Responses/          # ApiResponse<T>.cs, PagedApiResponse<T>.cs
│       ├── Middleware/
│       │   ├── ApiKeyMiddleware.cs       # X-API-Key validation → ExecutionContext
│       │   └── SecurityHeadersMiddleware.cs
│       ├── ExceptionHandlers/      # ✅ MODERN .NET 8+ PIPELINE-WIDE ERROR HANDLING
│       │   └── GlobalExceptionHandler.cs # Implements IExceptionHandler, Maps to RFC 9457 ProblemDetails
│       ├── Filters/                # MVC Action Filters (Fail-fast validation & Idempotency)
│       │   ├── ValidateCommandFilter.cs       # Intercepts commands & executes FluentValidation automatically
│       │   ├── IdempotencyFilter.cs           # IETF Idempotency-Key handler (lock, replay, rollback)
│       │   ├── IdempotentAttribute.cs         # Decorator for mutation endpoints (Opt-in/Mandatory, TTL)
│       │   └── RequestFingerprintCalculator.cs# Deterministic SHA-256 fingerprint for payload verification
│       ├── HealthChecks/           # Postgres, MinIO, Gotenberg health checks
│       └── Program.cs              # DI, Middleware pipeline, Swagger, Rate Limiting
│
└── tests/
    ├── SmkDoc.Tests/                   # Pure In-Memory Unit Test Suite (Zero I/O)
    └── SmkDoc.IntegrationTests/        # Segregated Integration & Benchmark Suite
```
</backend_scope>

---

<frontend_scope>
## 🌐 Frontend (frontend-v2/)

```text
frontend-v2/
├── src/
│   ├── tokens/                     # SSoT Design Tokens — ห้าม Hardcode สี/ขนาด
│   ├── schemas/                    # Zod Schemas สำหรับ validate API responses
│   ├── lib/api/                    # API Adapters (fetch wrappers)
│   ├── hooks/                      # Custom Hooks (Business Logic + Data fetching)
│   ├── components/
│   │   ├── ui/                     # Atomic: Button, Dropdown, Badge, Input (2-4px radius)
│   │   ├── layout/                 # AppShell, Sidebar, Topbar
│   │   └── features/               # Feature blocks: TemplatesView, GeneratorView, ...
│   └── app/                        # Next.js 15 App Router pages
└── ...
```
</frontend_scope>

---

## 🐳 Docker Orchestration

**V2 stack only** — ใช้ `docker-compose.v2.yml` กับ project name `smk-v2` เสมอ:

```bash
# ✅ Correct
docker compose -p smk-v2 -f docker-compose.v2.yml up -d

# ❌ Wrong — อย่าใช้ docker-compose.yml (V1 legacy)
```

**Services ใน V2:**
- `smk-api-v2` — ASP.NET Core 10 API (port 5100)
- `smk-portal-v2` — Next.js 15 Portal
- `postgres` — PostgreSQL (smkdoc database)
- `minio` — Object Storage
- `gotenberg` — PDF conversion service
</project_structure_scope>
