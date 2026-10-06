# PROJECT_STRUCTURE.md — Solution Layout & Canonical Folder Map

> **Purpose:** Comprehensive directory tree and complete catalog of all Entities, Enums, Value Objects, UseCases, and Adapters.  
> **Related Docs:** [ARCHITECTURE.md](ARCHITECTURE.md) (Layer rules), [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Naming conventions).

### ⚡ Quick-Lookup: Projects & Namespaces

| Project | Namespace | Layer Responsibility | Key Directories |
|---|---|---|---|
| `SmkDoc.Domain` | `SmkDoc.Domain` | Business invariants & Core Models | `Entities/`, `Enums/`, `ValueObjects/`, `Exceptions/` |
| `SmkDoc.Application` | `SmkDoc.Application` | Workflows, UseCases & DTOs | `Modules/`, `Common/Interfaces/`, `Common/Helpers/` |
| `SmkDoc.Infrastructure` | `SmkDoc.Infrastructure` | Database, MinIO, Engines, Gotenberg | `Persistence/`, `Storage/`, `Engines/`, `Pdf/`, `Schema/` |
| `SmkDoc.Api` | `SmkDoc.Api` | HTTP Controllers & Middleware | `Controllers/`, `Middleware/`, `Program.cs` |
| `frontend-v2` | — | Next.js 15 App Router Portal | `src/app/`, `src/components/`, `src/schemas/`, `src/lib/api/` |

---

## Backend (backend-v2/)

```
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
│   │   ├── Entities/               # 14 Rich Domain Models (private set + business methods)
│   │   │   ├── Template.cs
│   │   │   ├── TemplateVersion.cs
│   │   │   ├── FieldMapping.cs
│   │   │   ├── GenerationLog.cs
│   │   │   ├── ApiKey.cs
│   │   │   ├── Document.cs
│   │   │   ├── DocumentVersion.cs
│   │   │   ├── User.cs
│   │   │   ├── Project.cs
│   │   │   ├── Company.cs
│   │   │   ├── DataConnection.cs
│   │   │   ├── Dataset.cs
│   │   │   ├── TemplateDataset.cs
│   │   │   └── UserProjectRole.cs
│   │   ├── Enums/                  # Smart Enums (สืบทอดจาก Enumeration, O(1) Cache)
│   │   │   ├── TemplateFormat.cs   # Html / Docx / Xlsx — มี .Extension, .MimeType, .DefaultEngineType
│   │   │   ├── OutputFormat.cs     # Pdf / Docx / Xlsx
│   │   │   ├── RenderEngineType.cs # Html / Docx / Excel
│   │   │   ├── TemplateVersionStatus.cs  # Draft / Published / Archived
│   │   │   ├── RoleType.cs        # Admin / Developer / Viewer (Project-Scoped)
│   │   │   ├── SystemRole.cs      # SuperAdmin / Member / Viewer (Platform-Level)
│   │   │   └── GenerationStatus.cs # Success / Failed / Processing / Timeout

│   │   ├── ValueObjects/           # Immutable types with structural equality (13 Value Objects)
│   │   │   ├── ApiKeyName.cs, CompanyName.cs, ConnectionName.cs, DatasetAlias.cs, DatasetName.cs
│   │   │   ├── DataSourceType.cs, DocumentReference.cs, EmailAddress.cs, ExpirationPolicy.cs
│   │   │   ├── ProjectName.cs, Sha256Hash.cs, TemplateName.cs, TemplateSlug.cs
│   │   │   └── Validation/         # SchemaValidationResult, ValidationErrorItem (Pure Domain Result Objects)
│   │   ├── Exceptions/             # Domain Exceptions (Pure POCO — mapped by GlobalExceptionFilter)
│   │   │   ├── DomainException.cs           # Abstract base class (ErrorCode)
│   │   │   ├── DomainValidationException.cs # Invariant violation (format, required, range)
│   │   │   └── BusinessRuleViolationException.cs # Aggregate/state transition rule violation
│   │   └── Interfaces/
│   │       ├── IMustHaveProject.cs              # Multi-tenant isolation contract
│   │       ├── IUnitOfWork.cs                   # CommitAsync(CancellationToken)
│   │       ├── ITemplateRepository.cs           # Domain repository contract for Template aggregate
│   │       ├── IUserRepository.cs               # Domain repository contract for User aggregate
│   │       ├── IUserProjectRoleRepository.cs    # Domain repository contract for UserProjectRole
│   │       ├── IProjectRepository.cs            # Domain repository contract for Project aggregate
│   │       ├── ICompanyRepository.cs            # Domain repository contract for Company aggregate
│   │       ├── IApiKeyRepository.cs             # Domain repository contract for ApiKey aggregate
│   │       ├── IDatasetRepository.cs            # Domain repository contract for Dataset aggregate
│   │       └── IDataConnectionRepository.cs     # Domain repository contract for DataConnection aggregate
│   │
│   ├── SmkDoc.Application/         # 🔵 Use Cases & Ports — Depends on Domain only
│   │   ├── Common/
│   │   │   ├── Exceptions/         # Application Exceptions (mapped to HTTP by GlobalExceptionFilter)
│   │   │   │   ├── NotFoundException.cs         # → HTTP 404 (RESOURCE_NOT_FOUND)
│   │   │   │   ├── ConflictException.cs         # → HTTP 409 (RESOURCE_CONFLICT)
│   │   │   │   ├── DraftExpiredException.cs     # → HTTP 410 (DRAFT_EXPIRED)
│   │   │   │   ├── SchemaValidationException.cs # → HTTP 400 (SCHEMA_VALIDATION_FAILED, with errors array)
│   │   │   │   ├── RenderException.cs           # → HTTP 500 (DOCUMENT_RENDER_FAILED)
│   │   │   │   ├── UnauthorizedException.cs     # → HTTP 401 (UNAUTHORIZED)
│   │   │   │   └── ValidationException.cs       # → HTTP 400 (VALIDATION_FAILED)
│   │   │   ├── Interfaces/         # Ports (Abstractions ที่ Infrastructure จะ Implement)
│   │   │   │   ├── IUseCase.cs             # Standard Single-Responsibility Use Case contract
│   │   │   │   ├── IRepository<T>.cs
│   │   │   │   ├── IStorageService.cs
│   │   │   │   ├── IPdfRenderer.cs         # Stream-first: RenderHtmlToPdfStreamAsync, RenderOfficeToPdfStreamAsync
│   │   │   │   ├── IRenderEngine.cs        # Stream-first: RenderStreamAsync, RenderAsync
│   │   │   │   ├── IDocumentMetrics.cs     # Real-time APM Metrics & Tracing Contract
│   │   │   │   ├── IExecutionContext.cs
│   │   │   │   └── ...
│   │   │   └── Helpers/
│   │   │       ├── ThaiDataTransformer.cs  # SSoT สำหรับ Thai formatting ทุกประเภท
│   │   │       ├── PlaceholderHelper.cs    # SSoT สำหรับ Placeholder Regex
│   │   │       └── FieldMappingApplicatorService.cs
│   │   ├── Modules/                # 🎯 4 Bounded Contexts (Modular Monolith with Total Colocation)
│   │   │   ├── Rendering/          # High-Throughput & Stateless Document Generation Core
│   │   │   │   ├── Documents/      # Commands/ (GenerateDocument, HtmlToPdf, RenderStatelessDocument), Queries/ (PreviewDocument, GetDocumentVersions, DownloadDocumentVersion), Services/ (DataPreparation, Audit, Versioning), DTOs/, Validators/
│   │   │   │   ├── Logs/           # Queries/ (ListGenerationLogs, GetLogMetrics, GetLogDownloadUrl), DTOs/ (LogMetricsDto, GenerationLogDto)
│   │   │   │   └── RenderingModuleExtensions.cs
│   │   │   ├── Authoring/          # Template Studio, Version Lifecycle, Form/Schema Design
│   │   │   │   ├── Templates/      # Commands/ (Create, Update, Activate, Deactivate, Rollback, ParseDraft, CommitDraft, SaveTemplateHtml), Queries/ (GetById, List, ListVersions, Download, Scan, PreviewDraft, ValidateTemplateHtml, ValidateTemplatePayload), DTOs/ (TemplateResultDto, TemplateDtos), Validators/
│   │   │   │   ├── FieldMappings/  # Commands/ (SaveTemplateMappings, SaveTemplateDatasets), Queries/ (GetTemplateMappings, GetTemplateDatasets, PreviewMapping), DTOs/
│   │   │   │   ├── Fonts/          # Commands/ (UploadFont), Queries/ (ListFonts, GetFontBase64), DTOs/
│   │   │   │   ├── Schemas/        # ValidateStandaloneSchemaUseCase, DTOs/ (SchemaValidationDtos)
│   │   │   │   └── AuthoringModuleExtensions.cs
│   │   │   ├── Integration/        # External Data Sources & Dynamic Data Pipeline
│   │   │   │   ├── DataConnections/# Commands/ (Create, Test), Queries/ (List), DTOs/ (DataConnectionDtos)
│   │   │   │   ├── Datasets/       # Commands/ (Create, Update, Delete), Queries/ (GetById, List, PreviewQuery), DTOs/ (DatasetDtos)
│   │   │   │   └── IntegrationModuleExtensions.cs
│   │   │   └── IdentityAccess/     # IAM, Multi-tenancy Isolation & Security
│   │   │       ├── Users/          # Commands/ (InviteUser, RemoveUser, SetUserStatus, UpdateUserRole), Queries/ (ListProjectUsers), DTOs/, Validators/
│   │   │       ├── Projects/       # Commands/ (CreateProject), Queries/ (ListProjects, GetProjectById), DTOs/, Validators/
│   │   │       ├── Security/       # Commands/ (Login, CreateApiKey, RevokeApiKey), Queries/ (ListApiKeys, ValidateApiKey), Helpers/ (ApiKeyHelper), DTOs/, Validators/
│   │   │       └── IdentityAccessModuleExtensions.cs
│   │   └── DependencyInjection.cs  # Aggregates 4 modules into AddApplicationServices()
│   │
│   ├── SmkDoc.Infrastructure/      # 🟡 Adapters — Implements Application Ports
│   │   ├── Contexts/
│   │   │   └── ExecutionContextImpl.cs  # Scoped: CallerApp, ApiKeyId, ClientIp
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs     # EF Core + Fluent API mappings
│   │   │   ├── EfRepository<T>.cs  # Generic Repository
│   │   │   ├── Repositories/
│   │   │   │   ├── TemplateRepository.cs            # ITemplateRepository implementation
│   │   │   │   ├── UserRepository.cs                # IUserRepository implementation
│   │   │   │   ├── UserProjectRoleRepository.cs     # IUserProjectRoleRepository implementation
│   │   │   │   ├── ProjectRepository.cs             # IProjectRepository implementation
│   │   │   │   ├── CompanyRepository.cs             # ICompanyRepository implementation
│   │   │   │   ├── ApiKeyRepository.cs              # IApiKeyRepository implementation
│   │   │   │   ├── DatasetRepository.cs             # IDatasetRepository implementation
│   │   │   │   ├── DataConnectionRepository.cs      # IDataConnectionRepository implementation
│   │   │   │   ├── GenerationLogMetricsRepository.cs
│   │   │   │   └── UnitOfWork.cs
│   │   │   └── Migrations/
│   │   ├── Storage/
│   │   │   └── MinioStorageService.cs  # Buckets: "templates", "outputs" (Stream-to-Stream upload)
│   │   ├── Pdf/
│   │   │   └── GotenbergPdfRenderer.cs # Chromium & LibreOffice endpoints (Direct stream piping + Polly v8)
│   │   ├── Engines/
│   │   │   ├── Html/               # HtmlTemplateEngine (Handlebars + Zero-LOH Stream)
│   │   │   ├── Word/               # DocxTemplateEngine (OpenXML Pipeline + Zero-LOH Stream)
│   │   │   └── Excel/              # ExcelTemplateEngine (ClosedXML + Zero-LOH Stream)
│   │   ├── Security/               # BcryptPasswordHasher, JwtTokenGenerator, etc.
│   │   ├── Schema/                 # JsonSchemaValidationService (Bounded IMemoryCache + SHA256), SchemaInferenceService
│   │   ├── Observability/          # DocumentMetrics (System.Diagnostics.Metrics.Meter + ActivitySource)
│   │   └── Cache/                  # InMemoryTemplateDraftCache, MemoryCompiledTemplateCache
│   │
│   └── SmkDoc.Api/                 # 🔴 Presentation — HTTP surface only
│       ├── Controllers/            # Thin HTTP Controllers organized by Bounded Context
│       │   ├── Rendering/          # DocumentController (Stream preview & output), AuditLogController
│       │   ├── Authoring/          # TemplateController, TemplateDraftController, TemplateHtmlController, TemplateVersionController, SchemaController, FontManagementController...
│       │   ├── Integration/        # DataConnectionsController, DatasetController
│       │   └── IdentityAccess/     # AuthController, UserManagementController, ProjectManagementController, ApiKeyController, ApiKeyManagementController
│       ├── Contracts/              # Decoupled Request/Response records organized by Module
│       │   ├── IdentityAccess/     # Auth, Projects, ApiKeys, Users
│       │   ├── Authoring/          # Fonts, Schemas (ValidateSchemaRequest), FieldMappings (SaveFieldMappingItemRequest, PreviewMappingRequest), Templates (SaveHtmlRequest, ValidateHtmlRequest, SaveTemplateDatasetItemRequest, CommitDraftRequest, PreviewDraftRequest, UpdateTemplateMetadataRequest)
│       │   ├── Rendering/          # Documents (GenerateDocumentRequest, PreviewDocumentRequest, ValidatePayloadRequest)
│       │   └── Integration/        # DataConnections, Datasets Request contracts
│       ├── Common/
│       │   ├── Context/            # ExecutionContextImpl.cs (IExecutionContext provider)
│       │   └── Responses/          # ApiResponse<T>.cs, PagedApiResponse<T>.cs (Envelope Pattern)
│       ├── Middleware/
│       │   ├── ApiKeyMiddleware.cs       # X-API-Key validation → ExecutionContext
│       │   └── SecurityHeadersMiddleware.cs
│       ├── Filters/
│       │   ├── ValidateCommandFilter.cs  # Automatic FluentValidation execution
│       │   └── GlobalExceptionFilter.cs  # Domain Exceptions & ValidationException → RFC 7807
│       ├── HealthChecks/           # Postgres, MinIO, Gotenberg health checks
│       └── Program.cs              # DI, Middleware pipeline, Swagger, Rate Limiting
│
└── tests/
    ├── SmkDoc.Tests/                   # Pure In-Memory Unit Test Suite (xUnit + Moq + FluentAssertions — 100% Passing, 0 Failures, Zero I/O)
    │   ├── Common/                     # Dedicated Test Factories (*TestFactory), Builders (*Builder) & Fixtures (*TestFixture)
    │   ├── Domain/                     # Entities, Enums (EnumerationTests), ValueObjects, Exceptions
    │   ├── Application/                # Common Helpers, Modules (CQRS Mirrored 1:1 Parity)
    │   │   └── Modules/                # Mirrored Bounded Context suites (Commands & Queries isolated)
    │   ├── Infrastructure/             # Engines (Html, Word, Excel stream/bytes), Imaging, Security, Schema, Parsing
    │   └── Api/                        # Filters & DependencyInjectionSmokeTests (DI fitness check)
    │
    └── SmkDoc.IntegrationTests/        # Segregated Integration & Benchmark Suite (100% Passing)
        ├── Fixtures/                   # SmkDocApiFactory, PostgreSqlContainerFixture, MinioContainerFixture
        ├── Repositories/               # Real Database / Npgsql Repository integration checks
        ├── Storage/                    # Real Object Storage / MinIO integration checks
        ├── Generators/                 # RealEstateDocuments & MultiPageFixtures document generators
        └── Benchmarks/                 # Performance & 1000-Row stress benchmark suites
```

---

## Frontend (frontend-v2/)

```
frontend-v2/
├── src/
│   ├── tokens/                     # SSoT Design Tokens — ห้าม Hardcode สี/ขนาด
│   ├── schemas/                    # Zod Schemas สำหรับ validate API responses
│   ├── lib/api/                    # API Adapters (fetch wrappers)
│   ├── hooks/                      # Custom Hooks (Business Logic + Data fetching)
│   ├── components/
│   │   ├── ui/                     # Atomic: Button, Dropdown, Badge, Input
│   │   ├── layout/                 # AppShell, Sidebar, Topbar
│   │   └── features/               # Feature blocks: TemplatesView, GeneratorView, ...
│   └── app/                        # Next.js App Router pages
└── ...
```

---

## Docker Orchestration

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
