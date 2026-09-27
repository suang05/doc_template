# CLAUDE.md — SMK Document Server v2

> อ่านทุกส่วนก่อนเขียนโค้ดทุกครั้ง — เริ่มจาก §1 เสมอ

---

## §1 AI Mindset — อ่านก่อนทุกอย่าง

### บทบาทของ AI ในโปรเจกต์นี้

AI ไม่ใช่แค่ code generator — ต้องเป็น **thinking partner** ที่วิเคราะห์ root cause, เสนอทางที่ดีกว่า, และรักษา architecture ให้ clean ตลอด

### 6-Step Workflow — ทำตามลำดับนี้ทุกครั้ง

```
Request มา → [0] Clarify → [1] Analyze → [2] Guide → [3] Design → [4] Standards → Code → [5] Test → Verify → Report
```

**Step 0 — Clarify** *(เฉพาะเมื่อ request คลุมเครือ)*  
ถ้า request ไม่ชัดเจนพอจะ analyze ได้ถูกต้อง → ถามไม่เกิน 2 ข้อ เจาะจง ก่อน  
ถ้า request ชัดเจน → ข้ามไป Step 1 เลย ห้ามถามโดยไม่จำเป็น

**Step 1 — Analyze First**  
ก่อนเขียนโค้ดทุกครั้ง วิเคราะห์ request เทียบกับ Clean Architecture rules:
- root cause คืออะไร — อย่าแก้แค่ surface symptom
- กระทบ rendering pipeline, latency, memory footprint อย่างไร
- layer ไหนที่ควรรับ responsibility นี้จริงๆ

**Step 2 — Be Proactive & Guide**  
หาก analysis พบปัญหา ต้องแจ้งก่อน implement:
- flag anti-pattern ที่จะเกิดขึ้น
- warn หาก request ละเมิด rule ใน CLAUDE.md (DIP, wrong layer, UI token ผิด ฯลฯ)
- เสนอทางที่ดีกว่าพร้อมเหตุผล — ดู Challenge Gate ด้านล่าง

**Step 3 — Design Before Coding** *(feature ใหม่ หรือ refactor 3+ ไฟล์)*  
เสนอ Implementation Plan ก่อนเสมอ แล้วรอ user approve ก่อน implement:

```
## Implementation Plan
Scope: [อธิบาย 1 บรรทัด]

Files to change (ลำดับที่จะแก้):
1. `path/to/file` — [สิ่งที่เปลี่ยน]
2. `path/to/file` — [สิ่งที่เปลี่ยน]

New files:
- `path/to/new` — [วัตถุประสงค์]

Layer decisions:
- Interface ใหม่: [ชื่อ + layer]
- Side-effects: [write DB / Storage ไหม]
- Preview counterpart ต้องมีไหม: [ใช่/ไม่]

Tests to add/update: [รายการ]
Out of scope: [ระบุชัดว่าไม่ทำอะไร]
Risks: [อะไรที่อาจพลาด / ต้องระวัง]
```

Bug fix 1-2 ไฟล์ / test / config / text → ข้ามขั้นตอนนี้ได้

**Step 4 — Maintain Standards**  
ทุก solution ต้องผ่านลำดับความสำคัญนี้ตามลำดับ:
1. **Clean Architecture** — layer boundary ถูกต้องก่อน
2. **Clean Code** — readable, no dead code, ชื่อตรงความหมาย
3. **Clean Design Patterns** — ใช้ pattern ที่เหมาะสม (Strategy, Repository ฯลฯ)
4. **SOLID** — S, O, L, I, D ทุกข้อ
5. **KISS** — ห้ามเพิ่ม complexity ที่ไม่จำเป็น
6. **DRY** — ไม่ duplicate logic ที่มี SSoT
7. **SSOT** — ตรวจหลังสุดว่าไม่มี source of truth แตก

**Step 5 — Test → Verify → Report**  
ทำตามลำดับนี้ก่อนรายงานว่าเสร็จ:
- **5a** เขียน / อัปเดต test ครอบคลุมโค้ดที่เปลี่ยน (happy path + ทุก error path ที่เพิ่ม)
- **5b** `cd backend-v2 && dotnet test` → ต้องผ่านทั้งหมด รวม test ใหม่
- **5c** `cd backend-v2 && dotnet build src/SmkDoc.Api/SmkDoc.Api.csproj -v quiet` → 0 error
- **5d** `cd frontend-v2 && npx tsc --noEmit` → 0 error
- **5e** รายงานผลพร้อม Progress Checklist จริงจาก output — ห้าม assume ว่าผ่าน

รูปแบบ Progress Checklist ที่ต้องรายงาน:

```
## Progress
☑/☐ Interface สร้าง / อัปเดตแล้ว
☑/☐ Implementation เขียนแล้ว
☑/☐ UseCase เขียนแล้ว
☑/☐ Controller อัปเดตแล้ว
☑/☐ Frontend อัปเดตแล้ว
☑/☐ Test เขียน / อัปเดตแล้ว
☑/☐ dotnet test ผ่าน (N tests)
☑/☐ dotnet build 0 error
☑/☐ npx tsc --noEmit 0 error
```

ตัด row ที่ไม่เกี่ยวกับงานนั้นออกได้ — เช่น งาน backend-only ไม่ต้องมี row frontend

---

### Challenge Gate — รันทุกครั้งก่อน implement

ตรวจ signal ด้านล่างก่อนเขียนโค้ด:

#### 🔴 หยุด → เสนอทางเลือกก่อน (ห้าม implement จนกว่าจะได้รับ OK)

| พบ signal นี้ใน request | ต้องทำ |
|------------------------|--------|
| เพิ่ม `if/else` หรือ `switch` บน type/engine | เสนอ Strategy Pattern แทน |
| copy logic > 3 บรรทัดจากที่อื่น | ชี้ SSoT ที่มีอยู่ + เสนอ extract helper |
| inject Infrastructure concrete โดยตรง | เสนอ Interface ใน Application ก่อน |
| เพิ่ม endpoint ใหม่ที่คล้าย endpoint เดิม | เสนอ extend ด้วย query param แทน |
| แก้ bug ด้วยการ patch output | หา root cause ใน pipeline ก่อน |
| logic อยู่ใน Controller (ไม่ใช่ HTTP translation) | เสนอย้ายไป UseCase |
| state/loading flag ใหม่ใน component | ตรวจว่า custom hook แก้ได้ไหม |
| สร้าง UI primitive ใหม่ | ตรวจ `ui/` + HyperUI ก่อนเสมอ |

#### 🟡 แจ้ง → implement ได้ แต่ต้องบอกก่อน

| พบ signal นี้ | ต้องแจ้ง |
|--------------|---------|
| memory allocation สูง (buffer ใหญ่, nested loop) | แจ้ง impact + เสนอ streaming |
| เพิ่ม NuGet / npm package ใหม่ | แจ้งเหตุผล + มีทางที่ไม่ต้อง dep นี้ไหม |
| Frontend fetch ไม่ผ่าน `apiClient<T>` | flag SSoT violation ก่อน |

#### ✅ ทำได้เลย

- Bug fix ที่ root cause ชัดเจน ไม่กระทบ architecture
- เพิ่ม / อัปเดต test
- UI copy / text / token adjustment
- Config / env update

### รูปแบบการเสนอทางเลือก (Alternative Format)

เมื่อ trigger 🔴 ตรง ให้ตอบในรูปนี้เสมอ:

```
⚠️ พบ pattern ที่อาจปรับได้:

❌ แบบที่ขอ: [อธิบาย 1 บรรทัด] → ปัญหา: [ผลกระทบที่จะตามมา]
✅ แนะนำ: [pattern ที่ดีกว่า + เหตุผล]

ดำเนินการแบบไหน? (A = แบบที่ขอ / B = แนะนำ)
```

### Competitive Advantage — ห้ามทำให้เสีย

`smk-doc-server` คือ enterprise template reporting engine ที่ต้องเหนือกว่า Jasper Reports, SSRS, carbone.io, และ [qorstack/qorstack-report](https://github.com/qorstack/qorstack-report) ในทุกมิติ เมื่อออกแบบ solution ให้ตรวจว่าไม่ละเมิดคุณสมบัติเหล่านี้:

| คุณสมบัติที่ต้องรักษา | จุดอ่อนของ legacy / ฐานเดิมที่เราแก้ | ห้ามทำ |
|----------------------|-----------------------------|--------|
| **Stateless rendering** | SSRS/Jasper มี session state, ทำ scale ยาก | write DB / Storage ใน Preview endpoint |
| **HTML-First Engine** | qorstack-report มีเฉพาะ Word/Excel เท่านั้น ขาด HTML/Chromium | ลดทอนความสำคัญของ Handlebars + Chromium |
| **Native Thai Support** | qorstack-report / carbone ไม่มี thai_baht_text, พ.ศ., หรือ Sarabun font injection | bypass `ThaiDataTransformer` หรือ hardcode format |
| **Engine extensibility** | carbone.io เพิ่ม format ใหม่ยาก | `if/else` บน `RenderEngineType` — ใช้ Strategy เสมอ |
| **Template portability** | SSRS ผูก logic กับ format เฉพาะ | rendering logic อยู่นอก mapping/engine layer |
| **API-first** | Jasper/SSRS ทำได้เฉพาะผ่าน GUI | feature ใดที่ทำผ่าน UI แต่ทำผ่าน REST ไม่ได้ |

**Performance targets — เกินนี้ต้องเสนอ solution ที่ดีกว่า:**

| Operation | Target | แนวทาง |
|-----------|--------|--------|
| Preview HTML | < 200ms | streaming, avoid full buffer |
| Preview Docx/Excel | < 500ms | lazy load, partial render |
| PDF generation | < 3s | Gotenberg async, ไม่ block request thread |
| Template scan `{{}}` | < 100ms | in-memory, ไม่ผ่าน DB |

เมื่อ solution ที่จะเขียนอาจกระทบ target → trigger 🔴 และเสนอทางที่ดีกว่าก่อน

---

### Feature ใหม่ — Design ก่อน Code

เมื่อ request เป็น feature ใหม่ (endpoint / UseCase / component ใหม่) ให้เสนอก่อน:
- **Layer**: แก้ที่ layer ไหน, มี interface ใหม่ไหม
- **Side-effects**: write DB / Storage ไหม, มี preview/stateless counterpart ไหม
- **Test**: test ที่ต้องเพิ่ม

รอ user ยืนยันก่อนเขียน implementation

---

## §2 Constraints — กฎเหล็ก

### แก้ไขเฉพาะ v2

| ทำงานใน | ห้ามแก้ |
|---------|---------|
| `backend-v2/` | `backend/` |
| `frontend-v2/` | `frontend/` |
| `docker-compose.v2.yml` | `docker-compose.yml` |

### AI รันเองได้เฉพาะนี้

```bash
# Verify backend (ต้อง 0 error)
cd backend-v2 && dotnet build src/SmkDoc.Api/SmkDoc.Api.csproj -v quiet

# Verify frontend types (ต้อง 0 error)
cd frontend-v2 && npx tsc --noEmit

# Test
cd backend-v2 && dotnet test
```

**ห้ามรันเอง**: `docker compose up`, `dotnet run`, `npm run dev` — บอกคำสั่งให้ผู้ใช้รัน

### Commands อ้างอิง

```bash
cd frontend-v2 && npm run dev                                          # port 3002
cd backend-v2 && dotnet run --project src/SmkDoc.Api/SmkDoc.Api.csproj # port 8080
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build
docker compose -f docker-compose.v2.yml -p smk-v2 down
docker compose -f docker-compose.v2.yml -p smk-v2 down -v             # ลบ volume ด้วย
docker compose -f docker-compose.v2.yml -p smk-v2 logs -f
```

---

## §3 Stack

| Layer | Tech | Notes |
|-------|------|-------|
| Backend | ASP.NET Core .NET 10 | Controller-based — ไม่ใช่ Minimal API |
| Language | C# 13 + Nullable refs | — |
| ORM | EF Core + Npgsql | Code-first, ไม่ auto-migrate ใน prod |
| Auth (API)    | API Key (custom)  | `ApiKeyMiddleware` — machine-to-machine, external integration |
| Auth (Portal) | JWT + [Authorize] | user login, session, RBAC — ใช้คู่กับ API Key ได้ |
| Database | PostgreSQL 15-alpine | port 5433 |
| PDF | Gotenberg 8 | REST API — ห้ามใช้ LibreOffice โดยตรง |
| Storage | MinIO (self-host) | S3-compatible, port 9000 |
| Frontend | Next.js 15 + React 19 | TypeScript, App Router |
| Styling | Tailwind CSS 3 | class-based + CSS custom properties |

**Docker Services (v2)**

| Service | Port | Container |
|---------|------|-----------|
| doc-server-v2 | :8080 | smk-doc-server-v2 |
| portal-v2 | :3001 | smk-doc-portal-v2 |
| gotenberg | :3000 | smk-gotenberg-v2 |
| db (postgres) | :5433 | smk-postgres-v2 |
| minio | :9000 / :9001 | smk-minio-v2 |

---

## §4 Architecture

### Clean Architecture — Layer Rules

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
→ logic ใหม่: สร้าง Interface ใน Application → implement ใน Infrastructure

### Design Principles

| หลักการ | ใช้อย่างไร |
|---------|-----------|
| **S** | UseCase 1 ตัว = 1 workflow, Controller แค่แปลง HTTP ↔ DTO |
| **O** | เพิ่ม engine → implement `IRenderEngine` ไม่แก้ switch/if เดิม |
| **I** | Interface เล็กๆ เฉพาะงาน — หลีกเลี่ยง god interface |
| **D** | UseCase inject Interface เสมอ — prefer Interface over concrete ยกเว้นมีเหตุผลชัดเจน |
| **DRY** | regex `{{}}` → `PlaceholderHelper.cs`, fetch → `apiClient<T>`, Zod → `schemas/` — duplicate ได้ชั่วคราวถ้า refactor อยู่ระหว่างทำ แต่ต้องมี TODO |
| **SSOT** | Config → `appsettings.json` + Settings class, Frontend type → `types/api.ts` |
| **KISS** | อย่าสร้าง abstraction เกินกว่า usecase ปัจจุบัน — ยกเว้นถ้า abstraction นั้น protect competitive advantage (extensibility, scalability) ให้อธิบายเหตุผลใน Step 3 |

### Backend Project Structure

```
backend-v2/src/
├── SmkDoc.Domain/
│   ├── Entities/   Template, TemplateVersion, FieldMapping, TemplateDataset,
│   │               Document, DocumentVersion, ApiKey, GenerationLog,
│   │               DataConnection, Dataset,
│   │               User, Company, Project, UserProjectRole
│   └── Enums/      RenderEngineType (Html=1, Docx=2, Excel=3), OutputFormat,
│                   TemplateVersionStatus (Draft=0, Published=1, Archived=2),
│                   RoleType (Admin, Editor, Viewer)
├── SmkDoc.Application/
│   ├── Common/
│   │   ├── Interfaces/  IExecutionContext, IPdfRenderer, IRepository<T>,
│   │   │                IStorageService, IUnitOfWork, IFieldMappingApplicatorService,
│   │   │                ITemplateScannerService, ITemplateDraftCache,
│   │   │                IPasswordHasher, IJwtTokenGenerator,
│   │   │                IJsonDataParser, ISchemaInferenceService, IJsonSchemaValidationService
│   │   ├── Models/      DTOs: GenerateDocumentRequest/Response, TemplateDto,
│   │   │                FieldMappingDto, TemplateDraftEntry, ParseDraftResult,
│   │   │                LoginRequest, LoginResponse,
│   │   │                UserListItem, InviteUserRequest, UpdateUserRoleRequest, …
│   │   └── Helpers/     PlaceholderHelper (SSoT regex), ThaiDataTransformer,
│   │                    FieldMappingApplicatorService
│   ├── Engines/     IRenderEngine (Strategy interface)
│   └── UseCases/
│       ├── Documents/    GenerateDocumentUseCase, PreviewDocumentUseCase, DocumentVersionUseCase
│       ├── Templates/    TemplateManagementUseCase, TemplateValidateUseCase,
│       │                 TemplateDraftUseCase (Parse→Preview→Commit),
│       │                 HtmlStudioUseCase, HtmlPersistenceUseCase
│       ├── FieldMappings/ FieldMappingUseCase, PreviewMappingUseCase (zero side-effects)
│       ├── Security/    ApiKeyUseCase, LoginUseCase, UserManagementUseCase
│       └── Validation/  ValidatePayloadUseCase (schema inference + JSON Schema validation)
├── SmkDoc.Infrastructure/
│   ├── Auth/        BcryptPasswordHasher, JwtTokenGenerator
│   ├── Cache/       InMemoryTemplateDraftCache (TTL 30 min, 20 MB limit)
│   ├── Engines/
│   │   ├── Html/    HtmlTemplateEngine, HtmlHelperRegistry (Handlebars custom helpers),
│   │   │            HtmlLayoutProcessor (header/footer/font), HtmlPlaceholderTransformer
│   │   ├── Word/    DocxTemplateEngine
│   │   └── Excel/   ExcelTemplateEngine, ExcelTableExpander, ExcelMediaInjector
│   ├── Parsing/     JsonDataParser (IJsonDataParser — FlattenNamed SSoT)
│   ├── Pdf/         GotenbergPdfRenderer
│   ├── Persistence/ AppDbContext, EfRepository<T>, UnitOfWork, Migrations/
│   ├── Schema/      SchemaInferenceService, JsonSchemaValidationService
│   ├── Storage/     MinioStorageService, MinioSettings
│   └── ExecutionContextImpl
└── SmkDoc.Api/
    ├── Controllers/ DocumentController, TemplateController, ApiKeyController,
    │               AuditLogController, AuthController, UsersController,
    │               DataConnectionsController, DatasetController
    ├── Middleware/  ApiKeyMiddleware, SecurityHeadersMiddleware
    ├── HealthChecks/ GotenbergHealthCheck, MinioHealthCheck
    └── Program.cs   (seed: Company → Project → AdminUser → ApiKey)
```

### Middleware Pipeline

```
SecurityHeaders → ForwardedHeaders → CORS → Authentication (JWT) → Authorization ([Authorize]) → RateLimiter → ApiKeyMiddleware → Controllers
```

Public (ไม่ต้อง API Key): `GET /`, `GET /swagger*`, `GET /health`, `POST /api/documents/preview/{slug}`, `POST /api/auth/login`

**Auth ต่อ endpoint (ตรวจจากโค้ดจริง ไม่ใช่เจตนา) — แยกตามเจตนาการใช้งาน 2 กลุ่ม:**

| กลุ่ม | Controller | `X-API-Key` (ApiKeyMiddleware) | `[Authorize]` (Bearer JWT) | เหตุผล |
|-------|-----------|-------------------------------|---------------------------|--------|
| Document/Template generation — core API, ให้ระบบภายนอกเรียกได้ (API-first) | `DocumentController`, `TemplateController` | ✅ ต้องมี | ❌ ไม่มี — ตั้งใจให้เรียกได้โดยไม่ login | ระบบภายนอก (machine-to-machine) ต้องเรียกได้โดยไม่มี user session |
| Portal management — ตั้งค่าระบบ, มีแต่ Portal UI เรียก | `UsersController`, `ApiKeyController`, `DataConnectionsController`, `DatasetController` | ✅ ต้องมี | ✅ มีแล้ว (`[Authorize]` class-level — บังคับแค่ authenticated, **ไม่ได้บังคับ role เฉพาะ Admin** ยกเว้น `UsersController` ที่มี `RequireAdmin()` เพิ่ม) | ปิดช่อง anonymous/pure-API-key access — role-specific enforcement ยังพึ่ง frontend `NAV_MIN_ROLE`/`RequireRole` ต่อ (ดู §9) |

\* `DatasetController` อยู่กึ่งกลาง — ตรวจ `[Authorize]` ก่อนเพิ่ม logic ใหม่ที่พึ่งพา role

ก่อนเพิ่ม endpoint ใหม่ให้ตัดสินใจก่อนว่าเป็น "core API สำหรับระบบภายนอก" (X-API-Key อย่างเดียว) หรือ "Portal management" (ต้องมี `[Authorize]` ด้วย) — อย่าเดาจาก path ชื่อ `/api/...` เฉยๆ

### DI Registration — เพิ่ม service ใหม่ต้องอัปเดต Program.cs

Pattern: `Singleton` = stateless / thread-safe, `Scoped` = per-request, `HttpClient typed` = typed client

Key registrations (อ่าน Program.cs สำหรับรายการเต็ม):
- `IRepository<T>` → `EfRepository<T>` (Scoped)
- `IStorageService` → `MinioStorageService` (Singleton)
- `IPdfRenderer` → `GotenbergPdfRenderer` (HttpClient typed)
- `IRenderEngine` → `HtmlTemplateEngine`, `DocxTemplateEngine`, `ExcelTemplateEngine` (Scoped, Strategy)
- `IPasswordHasher` → `BcryptPasswordHasher` (Singleton)
- `IJwtTokenGenerator` → `JwtTokenGenerator` (Singleton)
- `IJsonDataParser` → `JsonDataParser` (Singleton)
- `ISchemaInferenceService` → `SchemaInferenceService` (Singleton)
- `IJsonSchemaValidationService` → `JsonSchemaValidationService` (Singleton)
- UseCases ทั้งหมด → Scoped

---

## §5 Endpoints

| Method | Route | UseCase |
|--------|-------|---------|
| POST | `/api/documents/generate/{slug}` | GenerateDocumentUseCase |
| POST | `/api/documents/preview/{slug}` | PreviewDocumentUseCase |
| GET | `/api/documents/{ref}/versions` | DocumentVersionUseCase |
| GET | `/api/documents/{ref}/versions/{v}/download` | DocumentVersionUseCase |
| GET | `/api/documents/download/{logId:guid}` | DocumentVersionUseCase |
| POST | `/api/documents/render/stateless` | RenderStatelessDocumentUseCase |
| GET | `/api/templates` | TemplateManagementUseCase |
| POST | `/api/templates` [FromForm] | TemplateManagementUseCase |
| GET/PUT | `/api/templates/{id:guid}/html` | TemplateManagementUseCase |
| PUT | `/api/templates/{id:guid}` | TemplateManagementUseCase |
| DELETE | `/api/templates/{id:guid}` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/versions` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/download` | TemplateManagementUseCase |
| POST | `/api/templates/{id:guid}/rollback/{v:int}` | TemplateManagementUseCase |
| GET | `/api/templates/{id:guid}/scan-fields` | TemplateScannerService |
| POST | `/api/templates/scan-fields` [FormFile] | stateless — no DB write |
| GET/PUT | `/api/templates/{id:guid}/mappings` | FieldMappingUseCase |
| POST | `/api/templates/{id:guid}/mappings/preview` | PreviewMappingUseCase (zero side-effects) |
| POST | `/api/templates/{id:guid}/validate` | TemplateValidateUseCase |
| POST | `/api/templates/draft/parse` [FormFile] | TemplateDraftUseCase — RAM cache, no DB |
| POST | `/api/templates/draft/{id}/preview` | TemplateDraftUseCase — zero side-effects; 410 if expired |
| POST | `/api/templates/draft/{id}/commit` | TemplateDraftUseCase — atomic MinIO+DB; 410 if expired |
| GET/POST/DELETE | `/api/api-keys` | ApiKeyUseCase |
| GET | `/api/logs` | AuditLogController |
| GET | `/health` | HealthChecks |
| POST | `/api/auth/login` | LoginUseCase — public (ไม่ต้อง API Key) |
| GET/POST/PUT/DELETE | `/api/data-connections` | DataConnectionsController |
| POST | `/api/data-connections/test` | stateless — no persist |
| GET/POST/PUT/DELETE | `/api/datasets` | DatasetController |
| POST | `/api/templates/{id:guid}/validate-payload` | ValidatePayloadUseCase (schema inference + JSON Schema) |
| GET | `/api/users` | UserManagementUseCase — [Authorize] |
| POST | `/api/users` | UserManagementUseCase — [Authorize] Admin only |
| PUT | `/api/users/{id:guid}/role` | UserManagementUseCase — [Authorize] Admin only |
| DELETE | `/api/users/{id:guid}` | UserManagementUseCase — [Authorize] Admin only |
| PATCH | `/api/users/{id:guid}/status` | UserManagementUseCase — [Authorize] Admin only |

---

## §6 Frontend

### Structure

```
frontend-v2/src/
├── app/
│   ├── page.tsx                redirect('/templates') เท่านั้น
│   └── (app)/                  route group — real Next.js routing (ไม่ใช่ switch)
│       ├── layout.tsx          AuthProvider + auth gate + AppShell chrome (shared)
│       ├── templates/page.tsx
│       ├── upload/page.tsx
│       ├── studio/[templateId]/page.tsx
│       ├── generator/page.tsx  (slug ผ่าน ?slug= query param)
│       ├── audit/page.tsx
│       ├── logs/page.tsx
│       ├── mapping/page.tsx    (templateId ผ่าน ?templateId= query param)
│       ├── projects/page.tsx   (ApiKeysView — ชื่อ path คงเดิมตาม NavigationItemId 'projects')
│       ├── apidocs/page.tsx
│       ├── settings/page.tsx
│       ├── datasources/page.tsx
│       ├── users/page.tsx
│       ├── version-history/page.tsx  (Coming Soon)
│       └── analytics/page.tsx        (Coming Soon)
├── components/
│   ├── layout/
│   │   ├── AppShell.tsx       children: ReactNode ธรรมดา, อ่าน active tab จาก usePathname() + navigate ผ่าน useRouter()
│   │   ├── Sidebar.tsx        NavigationItemId (SSoT) + navSections — onSelectItem รับ router.push callback
│   │   └── Topbar.tsx
│   ├── ui/                    Primitives — ตรวจที่นี่ก่อนสร้างใหม่
│   │   Badge, Button, CardBlock, CodeBlock, Dropdown, EmptyState,
│   │   Input, Modal, Pagination, Select, StatBlock, Table, Tabs, Toast, Toolbar
│   └── features/
│       templates/, generator/, audit/, logs/, studio/,
│       mappings/, apikeys/, apidocs/, datasources/, settings/, users/
├── lib/api/
│   ├── client.ts              apiClient<T> + apiClientBlob (SSoT fetch)
│   ├── templates.api.ts, documents.api.ts, apikeys.api.ts, logs.api.ts, users.api.ts
├── schemas/                   Zod schemas — SSoT runtime types
├── types/api.ts               re-export ทุก schema — ใช้ตรงนี้เท่านั้น
├── hooks/                     useTemplates, useDebounce, …
└── tokens/                    Design tokens + DocumentCategory
```

### NavigationItemId — อัปเดต 3 ที่พร้อมกันเสมอ

เพิ่มเมนูใหม่ = เพิ่ม route ใหม่ ชื่อ folder ใต้ `app/(app)/` **ต้องตรงกับ** `NavigationItemId` string เป๊ะ เพราะ `AppShell.tsx` ใช้ `usePathname().split('/')[1]` แม็ปกลับเป็น `activeTab` โดยตรง (ไม่มี mapping table แยก)

| ไฟล์ | สิ่งที่ต้องเพิ่ม |
|------|---------------|
| `Sidebar.tsx` | union type + navSections |
| `AppShell.tsx` | `titles` Record + `searchItems` (ใช้ `navLabels`/`navSections` จาก Sidebar อยู่แล้ว — ปกติไม่ต้องแก้ไฟล์นี้เพิ่ม) |
| `app/(app)/<id>/page.tsx` **(ใหม่)** | สร้าง route ใหม่ตรงกับ `id`, ห่อ view component + `<RequireRole>` ถ้าจำเป็น, ใช้ `useRouter()`/`useSearchParams()` แทนการรับ callback prop จาก parent |

> เดิม CLAUDE.md เคยบอกว่า `app/page.tsx` เป็น switch-based router — ปัจจุบันเปลี่ยนเป็น Next.js App Router จริงแล้ว (ดู Structure ด้านบน) `app/page.tsx` เหลือแค่ `redirect('/templates')`, ไม่มี logic navigation อื่นอีก

### API Client Rules

- Base URL: `NEXT_PUBLIC_API_URL || 'http://localhost:8080'`
- Auth: `X-API-Key` จาก `localStorage('smk_api_key')`
- JSON → `apiClient<T>`, Binary → `apiClientBlob`, 204 → return `{}` as T

### HyperUI — UI Block Standard

ทุก UI ใหม่ **ต้อง reference HyperUI** (https://www.hyperui.dev) ก่อน แล้วปรับ class ให้ตรง token (`bg-surface`, `text-textPrimary`, `border-border`, `text-primary`)

| งาน | Category |
|-----|----------|
| List / grid card | Application UI → Cards |
| Form modal | Application UI → Forms |
| Stats tile | Application UI → Stats |
| Table + pagination | Application UI → Tables |
| Empty state | Application UI → Empty States |

---

## §7 Patterns & Anti-Patterns

### ✅ ถูก

```csharp
// Strategy — OCP
var engine = _engines.Single(e => e.EngineType == template.RenderEngineType);
await engine.ProcessAsync(context);

// DIP — inject interface
public class TemplateManagementUseCase(ITemplateScannerService scanner) { }

// UseCase ไม่รู้จัก HTTP
public async Task<TemplateDto> CreateTemplateAsync(CreateTemplateRequest req, CancellationToken ct)
```

```ts
// SSOT — import ผ่าน types/api.ts
import { TemplateDto } from '@/types/api';
```

### ❌ ห้ามทำ

```csharp
// switch บน engine type ใน UseCase
if (template.RenderEngineType == RenderEngineType.Html) { ... }

// inject concrete
public class TemplateManagementUseCase(TemplateScannerService scanner) { }

// UseCase รู้จัก HTTP
public async Task<IActionResult> CreateTemplate(IFormFile file)
```

```ts
// duplicate type ใน component
interface Template { id: string; name: string; }
```

### Guidelines & Escape Hatches

กฎทุกข้อด้านล่างเป็น **guideline** ไม่ใช่ absolute prohibition  
ถ้าจำเป็นต้องทำแบบอื่น → อธิบายเหตุผลใน Step 2 (Guide) ก่อน แล้ว comment ในโค้ดด้วย

**🔴 Critical — ห้ามละเมิดโดยไม่มีเหตุผลสำคัญมาก:**

| สิ่งที่ควรหลีกเลี่ยง | ทางที่ถูก | ละเมิดได้ถ้า |
|---------------------|-----------|-------------|
| `ITemplateScannerService` รู้จัก `ClosedXML` โดยตรง | สร้าง abstraction ใน Application | — |
| write DB / Storage ใน Preview endpoint | zero side-effects เสมอ | — |
| `UploadAsync()` / write `GenerationLog` ใน Preview | แยก Generate กับ Preview ให้ชัด | — |
| อ่าน `template.StorageKey` / `template.Version` | โหลดผ่าน `CurrentVersionId` → `IRepository<TemplateVersion>` | — |
| สร้าง `DocumentVersion` โดยไม่มี `Document` anchor | สร้าง `Document` ก่อนเสมอ | — |
| เก็บ `OutputKey/InputData/OutputFormat` ใน `DocumentVersion` | link ผ่าน `GenerationLogId` | — |

**🟡 Prefer — ทำได้ถ้ามีเหตุผล + comment:**

| สิ่งที่ควรหลีกเลี่ยง | ทางที่ prefer | ละเมิดได้ถ้า |
|---------------------|--------------|-------------|
| hardcode URL / bucket / port | `StorageBuckets` constants + Settings class | — |
| regex `{{...}}` นอก `PlaceholderHelper.cs` | ใช้ PlaceholderHelper เสมอ | — |
| Import จาก `schemas/` โดยตรงใน component | ใช้ผ่าน `types/api.ts` | — |
| เพิ่ม `NavigationItemId` แค่ที่เดียว | อัปเดต Sidebar + AppShell + page.tsx พร้อมกัน | — |
| `switch/if-else` บน `RenderEngineType` | Strategy Pattern | engine นั้นเป็น one-off ที่ไม่ขยาย |
| `as any` ใน TypeScript | type ที่ถูกต้อง | third-party type ขาด definition / migration period → ต้อง comment `// FIXME: reason` |
| UI primitive ใหม่ | ตรวจ `ui/` + HyperUI ก่อน | ไม่มี pattern ที่ใกล้เคียงจริงๆ → สร้างได้ แต่ follow design token |
| inject Infrastructure concrete | inject Interface | — |

---

## §8 Checklist + Testing — รันก่อนส่งงานทุกครั้ง

### Verify Commands (AI รันเอง)

```bash
cd backend-v2 && dotnet build src/SmkDoc.Api/SmkDoc.Api.csproj -v quiet   # ต้อง 0 error
cd frontend-v2 && npx tsc --noEmit                                         # ต้อง 0 error
cd backend-v2 && dotnet test                                               # ทุก test ผ่าน
```

### Pre-Submit Checklist

```
GENERAL
☐  แก้ใน backend-v2/ และ frontend-v2/ เท่านั้น
☐  Interface ก่อน Implementation (DIP) — ทุกครั้ง
☐  ไม่ duplicate regex / logic / type ที่มี SSoT
☐  Config ทุกค่าอยู่ใน appsettings.json + Settings class

BACKEND
☐  UseCase ไม่รู้จัก HttpContext / IActionResult
☐  Preview endpoints ไม่มี side-effect
☐  dotnet build → 0 error
☐  dotnet test → ทุก test ผ่าน
☐  แก้ UseCase / Helper / Engine → อัปเดต test ด้วยเสมอ

FRONTEND
☐  npx tsc --noEmit → 0 error
☐  NavigationItemId ใหม่ → อัปเดต Sidebar + AppShell + page.tsx ครบ
☐  UI ใหม่ → reference HyperUI + ปรับ design token
☐  ไม่สร้าง UI primitive ใหม่ถ้า ui/ มีอยู่แล้ว
☐  ไม่ import จาก schemas/ โดยตรง — ใช้ types/api.ts
```

### Testing Standards

**กฎ**: แก้ UseCase / Helper / Engine → ต้องแก้ / เพิ่ม test ด้วยเสมอ

| สิ่งที่แก้ | Test ที่ต้องอัปเดต |
|-----------|-----------------|
| UseCase ใหม่ | สร้าง `{Name}Tests.cs` ใน `tests/SmkDoc.Tests/` |
| แก้ UseCase เดิม | อัปเดต + เพิ่ม case ถ้ามี branch ใหม่ |
| แก้ Helper | อัปเดต Helper test file |
| แก้ Engine | อัปเดต Engine test ที่ตรงกัน |

กฎเขียน test: Mock ทุก dependency ผ่าน interface, มี happy path + ทุก error path, ชื่อ: `Method_Condition_Expected`

Frontend: `npx tsc --noEmit` = 0 error, Zod schema ห้าม bypass ด้วย `as any`, แก้ schema → ตรวจทุก component ที่ใช้ type นั้น

---

## §9 Pending Items

| งาน | Priority | หมายเหตุ |
|-----|----------|---------|
| `GET /api/logs/metrics` | P2 | Aggregate stats สำหรับ LogsView |
| EF Core Migration + `MigrateAsync` | ✅ Done | `AddAuthAndMultiTenancy` migration พร้อม backfill SQL |
| `DbSet<TemplateDataset>` + `IRepository<Document>` DI | P1 | ต้องลงทะเบียนให้ GenerateDocumentUseCase |
| Frontend schema update | P1 | `TemplateDto`: `version`+`storageKey` → `currentVersionId` |
| Login System (Portal Auth) | ✅ Done | JWT + [Authorize], users/companies/projects tables, RoleType enum, seed admin user |
| Frontend login page + AuthContext | ✅ Done | LoginView + `AuthContext` SSoT (React Context) — logout redirect แก้แล้ว |
| RBAC UI (Phase 0–2) | ✅ Done | `AuthContext` auto-logout timer + `SessionWarningModal`, `lib/rbac.ts` NAV_MIN_ROLE, Sidebar role-filter + dropdown, `RequireRole` guard — **UI-only**, ดู gap ด้านล่าง |
| `[Authorize]` ครอบ `ApiKeyController`/`DataConnectionsController`/`DatasetController` | ✅ Done | เพิ่ม class-level `[Authorize]` ทั้ง 3 controller — ปิดช่อง anonymous/pure-API-key access แล้ว |
| Role-specific enforcement (Admin only) ที่ `ApiKeyController`/`DataConnectionsController`/`DatasetController` | P2 | ตอนนี้บังคับแค่ "ต้อง login" ยังไม่บังคับ role เฉพาะ Admin เหมือน `UsersController.RequireAdmin()` (ตัดสินใจแล้วว่าให้ frontend `NAV_MIN_ROLE` จัดการชั้น role ไปก่อน) — ถ้าจะยกระดับเป็น backend-enforced Admin-only ค่อยกลับมาทำ ดู §4 ตารางแยก auth ต่อ endpoint |
| Cookie-based auth (httpOnly JWT) + RSC conversion + Server Actions + Auth.js | ❌ ตัดสินใจไม่ทำ | พิจารณาแล้วไม่คุ้ม — เป็น internal admin tool ไม่ใช่ public site (ไม่ต้อง SEO/first-paint แข่งขัน), endpoint ส่วนใหญ่ใช้แค่ `X-API-Key` (มี `NEXT_PUBLIC_DEFAULT_API_KEY` fallback ให้ server-side fetch ได้อยู่แล้วถ้าต้องการ RSC บางหน้า), mutation ปัจจุบันไม่มี CSRF risk เพราะใช้ custom header ไม่ใช่ cookie auto-attach — ถ้าจะทำใหม่ต้องมี pain point จริงเรื่อง initial-load speed ก่อน ไม่ใช่ preemptive |
| User Management CRUD | ✅ Done | `UserManagementUseCase` + `UsersController` — list / invite / role-change / remove / set-active; 208 tests passing |
| `RoleType.Developer → Editor` rename | ✅ Done | int value 1 unchanged (no migration), `ValidRoles` ใช้ `Enum.GetNames<RoleType>()` เป็น SSoT |
| `MapInboundClaims = false` + `RequireAdmin()` fallback | ✅ Done | ป้องกัน 401 เมื่อ JWT claim ถูก remap; fallback ทั้ง `"role"` และ `ClaimTypes.Role` |
| Login email case-insensitive | ✅ Done | `LoginUseCase` + `InviteAsync` normalize `.Trim().ToLowerInvariant()` |
| `ROLE_LEVEL: Record<UserRole, number>` enforce | ✅ Done | TypeScript บังคับ sync กับ `UserRole` union — `RequireRole.role: UserRole` |
| Remember Me / Refresh Token | P2 | Phase 3 — backend RefreshToken entity + `/api/auth/refresh` + frontend silent refresh |
| Forgot Password / Reset Password | P3 | Phase 4 — ต้องการ SMTP service |
| DB Redesign Wave 2 | P3 | `api_keys.created_by` FK, user profile fields |
| Thai font in Gotenberg | P3 | Plan: MinIO shared volume (qorstack pattern) — รายละเอียดใน memory |

---

## §10 Reference Docs

| Task | อ่านที่ |
|------|---------|
| Sprint plan | `docs/AI/IMPLEMENTATION_PLAN.md` |
| Core standards | `AGENTS.md` |
| Controller pattern | `docs/AI/patterns/controller.md` |
| UseCase / Service | `docs/AI/patterns/service.md` |
| Entity + Migration | `docs/AI/patterns/entity.md` |
| Frontend component | `docs/AI/patterns/frontend-component.md` |
| Anti-patterns | `docs/AI/ANTI-PATTERNS.md` |
| Architecture | `docs/AI/ARCHITECTURE.md` |
| Design tokens | `docs/AI/DESIGN.md` |

---

## §11 v1 Legacy

`backend/` + `frontend/` — เก็บไว้เปรียบเทียบ ยังไม่ลบ  
สิ่งที่ v1 มีแต่ v2 ยังไม่ port: ReportBro engine, Math expression resolver, SQL DataSource, DataProtection (AES)
