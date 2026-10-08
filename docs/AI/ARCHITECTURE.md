# ARCHITECTURE.md — System Map & Clean Architecture Blueprint

> **Purpose:** Authoritative system topology, Clean Architecture layering rules, and dual-channel authentication flow.  
> **Related Docs:** [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) (Folder map), [DB_SCHEMA.md](DB_SCHEMA.md) (Data models).

### ⚡ Quick-Lookup: Layer Responsibilities & Dependencies

| Layer | Project | Allowed References | Prohibited Elements | Return Type |
|---|---|---|---|---|
| **Domain** | `SmkDoc.Domain` | None (BCL only) | No EF Core, OpenXml, ASP.NET, **No DTOs** | Entities, ValueObjects, Enums |
| **Application** | `SmkDoc.Application` | `SmkDoc.Domain` | No `AppDbContext`, MinIO SDK, Gotenberg, **No API models** | Application DTOs only |
| **Infrastructure** | `SmkDoc.Infrastructure` | `SmkDoc.Application`, `SmkDoc.Domain` | No Controllers, No business logic validation | Internal adapter implementations |
| **Api** | `SmkDoc.Api` | `SmkDoc.Application` (via DIP) | **No direct Domain Entity access**, No direct DB calls | `ApiResponse<T>` / `ApiPagedResponse<T>` |

---

## 1. System Overview

```
┌──────────────────────────────────────┐     ┌──────────────────────────────────────┐
│  Next.js 15 Portal (frontend-v2/)    │     │  External Systems (M2M)              │
│  React 19 • TypeScript • Tailwind CSS │     │  CRM, ERP, Billing, Core Services   │
│  - Human Users & Designers           │     │  - Automated document generation     │
│  - Template Studio, Generator, Logs  │     │  - Batch reporting                   │
└──────────────────┬───────────────────┘     └──────────────────┬───────────────────┘
                   │ Bearer JWT                                 │ X-API-Key
                   │ (Optional X-Project-Id)                    │
┌──────────────────▼────────────────────────────────────────────▼───────────────────┐
│  ASP.NET Core 10 API  (backend-v2/src/SmkDoc.Api)                                 │
│  Dual-Channel Authentication:                                                     │
│    ├─ Channel A (M2M): ApiKeyMiddleware → Scope to ApiKey.ProjectId               │
│    └─ Channel B (Human): JwtBearer → SystemRole (SuperAdmin / Member)             │
│  GlobalExceptionFilter → Controllers → UseCases → Domain / Infrastructure        │
│  Engine Routing: Strategy Pattern → IRenderEngine                                 │
└────────┬─────────────────────────┬───────────────────────────┬────────────────────┘
         │                         │                           │
    PostgreSQL                   MinIO                    Gotenberg 8
    (EF Core 10)               (self-host)            (Chromium & LibreOffice)
    metadata & users           templates/             HTML → PDF (Chromium)
    mappings & logs            outputs/               DOCX/XLSX → PDF (LibreOffice)
    versions & projects
```

---

## 2. Backend Clean Architecture (backend-v2/)

การแบ่ง Layer ถูกบังคับใช้ผ่าน Project References ใน `SmkDocServerV2.slnx`:

```
SmkDoc.Domain ← SmkDoc.Application ← SmkDoc.Infrastructure ← SmkDoc.Api
```

> **กฎเหล็ก:** แต่ละ Layer อ้างอิง (import) ได้เฉพาะ Layer ที่อยู่ข้างใน (ทางซ้าย) เท่านั้น

---

### Layer 1 — `SmkDoc.Domain` (Core)

**Zero dependencies.** ไม่มี NuGet Package ใดๆ ยกเว้น BCL.

| โฟลเดอร์ | ไฟล์สำคัญ | หน้าที่ |
|---|---|---|
| `Entities/` | `Template.cs`, `TemplateVersion.cs`, `GenerationLog.cs`, `ApiKey.cs`, `Document.cs`, `DocumentVersion.cs`, `FieldMapping.cs`, `User.cs`, `Project.cs`, `Company.cs`, `DataConnection.cs`, `Dataset.cs`, `TemplateDataset.cs`, `UserProjectRole.cs` | **Sealed Rich Domain Models** — `private set`, Private EF Core constructors, Static Factory Methods (`Create`, `Draft`, etc.), Invariant Guards, และ Business Methods |
| `Enums/` | `DatabaseProvider.cs`, `GenerationStatus.cs`, `OutputFormat.cs`, `RenderEngineType.cs`, `RoleType.cs`, `SystemRole.cs`, `TemplateFormat.cs`, `TemplateVersionStatus.cs` | **Smart Enums** — สืบทอดจาก `Enumeration` base class มีคุณสมบัติ, static O(1) dictionary cache, และ implicit string conversion |
| `ValueObjects/` | `ApiKeyName.cs`, `CompanyName.cs`, `ConnectionName.cs`, `DatasetAlias.cs`, `DatasetName.cs`, `DataSourceType.cs`, `DocumentReference.cs`, `EmailAddress.cs`, `ExpirationPolicy.cs`, `ProjectName.cs`, `Sha256Hash.cs`, `TemplateName.cs`, `TemplateSlug.cs`, `Validation/` | Immutable, self-validating Value Objects (structural equality ผ่าน `GetEqualityComponents()`) พร้อม fail-fast invariant validation |
| `Common/` | `BaseEntity.cs`, `Enumeration.cs`, `Guard.cs`, `ValueObject.cs` | Domain primitives — `BaseEntity` (sequential UUIDv7, `CreatedAt`, `UpdatedAt`), `Guard` (fail-fast helper โยน `DomainValidationException`) |
| `Exceptions/` | `DomainException.cs`, `DomainValidationException.cs`, `BusinessRuleViolationException.cs`, Typed Exceptions (`DuplicateVersionException`, `DuplicateDatasetAliasException`, `DuplicatePlaceholderException`, `DuplicateDocumentVersionException`, `VersionNotInTemplateException`, `ArchivedVersionImmutableException`, `UserDeactivatedException`, `ProjectDeactivatedException`) | Pure Domain Exception hierarchy พร้อม machine-readable `ErrorCode` ทุกตัว |
| `Interfaces/` | `I*Repository.cs` (ต่อ Aggregate), `IUnitOfWork.cs`, `IMustHaveProject.cs` | Repository contracts (Entity/VO/primitive เท่านั้น — ห้าม `IQueryable`/DTO), Atomic transactions, Multi-project scoping |

**Rich Domain Model Rules (บังคับ — ดูตัวอย่างใน [PATTERNS.md §1.4](PATTERNS.md) และ ADR-022):**
1. **Pure C#:** ห้าม NuGet / EF Core / Data Annotations (`[Key]`, `[Table]`, `[ForeignKey]`) — Mapping ทั้งหมดอยู่ใน `Infrastructure/Persistence/AppDbContext.cs` ผ่าน Fluent API + `HasConversion`
2. **Encapsulation:** Properties ทั้งหมดเป็น `{ get; private set; }` หรือ `{ get; protected set; }` (บน `BaseEntity.Id`) — ห้าม `public set` หรือ external object initializers (`{ Id = ... }`). Aggregate collections ทั้งหมดเป็น `IReadOnlyCollection<T>` backed by `private readonly List<T>`
3. **Construction (ADR-023 Strict Pure DDD):** สร้างผ่าน **Single Canonical Factory Method (`Create`) เท่านั้น** โดยรับเฉพาะ strongly-typed Value Objects และ deterministic `DateTimeOffset now` · **ห้ามมี Primitive Convenience Overloads** (`(string, string)`) ภายใน Domain Entity (Application UseCases มีหน้าที่ map Primitive จาก DTO สู่ Value Objects) · **ห้ามมี `CreateForTest` ใน Production Domain Model** — การสร้าง entity เพื่อการทดสอบต้องทำผ่าน `*Builder` หรือ `*TestFactory` ในโปรเจกต์ `SmkDoc.Tests` เท่านั้น โดยใช้ `internal` constructor ที่เปิดผ่าน `[assembly: InternalsVisibleTo("SmkDoc.Tests")]` + `private` parameterless ctor สำหรับ EF Core materialization
4. **Fail-Fast Invariants:** Factory และ Business Method ทุกตัวต้อง validate ผ่าน `Guard` ก่อน mutate และโยน `DomainValidationException` (input ผิด) หรือ typed `BusinessRuleViolationException` (ผิดกฎธุรกิจ พร้อม `ErrorCode` เฉพาะ)
5. **State Mutation ผ่าน Business Methods เท่านั้น:** เมธอดต้อง idempotent เมื่อ state ไม่เปลี่ยน และรับ `DateTimeOffset now` เพื่อ deterministic timestamp audit
6. **Aggregate Boundaries:** Child entities แก้ไขผ่าน Aggregate Root เท่านั้น; Collection expose เป็น `IReadOnlyCollection<T>`; อ้างอิงข้าม Aggregate ด้วย **Id** เท่านั้น (ตัด Domain navigation properties ข้าม Aggregate ออก แล้ว map ผ่าน EF Core `HasOne<T>().WithMany().HasForeignKey(...)`)
7. **Value Objects & Converters:** ข้อมูลที่มี domain validation rules (names, aliases, references, emails, hashes) ต้องสร้างเป็น `ValueObject` และ map สู่ Database column เดิมผ่าน `HasConversion` ใน EF Core ทำให้ **Zero Database Migration** 100%

> [!NOTE]
> **Domain Layer Modernization & Zero-Migration DDD (ADR-022 & ADR-023):** Entities ทั้งหมด (Aggregate Roots และ Child Entities) ถูก refactor เป็น Sealed Rich Domain Models ครบถ้วน 100%, Value Objects ครอบคลุมทุก Invariant ของระบบ, Cross-aggregate navigation ถูก decouple ออกจาก Domain, ปฏิบัติตาม Strict Pure DDD ครบทุก Entity (Canonical Factory, Zero Primitive Overloads, Zero Test Backdoors ใน Domain, Mandatory Deterministic Time), รองรับการทดสอบผ่าน Dedicated Test Factories ใน `SmkDoc.Tests/Common/Factories/`, และ 100% ของ Unit/Integration test suites ผ่านทั้งหมด (0 errors, 0 failures).

---

### Layer 2 — `SmkDoc.Application` (Use Cases & Ports)

**Depends only on Domain.** ห้ามอ้างอิง Infrastructure หรือ ASP.NET Core.

| โฟลเดอร์ | ไฟล์สำคัญ | หน้าที่ |
|---|---|---|
| `Common/Interfaces/` | `IRepository<T>`, `IStorageService`, `IPdfRenderer`, `IRenderEngine`, `IExecutionContext`, `IUnitOfWork`, `ICompiledTemplateCache`, `IUserWorkspaceQueryService` | Ports — Abstractions ที่ Infrastructure จะ Implement |
| `Common/Exceptions/` | `NotFoundException.cs`, `ValidationException.cs`, `UnauthorizedException.cs`, `ConflictException.cs`, `DraftExpiredException.cs`, `RenderException.cs`, `SchemaValidationException.cs` | Application-level exceptions ที่ map ไปเป็น RFC 7807 Problem Details |
| `UseCases/Documents/` | `GenerateDocumentUseCase`, `PreviewDocumentUseCase`, `ValidatePayloadUseCase`, `DocumentVersionUseCase`, `RenderStatelessDocumentUseCase`, `HtmlToPdfUseCase` | Document generation pipeline |
| `UseCases/Schemas/` | `ValidateStandaloneSchemaUseCase` | Standalone zero-DB Draft-07 schema validation (Monaco Studio & M2M) |
| `UseCases/Templates/` | `TemplateManagementUseCase`, `HtmlStudioUseCase`, `HtmlPersistenceUseCase`, `TemplateValidateUseCase`, `ValidateTemplatePayloadUseCase`, `TemplateDraftUseCase` | Template CRUD, HTML Studio, และ Payload Validation |
| `UseCases/FieldMappings/` | `FieldMappingUseCase`, `PreviewMappingUseCase` | Field Mapping และ Preview |
| `UseCases/Security/` | `ApiKeyUseCase`, `LoginUseCase`, `UserManagementUseCase` | Auth & User management |
| `UseCases/Datasets/` | `DatasetUseCase`, `DataConnectionUseCase`, `TemplateDatasetUseCase` | External data sources |
| `DTOs/` | `Documents/`, `Templates/`, `Schemas/`, `FieldMappings/`, `Datasets/`, `DataConnections/`, `Security/`, `Users/`, `Projects/`, `Logs/` | **Application DTOs & Commands** (100% Immutable records — ห้ามใส่ API Models ที่นี่) |
| `Validators/` | `Documents/`, `Templates/`, `Security/` | **FluentValidation Command Validators** (ตรวจ format, types, lengths, regex slugs) |
| `Engines/` | `IRenderEngineResolver` | Strategy Pattern resolver |

**Use Case Rule:** Use Cases ต้องรับ Commands/Queries และคืนค่าเป็น **Application DTOs เท่านั้น** (ห้ามคืน Domain Entities ออกไปสู่ Controller หรือ Middleware)

---

### Layer 3 — `SmkDoc.Infrastructure` (Adapters)

**Implements Application Ports.** Depends on Domain + Application.

| โฟลเดอร์ | ไฟล์สำคัญ | หน้าที่ |
|---|---|---|
| `Persistence/` | `AppDbContext.cs`, `EfRepository<T>.cs`, `UnitOfWork.cs`, `GenerationLogMetricsRepository.cs`, `Repositories/`, `Queries/UserWorkspaceQueryService.cs` | EF Core + Npgsql, Repositories, Single-SQL Query Projections |
| `Storage/` | `MinioStorageService.cs` | MinIO S3 Adapter (`templates/`, `outputs/` buckets) |
| `Pdf/` | `GotenbergPdfRenderer.cs` | HTTP Client สำหรับ Gotenberg 8 (Chromium + LibreOffice) |
| `Engines/Html/` | `HtmlTemplateEngine.cs`, `HtmlHelperRegistry.cs`, `HtmlPlaceholderTransformer.cs`, `HtmlLayoutProcessor.cs` | Handlebars-based HTML template processing (พร้อม SHA-256 AST caching) |
| `Engines/Word/` | `DocxTemplateEngine.cs`, `WordTextReplacer.cs`, `WordTableExpander.cs`, `WordMediaInjector.cs` | OpenXML Pipeline (Composable steps) |
| `Engines/Excel/` | `ExcelTemplateEngine.cs`, `ExcelTableExpander.cs`, `ExcelMediaInjector.cs` | ClosedXML A4-fit Excel engine (เคารพ PageOrientation & Fit) |
| `Engines/` | `TemplateScannerService.cs` | Placeholder scanning สำหรับทุก format |
| `Security/` | `BcryptPasswordHasher.cs`, `JwtTokenGenerator.cs`, `DataProtectionService.cs`, `DocxSecurityScannerService.cs` | Security adapters |
| `Contexts/` | `ExecutionContextImpl.cs` | Implements `IExecutionContext` (request-scoped) |
| `Schema/` | `JsonSchemaValidationService.cs`, `SchemaInferenceService.cs` | JSON Schema Draft-07 validation (Bounded `IMemoryCache` + SHA-256 hash keys + JsonElement overloads) |
| `Cache/` | `InMemoryTemplateDraftCache.cs`, `MemoryCompiledTemplateCache.cs` | In-memory draft & compiled Handlebars template cache |

---

### Layer 4 — `SmkDoc.Api` (Presentation)

**HTTP surface only.** Depends on Application + Infrastructure (DI only in Program.cs).

| โฟลเดอร์ | ไฟล์สำคัญ | หน้าที่ |
|---|---|---|
| `Controllers/` | `DocumentController`, `TemplateController`, `TemplateVersionController`, `TemplateHtmlController`, `TemplateMappingController`, `TemplateScanController`, `TemplateDraftController`, `ApiKeyController`, `ApiKeyManagementController`, `AuthController`, `UserManagementController`, `ProjectManagementController`, `AuditLogController`, `DataConnectionsController`, `DatasetController`, `FontManagementController` | Thin Controllers — รับ Request, เรียก UseCase, return `ApiResponse<T>` |
| `Filters/` | `ValidateCommandFilter.cs`, `GlobalExceptionFilter.cs` | Automatic FluentValidation execution & RFC 7807 Exception Mapping |
| `Contracts/` | `Contracts/IdentityAccess/`, `Contracts/Authoring/` | Feature-based HTTP Request & Response contracts (Decoupled positional records) |
| `Common/` | `Common/Context/ExecutionContextImpl.cs`, `Common/Responses/ApiResponse.cs` | Presentation Context Provider & Global Response Envelopes (`ApiResponse<T>`, `PagedApiResponse<T>`) |
| `Program.cs` | — | DI Container, Middleware pipeline, Swagger, Rate Limiting |

**Controller Rule:** Controllers ต้องไม่มี Business Logic, ห้าม Inject `IRepository<T>` โดยตรง

---

## 3. Request Pipeline (ลำดับการทำงาน)

```
HTTP Request
  → SecurityHeadersMiddleware   (HSTS, X-Frame-Options ฯลฯ)
  → Dual-Channel Auth:
      ├─ M2M Channel & Portal Auth Gateway:
      │    ApiKeyMiddleware     (Validate X-API-Key → Bind ApiKey.ProjectId & Scope to IExecutionContext)
      │    RateLimiter          (60 req/min per API key)
      └─ Portal Authenticated Session (Post-Login):
           JwtBearer Auth       (Validate Bearer JWT → Extract SystemRole, UserId, Role & ProjectId)
           ApiKey Bypass        (Authenticated Bearer sessions bypass X-API-Key check automatically)
  → Controller Action           (Thin — no business logic)
  → GlobalExceptionFilter       (Catch Domain Exceptions → RFC 7807)
  → UseCase                    (Business Logic, returns DTO)
  → Infrastructure Port         (IRepository / IStorageService / IRenderEngine)
  → ApiResponse<T>             (Wraps DTO as HTTP 200)
```

---

## 4. Template Engine Strategy (Strategy Pattern)

```
Template.FileFormat (TemplateFormat smart enum)
  ↓
RenderEngineType (Html / Docx / Excel)
  ↓ resolved by IEnumerable<IRenderEngine> + EngineType matching
  ├── HtmlTemplateEngine   → Handlebars merge → Gotenberg Chromium → PDF
  ├── DocxTemplateEngine   → OpenXML Pipeline → direct DOCX / Gotenberg LibreOffice → PDF
  └── ExcelTemplateEngine  → ClosedXML → direct XLSX / Gotenberg LibreOffice → PDF
```

**ห้ามใช้** `if/else` หรือ `switch` บน engine type ใน orchestration layer ใดๆ ให้ใช้ `engines.First(e => e.EngineType == type)` แทน

---

## 5. Datasources & Datasets Layer

ระบบรองรับการดึงข้อมูลจาก External Database เพื่อ Merge ใน Template:

1. **Data Connections** (`data_connections`): PostgreSQL / SqlServer / Oracle — เก็บ `EncryptedConnectionString` ด้วย AES-256
2. **Datasets** (`datasets`): SQL Query + Parameters + `CacheSeconds`
3. **Template Datasets** (`template_datasets`): Many-to-Many ระหว่าง Template และ Dataset พร้อม `Alias` (เช่น `orders`)

---

## 6. Testing Architecture

**Quality Baseline: 100% Pass Rate on all Backend & Frontend Test Suites (Zero Tolerated Failures)**

| Suite | ไฟล์ตัวอย่าง | ครอบคลุม |
|---|---|---|
| Domain Logic | `TemplateVersionTests.cs`, `EnumerationTests.cs`, `BaseEntityTests.cs` | Rich Domain, Smart Enums O(1) Cache & TryFromExtension, UUIDv7, Thai formatting |
| Use Cases | `GenerateDocumentUseCaseTests.cs`, `LoginUseCaseTests.cs`, `RenderStatelessDocumentUseCaseTests.cs` | Application orchestration, Hybrid RBAC, Dual-Channel Auth, Sequential UUIDv7 verification |
| Engines & Cache | `HtmlTemplateEngineTests.cs`, `DocxTemplateEngineTests.cs`, `ExcelTemplateEngineTests.cs`, `MemoryCompiledTemplateCacheTests.cs` | Template rendering pipelines, Handlebars AST caching, Excel PageSetup preservation |
| Performance | `PerformanceBenchmarkTests.cs` | 1,000-row Excel/Word/HTML benchmarks + 50-request concurrency test |
| Security | `DataProtectionServiceTests.cs`, `DocxSecurityScannerServiceTests.cs` | AES-256 roundtrip, DOCX scan |

**Guarantees:**
- ✅ Word Engine: `DrawingML Id > 0` (prevents Word Desktop crash)
- ✅ Preview: Zero side-effects (no MinIO write, no DB log)
- ✅ Thai Baht Text: Negative amounts, zero, สตางค์ — ทดสอบ 7 cases
- ✅ Phone formatting: 9-digit (`02-xxx-xxxx`) and 10-digit (`081-xxx-xxxx`) coverage
- ✅ Handlebars Cache: Zero redundant AST compilation on repeated template runs
- ✅ Sequential UUIDv7: 100% timestamp-ordered indexing without B-tree page splits
