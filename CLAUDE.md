# CLAUDE.md — SMK Document Server v2
> **อ่านทุกส่วนก่อนเริ่มเขียนโค้ดทุกครั้ง — ไม่มีข้อยกเว้น**

---

## หลักการทำงาน (Working Principles)

### อย่า build เอง — บอกคำสั่งให้ผู้ใช้รัน
AI **ไม่รัน** `docker compose up`, `dotnet run`, `npm run dev` หรือ build scripts เอง  
ให้บอกคำสั่งที่ต้องรัน พร้อมอธิบายว่าทำไม แล้วให้ผู้ใช้ตัดสินใจรันเอง

**ข้อยกเว้นที่รันได้**: `dotnet build` (ตรวจ error), `npx tsc --noEmit` (ตรวจ type), `dotnet test` (รัน test)

AI **รันได้เอง** หลังแก้โค้ดเสร็จทุกครั้ง เพื่อ verify ก่อนรายงานผล:
- `cd frontend-v2 && npx tsc --noEmit` — ต้อง 0 error
- `cd backend-v2 && dotnet build src/SmkDoc.Api/SmkDoc.Api.csproj -v quiet` — ต้อง 0 error

### แก้ไขเฉพาะ v2 เสมอ
| ทำงานใน | ห้ามแก้ |
|---------|---------|
| `backend-v2/` | `backend/` |
| `frontend-v2/` | `frontend/` |
| `docker-compose.v2.yml` | `docker-compose.yml` |

---

## Stack

| Layer     | Tech                    | Notes                                      |
|-----------|-------------------------|--------------------------------------------|
| Backend   | ASP.NET Core .NET 10    | Controller-based — ไม่ใช่ Minimal API      |
| Language  | C# 13 + Nullable refs   | —                                          |
| ORM       | EF Core + Npgsql        | Code-first, ไม่ auto-migrate ใน prod       |
| Auth      | API Key (custom)        | `ApiKeyMiddleware` — ไม่ใช่ JWT/[Authorize] |
| Database  | PostgreSQL 15-alpine    | port 5433                                  |
| PDF       | Gotenberg 8             | REST API — ห้ามใช้ LibreOffice โดยตรง      |
| Storage   | MinIO (self-host)       | S3-compatible, port 9000                   |
| Frontend  | Next.js 15 + React 19   | TypeScript, App Router                     |
| Styling   | Tailwind CSS 3          | class-based + CSS custom properties        |

---

## คำสั่งที่ใช้บ่อย (Commands)

```bash
# Dev — frontend
cd frontend-v2 && npm run dev          # port 3002

# Dev — backend
cd backend-v2 && dotnet run --project src/SmkDoc.Api/SmkDoc.Api.csproj

# Verify — Backend build (ต้อง 0 error)
cd backend-v2 && dotnet build src/SmkDoc.Api/SmkDoc.Api.csproj -v quiet

# Verify — Frontend types (ต้อง 0 error)
cd frontend-v2 && npx tsc --noEmit

# Test — Backend
cd backend-v2 && dotnet test

# Docker — Build + Deploy v2
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build

# Docker — Stop v2 (เก็บ volume)
docker compose -f docker-compose.v2.yml -p smk-v2 down

# Docker — Stop v2 + ลบ volume (ต้องรันเมื่อ DB schema เปลี่ยน)
docker compose -f docker-compose.v2.yml -p smk-v2 down -v

# Docker — Logs
docker compose -f docker-compose.v2.yml -p smk-v2 logs -f

# หรือใช้ npm script จาก root
npm run dev            # frontend-v2
npm run docker:build   # build + up v2
npm run docker:down    # down v2
```

---

## Docker Services (v2)

| Service        | Port    | Container         |
|----------------|---------|-------------------|
| doc-server-v2  | :8080   | smk-doc-server-v2 |
| portal-v2      | :3001   | smk-doc-portal-v2 |
| gotenberg      | :3000   | smk-gotenberg-v2  |
| db (postgres)  | :5433   | smk-postgres-v2   |
| minio          | :9000 / :9001 | smk-minio-v2 |

---

## Design Principles — บังคับใช้ทุก feature

### SOLID

| หลักการ | นำไปใช้อย่างไร |
|---------|--------------|
| **S** — Single Responsibility | UseCase 1 ตัว = 1 workflow เท่านั้น, Controller แค่แปลง HTTP ↔ DTO |
| **O** — Open/Closed | เพิ่ม engine ใหม่ → implement `IRenderEngine` ไม่แก้ switch/if ที่มีอยู่ |
| **L** — Liskov | Concrete ใดๆ ที่ implement Interface ต้องแทนกันได้โดยไม่เสีย contract |
| **I** — Interface Segregation | Interface เล็กๆ เฉพาะงาน — ห้าม god interface |
| **D** — Dependency Inversion | UseCase → inject Interface เสมอ, ห้าม inject Concrete จาก Infrastructure |

### DRY — Don't Repeat Yourself
- regex `{{...}}` → `PlaceholderHelper.cs` เท่านั้น  
- logic apply mapping → `FieldMappingApplicatorService` เท่านั้น  
- API client fetch → `apiClient<T>` / `apiClientBlob` เท่านั้น  
- Zod schema → `schemas/*.schema.ts` เท่านั้น, ห้าม duplicate type ใน component  

### SSOT — Single Source of Truth
- Entity field → Domain layer เท่านั้น  
- Config (URL/port/bucket) → `appsettings.json` + strongly-typed Settings class — ห้าม hardcode  
- Frontend type → `schemas/` → re-export ผ่าน `types/api.ts`  
- NavigationItemId → `Sidebar.tsx` เท่านั้น (AppShell + page.tsx ใช้ import มา)  

### KISS — Keep It Simple, Stupid
- ห้ามเพิ่ม abstraction ที่ไม่มี usecase ตอนนี้  
- ถ้าโค้ดเหมือนกัน 3 ที่ค่อยแยก helper — ไม่ใช่ตั้งแต่ครั้งแรก  
- ไม่ทำ feature flag, backwards-compat shim, หรือ future-proof layer ที่ไม่ถูกขอ  

### HyperUI — UI Block Standard (Frontend)
ทุก UI component / หน้าใหม่ **ต้องอ้างอิง HyperUI blocks** (https://www.hyperui.dev) เป็น baseline  

**กฎการใช้**:
- ใช้ HyperUI block เป็น layout / pattern ตั้งต้น แล้วปรับ class ให้ตรง design token ของโปรเจกต์
- ห้ามประดิษฐ์ layout ขึ้นเองโดยไม่มี reference — ให้หา HyperUI component ที่ใกล้เคียงที่สุดก่อน
- ปรับ color ผ่าน Tailwind class ที่ map กับ CSS variable ของโปรเจกต์ (`bg-surface`, `text-textPrimary`, `border-border`, `text-primary` ฯลฯ)
- Component ที่ HyperUI ไม่มี → ใช้ ui/ primitives ที่มีอยู่ก่อน (`Button`, `CardBlock`, `Modal`, `Table`, `Badge` ฯลฯ) — ไม่สร้าง primitive ใหม่โดยไม่จำเป็น

**ประเภท block ที่ใช้บ่อย**:
| งาน | HyperUI Category |
|-----|----------------|
| หน้า list / grid card | Application UI → Cards |
| Form modal | Application UI → Forms |
| Stats / metric tiles | Application UI → Stats |
| Table + pagination | Application UI → Tables |
| Empty state | Application UI → Empty States |
| Alert / notification | Application UI → Alerts |

---

## Clean Architecture — Layer Rules

```
Domain ← Application ← Infrastructure
                ↑             ↑
              Api (Controller layer)
```

| Layer | รู้จัก | ไม่รู้จัก |
|-------|-------|---------|
| Domain | Entity, Enum, Value Object | EF Core, HTTP, MinIO |
| Application | Interface, UseCase, DTO, Helper | EF Core, OpenXml, HTTP |
| Infrastructure | EF Core, OpenXml, MinIO, Gotenberg | UseCase internals |
| Api | Controller, Middleware, Program.cs | Business logic |

**กฎเหล็ก**: Application ห้าม reference Infrastructure package โดยตรง  
→ ถ้าต้องการ logic ใหม่: สร้าง Interface ใน Application ก่อน แล้ว implement ใน Infrastructure

---

## Checklist ก่อนส่งงาน (Pre-Submit Checklist)

```
GENERAL
☐  แก้ไขใน backend-v2/ และ frontend-v2/ เท่านั้น
☐  Interface ก่อน Implementation (DIP) — ทุกครั้ง
☐  ไม่ duplicate regex / logic / type ที่มี SSoT อยู่แล้ว
☐  Config ทุกค่าอยู่ใน appsettings.json + Settings class

BACKEND
☐  UseCase ไม่รู้จัก HttpContext / IActionResult
☐  Preview endpoints ไม่มี side-effect (ไม่ write DB / Storage)
☐  dotnet build → 0 error, 0 warning ใหม่
☐  dotnet test → ทุก test ผ่าน
☐  แก้ UseCase / Helper / Engine → อัปเดต test file ด้วยเสมอ

FRONTEND
☐  npx tsc --noEmit → 0 error
☐  NavigationItemId ใหม่ → อัปเดต Sidebar + AppShell titles + page.tsx case ครบ
☐  UI ใหม่ → อ้างอิง HyperUI block ก่อน ปรับ class ให้ตรง design token
☐  ไม่สร้าง UI primitive ใหม่ถ้า ui/ มีอยู่แล้ว
☐  ห้าม import schema โดยตรงจาก schemas/ → ใช้ผ่าน types/api.ts
```

> **บอกคำสั่งให้ผู้ใช้รัน** — ไม่รัน build/server/docker เอง

---

## Testing Standards — บังคับทุกครั้งที่แก้โค้ด

> **กฎ**: แก้ UseCase / Helper / Engine → **ต้องแก้ / เพิ่ม test ด้วยเสมอ** ห้ามส่งงานโดยไม่มี test ครอบคลุมโค้ดที่เปลี่ยน

### Backend (xUnit)

| สิ่งที่แก้ | Test ที่ต้องอัปเดต |
|-----------|-----------------|
| UseCase ใหม่ | สร้าง `{UseCaseName}Tests.cs` ใน `tests/SmkDoc.Tests/` |
| แก้ UseCase เดิม | อัปเดต test case ที่เกี่ยวข้อง + เพิ่ม case ใหม่ถ้าเพิ่ม branch |
| แก้ Helper (PlaceholderHelper ฯลฯ) | อัปเดต / เพิ่ม test ใน Helper test file |
| แก้ Engine (Html/Docx/Excel) | อัปเดต Engine test ที่ตรงกัน |

**กฎการเขียน test**:
- Mock ทุก dependency ผ่าน interface — ห้าม instantiate Infrastructure concrete
- ต้องมี: happy path + ทุก error path ที่เพิ่มใน code
- test method name: `MethodName_Condition_ExpectedResult`

```bash
# รัน test ทั้งหมด
cd backend-v2 && dotnet test

# รันเฉพาะ class
cd backend-v2 && dotnet test --filter "FullyQualifiedName~GenerateDocumentUseCaseTests"

# รันพร้อม coverage report
cd backend-v2 && dotnet test --collect:"XPlat Code Coverage"
```

### Frontend (TypeScript)
- `npx tsc --noEmit` → 0 error **ทุกครั้ง** ก่อนส่งงาน  
- Zod schema คือ runtime type guard ที่ API boundary — ห้าม bypass ด้วย `as any`
- แก้ schema → ต้องตรวจทุก component ที่ใช้ type นั้น

```bash
cd frontend-v2 && npx tsc --noEmit
```

---

## backend-v2 — Architecture

```
backend-v2/src/
├── SmkDoc.Domain/
│   ├── Entities/            Template, TemplateVersion, FieldMapping, TemplateDataset,
│   │                        Document, DocumentVersion,
│   │                        ApiKey, GenerationLog,
│   │                        DataConnection, Dataset
│   └── Enums/               RenderEngineType (Html=1, Docx=2, Excel=3), OutputFormat,
│                            TemplateVersionStatus (Draft=0, Published=1, Archived=2)
│
├── SmkDoc.Application/
│   ├── Common/
│   │   ├── Interfaces/      IExecutionContext, IPdfRenderer, IRepository<T>, IStorageService,
│   │   │                    IUnitOfWork, IFieldMappingApplicatorService, ITemplateScannerService,
│   │   │                    ITemplateDraftCache
│   │   ├── Models/          DTOs: GenerateDocumentRequest/Response, TemplateDto, FieldMappingDto,
│   │   │                    TemplateDraftEntry, ParseDraftResult, PreviewDraftRequest, CommitDraftRequest, …
│   │   └── Helpers/         PlaceholderHelper (SSoT regex), ThaiDataTransformer,
│   │                        FieldMappingApplicatorService
│   ├── Engines/             IRenderEngine (Strategy interface)
│   └── UseCases/
│       ├── Documents/       GenerateDocumentUseCase, PreviewDocumentUseCase, DocumentVersionUseCase
│       ├── Templates/       TemplateManagementUseCase, TemplateValidateUseCase,
│       │                    TemplateDraftUseCase (Parse→Preview→Commit pipeline)
│       ├── FieldMappings/   FieldMappingUseCase, PreviewMappingUseCase (zero side-effects)
│       └── Security/        ApiKeyUseCase
│
├── SmkDoc.Infrastructure/
│   ├── Cache/               InMemoryTemplateDraftCache (IMemoryCache, TTL 30 min, 20 MB limit)
│   ├── Engines/             HtmlTemplateEngine, DocxTemplateEngine, ExcelTemplateEngine,
│   │                        TemplateScannerService (scan {{}} placeholders)
│   ├── Pdf/                 GotenbergPdfRenderer
│   ├── Persistence/         AppDbContext, EfRepository<T>, UnitOfWork
│   ├── Storage/             MinioStorageService, MinioSettings
│   └── ExecutionContextImpl
│
└── SmkDoc.Api/
    ├── Controllers/         DocumentController, TemplateController, ApiKeyController, AuditLogController
    ├── Middleware/          ApiKeyMiddleware, SecurityHeadersMiddleware
    ├── HealthChecks/        GotenbergHealthCheck, MinioHealthCheck
    └── Program.cs
```

### DI Registration (Program.cs) — ห้ามเพิ่มนอกนี้โดยไม่อัปเดต

```
IExecutionContext              → ExecutionContextImpl              (Scoped)
IRepository<T>                → EfRepository<T>                   (Scoped)
IUnitOfWork                   → UnitOfWork                        (Scoped)
IStorageService               → MinioStorageService               (Singleton)
IPdfRenderer                  → GotenbergPdfRenderer              (HttpClient typed)
IDataProtectionService        → DataProtectionService             (Singleton)
IDocxSecurityScanner          → DocxSecurityScannerService        (Singleton)
ISqlExecutorService           → SqlExecutorService                (Scoped)
IQrCodeService                → QrCodeService                     (Singleton)
IBarcodeService               → BarcodeService                    (Singleton)
IImageOptimizer               → ImageOptimizerService             (Singleton)
IMathExpressionResolver       → MathExpressionResolverService     (Singleton)
IFieldMappingApplicatorService → FieldMappingApplicatorService    (Scoped)
ITemplateScannerService        → TemplateScannerService           (Singleton)
ITemplateDraftCache           → InMemoryTemplateDraftCache        (Singleton)
IRenderEngine                 → HtmlTemplateEngine                (Scoped, Strategy)
IRenderEngine                 → DocxTemplateEngine                (Scoped, Strategy)
IRenderEngine                 → ExcelTemplateEngine               (Scoped, Strategy)

UseCases (Scoped):
  GenerateDocumentUseCase, PreviewDocumentUseCase, DocumentVersionUseCase,
  RenderStatelessDocumentUseCase, TemplateManagementUseCase, TemplateValidateUseCase,
  TemplateDraftUseCase, FieldMappingUseCase, PreviewMappingUseCase, TemplateDatasetUseCase,
  ApiKeyUseCase, DataConnectionUseCase, DatasetUseCase
```

### Middleware Pipeline

```
SecurityHeaders → HttpsRedirection → CORS → RateLimiter → ApiKeyMiddleware → Controllers
```

Public (ไม่ต้อง API Key): `GET /`, `GET /swagger*`, `GET /health`, `POST /api/documents/preview/{slug}`

---

## backend-v2 — Endpoints

| Method | Route | UseCase |
|--------|-------|---------|
| POST | `/api/documents/generate/{slug}` | GenerateDocumentUseCase |
| POST | `/api/documents/preview/{slug}` | PreviewDocumentUseCase |
| GET | `/api/documents/{ref}/versions` | DocumentVersionUseCase |
| GET | `/api/documents/{ref}/versions/{v}/download` | DocumentVersionUseCase |
| GET | `/api/documents/download/{logId:guid}` | DocumentVersionUseCase |
| GET | `/api/templates` | TemplateManagementUseCase |
| POST | `/api/templates` [FromForm] | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/html` | TemplateManagementUseCase |
| PUT | `/api/templates/{id:guid}/html` | TemplateManagementUseCase |
| PUT | `/api/templates/{id:guid}` | TemplateManagementUseCase |
| DELETE | `/api/templates/{id:guid}` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/mappings` | FieldMappingUseCase |
| PUT | `/api/templates/{id:guid}/mappings` | FieldMappingUseCase |
| POST | `/api/templates/{id:guid}/mappings/preview` | PreviewMappingUseCase (zero side-effects) |
| POST | `/api/templates/{id:guid}/validate` | TemplateValidateUseCase |
| GET | `/api/templates/{id:guid}/versions` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/download` | TemplateManagementUseCase |
| POST | `/api/templates/{id:guid}/rollback/{v:int}` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/scan-fields` | TemplateManagementUseCase → ITemplateScannerService |
| POST | `/api/templates/scan-fields` [FormFile] | TemplateManagementUseCase (stateless — no DB write) |
| POST | `/api/templates/draft/parse` [FormFile] | TemplateDraftUseCase.ParseAsync (RAM cache, no DB) |
| POST | `/api/templates/draft/{id}/preview` | TemplateDraftUseCase.PreviewAsync (zero side-effects; 410 if expired) |
| POST | `/api/templates/draft/{id}/commit` | TemplateDraftUseCase.CommitAsync (atomic MinIO+DB; 410 if expired) |
| GET | `/api/api-keys` | ApiKeyUseCase |
| POST | `/api/api-keys` | ApiKeyUseCase |
| DELETE | `/api/api-keys/{id:guid}` | ApiKeyUseCase |
| GET | `/api/logs` | AuditLogController |
| POST | `/api/documents/render/stateless` | RenderStatelessDocumentUseCase |
| GET | `/health` | HealthChecks |

---

## frontend-v2 — Structure

```
frontend-v2/src/
├── app/
│   └── page.tsx                   Router — switch activeTab → view component
├── components/
│   ├── layout/
│   │   ├── AppShell.tsx           titles Record<NavigationItemId> + searchItems
│   │   ├── Sidebar.tsx            NavigationItemId (SSoT) + navSections
│   │   └── Topbar.tsx
│   ├── ui/                        Design system primitives (ห้ามสร้าง UI ใหม่โดยไม่ตรวจที่นี่ก่อน)
│   │   ├── Badge.tsx, Button.tsx, CardBlock.tsx, CodeBlock.tsx
│   │   ├── Dropdown.tsx, EmptyState.tsx, Input.tsx, Modal.tsx
│   │   ├── Pagination.tsx, Select.tsx, StatBlock.tsx, Table.tsx
│   │   ├── Tabs.tsx, Toast.tsx, Toolbar.tsx
│   └── features/
│       ├── templates/
│       │   ├── TemplatesView.tsx      จัดการ template list + create modal
│       │   └── UploadTemplateView.tsx 2-step: upload → scan {{}} → mapping table + PDF preview
│       ├── generator/             GeneratorView.tsx
│       ├── audit/                 AuditView.tsx
│       ├── logs/                  LogsView.tsx
│       ├── studio/                TemplateStudioView.tsx
│       ├── mappings/              FieldMappingView.tsx
│       ├── apikeys/               ApiKeysView.tsx
│       ├── apidocs/               ApiDocsView.tsx
│       ├── datasources/           DatasourcesView.tsx
│       └── settings/              SettingsView.tsx
├── lib/api/
│   ├── client.ts                  apiClient<T> + apiClientBlob (SSoT fetch wrapper)
│   ├── templates.api.ts           + scanTemplateFields, previewMappings
│   ├── documents.api.ts
│   ├── apikeys.api.ts
│   └── logs.api.ts
├── schemas/                       Zod schemas — SSoT for all API types
│   ├── template.schema.ts, document.schema.ts, mapping.schema.ts
│   ├── apikey.schema.ts, log.schema.ts
├── types/api.ts                   re-exports ทุก schema (ห้าม import จาก schemas/ โดยตรงใน component)
├── hooks/                         useTemplates, useDebounce, …
└── tokens/                        Design tokens + DocumentCategory
```

### NavigationItemId (Sidebar.tsx — SSoT)

ทุกครั้งที่เพิ่ม id ใหม่ **ต้องอัปเดต 3 ที่พร้อมกัน**:

| ไฟล์ | สิ่งที่ต้องเพิ่ม |
|------|---------------|
| `Sidebar.tsx` | เพิ่มใน union type + navSections |
| `AppShell.tsx` | เพิ่มใน `titles` Record + `searchItems` array |
| `app/page.tsx` | เพิ่ม `case` ใน switch |

### API Client Rules

- Base URL: `NEXT_PUBLIC_API_URL \|\| 'http://localhost:8080'`
- Auth: `X-API-Key` จาก `localStorage('smk_api_key')`
- JSON response → `apiClient<T>`
- Binary (PDF/file) → `apiClientBlob`
- 204 → return `{}` as T

---

## Critical Patterns — ตัวอย่างถูก/ผิด

### Strategy Pattern (OCP)
```csharp
// ✅ ถูก
var engine = _engines.Single(e => e.EngineType == template.RenderEngineType);
await engine.ProcessAsync(context);

// ❌ ผิด — แก้ทุกครั้งที่เพิ่ม engine
if (template.RenderEngineType == RenderEngineType.Html) { ... }
else if (template.RenderEngineType == RenderEngineType.Docx) { ... }
```

### Interface First (DIP)
```csharp
// ✅ ถูก — Application layer
public class TemplateManagementUseCase(ITemplateScannerService scanner) { }

// ❌ ผิด — inject Infrastructure concrete
public class TemplateManagementUseCase(TemplateScannerService scanner) { }
```

### UseCase ไม่รู้จัก HTTP
```csharp
// ✅ ถูก
public async Task<TemplateDto> CreateTemplateAsync(CreateTemplateRequest req, CancellationToken ct)

// ❌ ผิด
public async Task<IActionResult> CreateTemplate(IFormFile file)
```

### Frontend — ห้าม duplicate type
```ts
// ✅ ถูก
import { TemplateDto } from '@/types/api';

// ❌ ผิด — สร้าง interface ซ้ำใน component
interface Template { id: string; name: string; ... }
```

---

## Anti-Patterns (ห้ามทำ)

- `ITemplateScannerService` ใน Application รู้จัก `DocumentFormat` / `ClosedXML` โดยตรง
- `switch` / `if-else` บน `RenderEngineType` ใน UseCase
- hardcode URL, bucket name, port ใน source code — ใช้ `StorageBuckets` constants และ strongly-typed Settings classes
- `UploadAsync()` หรือ write GenerationLog ใน Preview endpoint
- สร้าง regex `{{...}}` นอก `PlaceholderHelper.cs`
- Import จาก `schemas/*.schema.ts` โดยตรงใน component (ใช้ผ่าน `types/api.ts`)
- เพิ่ม `NavigationItemId` แค่ที่เดียวโดยไม่อัปเดตอีก 2 ที่
- อ่าน `template.StorageKey` หรือ `template.Version` โดยตรง — field เหล่านี้ถูกลบแล้ว ต้องโหลดผ่าน `template.CurrentVersionId` → `IRepository<TemplateVersion>.GetByIdAsync`
- เก็บ `OutputKey`, `InputData`, `OutputFormat` ใน `DocumentVersion` — ข้อมูลเหล่านี้อยู่ใน `GenerationLog` แล้ว ให้ link ผ่าน `GenerationLogId`
- สร้าง `DocumentVersion` โดยไม่มี `Document` anchor entity — ต้องหรือสร้าง `Document` ก่อนเสมอ

---

## Pending Items

| งาน | Priority | หมายเหตุ |
|-----|----------|---------|
| `GET /api/logs/metrics` | P2 | Aggregate stats สำหรับ LogsView stats row |
| EF Core Migration + `MigrateAsync` | P1 | สร้าง migration สำหรับ Wave 1 schema แล้วเปลี่ยน Program.cs จาก `EnsureCreatedAsync` → `MigrateAsync` |
| AppDbContext: DbSet`<TemplateDataset>` ลงทะเบียน DI ใน Program.cs | P1 | `IRepository<Document>` ต้องลงทะเบียนให้ GenerateDocumentUseCase ใช้ได้ |
| Frontend schema update | P1 | `TemplateDto` เปลี่ยนจาก `version`+`storageKey` → `currentVersionId` / `DocumentVersionDto` เปลี่ยน shape |
| DB Redesign Wave 2 | P3 | users table, RBAC roles, api_keys.created_by FK |

---

## Reference Docs

| Task | อ่านที่ |
|------|---------|
| Sprint plan | `docs/AI/IMPLEMENTATION_PLAN.md` |
| Core standards | `AGENTS.md` |
| Controller pattern | `docs/AI/patterns/controller.md` |
| UseCase / Service pattern | `docs/AI/patterns/service.md` |
| Entity + Migration | `docs/AI/patterns/entity.md` |
| Frontend component | `docs/AI/patterns/frontend-component.md` |
| Anti-patterns | `docs/AI/ANTI-PATTERNS.md` |
| Architecture | `docs/AI/ARCHITECTURE.md` |
| Design tokens | `docs/AI/DESIGN.md` |

---

## v1 Legacy (backend/ + frontend/)

ยังไม่ลบ — เก็บไว้เปรียบเทียบ  
สิ่งที่ v1 มีแต่ v2 ยังไม่ port: ReportBro engine, Math expression resolver, SQL DataSource, DataProtection (AES)
