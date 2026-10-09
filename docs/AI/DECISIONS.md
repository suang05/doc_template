# DECISIONS.md — Architecture Decision Records (ADRs)
> SMK Document Server v2 • Updated September 2026

> **[AI_DIRECTIVE] Purpose & Usage:** 
> This file is a historical log of Architecture Decision Records (ADRs). It explains **WHY** certain technologies or patterns were chosen or rejected.
> - **DO NOT** read or reference this file for day-to-day coding, bug fixing, or feature implementation. (Use `CODING_CONVENTIONS.md` for active rules).
> - **ONLY** read this file when the user explicitly asks for architectural advice, tech stack changes, or historical design rationale to avoid suggesting rejected solutions.

---

## ADR-001: X-API-Key Authentication แทน JWT Bearer

**Date:** September 2026 | **Status:** Accepted

**Decision:** ใช้ `X-API-Key` header ร่วมกับตาราง `api_keys` ใน PostgreSQL สำหรับยืนยันตัวตนทุก request แทน JWT/Bearer Token

**Rationale:**
- Caller apps ส่วนใหญ่เป็น Internal C# systems — API Key จัดการง่ายกว่า Token lifecycle
- Revoke สิทธิ์ได้ทันที (ปิด row ในตาราง)
- ไม่มี Refresh Token complexity
- แยกการเข้าถึงตาม Project ได้ชัดเจน

**Implementation:**
- `ApiKeyMiddleware.cs` — validate ทุก request (ยกเว้น `/`, `/health`, `/swagger`)
- `MASTER_API_KEY` env var — bypass สำหรับ bootstrapping (ก่อนมี DB record)
- `ApiKeyUseCase.ValidateKeyAsync()` — hash comparison กับ `api_keys.key_hash`
- Unauthorized responses ใช้ RFC 7807 Problem Details format

**Consequence:** ระบบ `[Authorize]` attribute มาตรฐานของ ASP.NET ไม่ได้ใช้ — ใช้ Middleware-based validation แทน

---

## ADR-002: Gotenberg 8 แทน Embedded LibreOffice

**Date:** September 2026 | **Status:** Accepted

**Decision:** แปลง PDF ผ่าน Gotenberg 8 Microservice แทนการรัน LibreOffice process ตรงๆ บน API Server

**Rationale:**
- Gotenberg จัดการ Process lifecycle ป้องกัน Memory Leak / Process hang
- API Server เป็น Stateless ได้ — ไม่ต้องติดตั้ง LibreOffice บนเครื่อง
- Concurrency Control และ Timeout ในตัว
- Mock ได้ง่ายใน Unit Tests ผ่าน `IPdfRenderer` interface

**Implementation:**
- `GotenbergPdfRenderer.cs` — HTTP Client กับ Gotenberg API
- Config: `GotenbergUrl` env var หรือ `appsettings.json`
- Chromium endpoint: `POST /forms/chromium/convert/html` (HTML → PDF)
- LibreOffice endpoint: `POST /forms/libreoffice/convert` (DOCX/XLSX → PDF)

---

## ADR-003: Rich Domain Model แทน Anemic Domain Model

**Date:** September 2026 | **Status:** Accepted (ใช้งาน 100%)

**Decision:** เปลี่ยน Entities ทั้ง 14 ตัวจาก Anemic (`public set`, Object Initializers) เป็น Rich Domain Models

**Rules:**
- Properties: `private set` ทั้งหมด (หรือ `init`)
- Constructor: Parameterized เท่านั้น — ห้าม Object Initializer `{ }`
- EF Core: ต้องมี `private` Parameterless Constructor สำหรับ Materialization
- State changes: ผ่าน Business Methods เท่านั้น (เช่น `Activate()`, `Publish()`, `SetCurrentVersion()`)

**Consequence:**
- Use Cases อ่านง่ายขึ้น (delegate logic ไปที่ Entity)
- Unit Tests ต้องสร้าง Object ผ่าน Constructor (ทดสอบ business rules ได้ถูกต้อง)

---

## ADR-004: Smart Enums แทน C# Enum + Magic Strings

**Date:** September 2026 | **Status:** Accepted

**Decision:** แปลง Enums สำคัญจาก C# `enum` + switch statement เป็น Smart Enum classes ที่สืบทอดจาก `Enumeration` base class

**Affected Enums:**
| Smart Enum | Properties เพิ่มเติม |
|---|---|
| `TemplateFormat` | `.Extension`, `.MimeType`, `.RenderEngineType` |
| `OutputFormat` | `.ContentType`, `.Extension` |
| `RenderEngineType` | matching logic |
| `TemplateVersionStatus` | `.Name`, `.IsTerminal` |
| `RoleType` | permission mapping |

**Rationale:**
- ขจัด Primitive Obsession (ห้ามใช้ `"html"`, `".docx"` string ใน business logic)
- Behavior อยู่ใน Enum เอง ไม่ต้องมี helper methods แยก
- Compile-time safety — ผิดพลาดน้อยกว่า string comparison

**Consequence:** Unit Tests ต้องใช้ `TemplateFormat.Html` แทน `"html"` หรือ `TemplateFormat.Html.ToString()`

---

## ADR-005: Value Objects แทน Primitive Types ที่มี Validation

**Date:** September 2026 | **Status:** Accepted

**Decision:** แทนที่ `string DataSourceType` และ `string PayloadHashSha256` ด้วย Value Object classes

| Value Object | แทนที่ | Validation |
|---|---|---|
| `DataSourceType` | `string DataSourceType` ใน `FieldMapping` | ต้องเป็น `"json"` หรือ `"sql"` เท่านั้น |
| `Sha256Hash` | `string PayloadHashSha256` ใน `GenerationLog` | ต้องเป็น hex string 64 ตัวอักษร |

---

## ADR-006: Application DTO Boundary

**Date:** September 2026 | **Status:** Accepted

**Decision:** Use Cases ต้องคืนค่าเป็น Application DTOs เสมอ ห้ามคืน Domain Entities ออกจาก Application Layer

**Affected DTOs (เดิมอยู่ใน `SmkDoc.Application/Common/Models/` ปัจจุบันรวมเป็น SSoT ใน `SmkDoc.Application/DTOs/` ตาม ADR-020):**
- `TemplateDto` — Template metadata
- `TemplateVersionDto` — Version list (รวม `Status.Name` เป็น string)
- `DocumentVersionDto` — Document version history
- `FieldMappingDto` — Field mapping data
- `GenerateDocumentRequest/Response` — Document generation IO
- `TemplateDraftModels` — Draft save/load

**Rationale:**
- ป้องกัน Domain Entity รั่วไหลออกไปยัง API layer
- API response structure เปลี่ยนได้โดยไม่กระทบ Domain
- Serialization behavior ควบคุมได้ที่ DTO layer

---

## ADR-007: GlobalExceptionFilter แทน SchemaValidationExceptionFilter

**Date:** September 2026 | **Status:** Accepted

**Decision:** รวม `SchemaValidationExceptionFilter` และ Exception handling ทั้งหมดไว้ใน `GlobalExceptionFilter` เดียว แล้วลบ `SchemaValidationExceptionFilter` และ `ApiKeyAuthenticationHandler` ที่ไม่ได้ใช้ออก

**Rationale:**
- Single Responsibility ที่ชัดเจน — 1 Filter จัดการ Exception ทั้งระบบ
- ลบ `ApiKeyAuthenticationHandler` ซึ่งมี obsolete `ISystemClock` warning
- ลดความซับซ้อนของ Filter pipeline

**กฎ:** ห้ามสร้าง Exception Filter เพิ่มอีก ให้เพิ่ม mapping ใน `GlobalExceptionFilter.cs` เท่านั้น

---

## ADR-008: Rate Limiting แบบ Per-API-Key Partition

**Date:** September 2026 | **Status:** Accepted

**Decision:** ใช้ ASP.NET Core Rate Limiter ที่ Partition ตาม API Key value (ไม่ใช่ per-IP)

**Config:**
- 60 requests / minute / API Key
- Queue limit: 0 (ปฏิเสธทันทีถ้าเกิน)
- 429 Response: RFC 7807-inspired JSON body
- Fallback: partition ตาม IP ถ้าไม่มี X-API-Key

---

## ADR-009: DomainException Hierarchy & Machine-Readable Error Codes

**Date:** September 2026 | **Status:** Accepted

**Decision:** 
1. สร้าง `DomainException` เป็น abstract base class ใน `SmkDoc.Domain.Exceptions` สำหรับ Domain Exceptions ทั้งหมด โดยบังคับมี `ErrorCode` (machine-readable string) และ `StatusCode` (HTTP status code)
2. ปรับ `GlobalExceptionFilter` ให้ตรวจจับ `DomainException` และแนบ `extensions["errorCode"]` ลงใน ProblemDetails อัตโนมัติ
3. เปลี่ยน `DraftExpiredException` ให้ส่งกลับ HTTP 410 Gone (`DRAFT_EXPIRED`) และลบ `try-catch` ใน Controllers ออกทั้งหมด
4. ขจัด `KeyNotFoundException` ออกจาก Use Cases ทั้งหมด และหันมาใช้ `NotFoundException` (HTTP 404)
5. สร้าง `RenderException` (HTTP 500 / `DOCUMENT_RENDER_FAILED`) และ `ConflictException` (HTTP 409 / `RESOURCE_CONFLICT`) เพื่อแยกแยะข้อผิดพลาดให้ตรงตามความหมายที่แท้จริง

**Rationale:**
- ป้องกันการหลุดของ `KeyNotFoundException` ไปเป็น HTTP 500
- ขจัด duplication ของ `DraftExpiredException` ระหว่าง Domain และ Application layer
- เพิ่มความสามารถในการ Debug และ Integration ให้กับ Frontend/Client ผ่าน Machine-Readable `errorCode`

---

## ADR-010: High-Performance Domain Optimization & Zero-Allocation Caching

**Date:** September 2026 | **Status:** Accepted

**Decision:** ปรับปรุงแกนหลักของ `SmkDoc.Domain` เพื่อรองรับ High-Throughput (ระดับพัน requests/sec) และขยายตัวใน PostgreSQL:
1. **Generic Static Caching ใน `Enumeration`:** แทนที่การทำ Reflection (`GetFields`) สดทุกครั้งใน `GetAll<T>()`, `FromValue<T>()`, และ `FromDisplayName<T>()` ด้วย generic static class `Cache<T>` ที่คำนวณและเก็บ lookup dictionaries (`ById`, `ByName`) เพียงครั้งเดียวตอน initialization ทำให้ lookup เป็น **$O(1)$** พร้อมเพิ่ม `TryFrom...` pattern
2. **UUIDv7 ใน `BaseEntity`:** เปลี่ยน Default ID จาก `Guid.NewGuid()` (UUIDv4 สุ่ม) เป็น **`Guid.CreateVersion7()`** (UUIDv7) เพื่อเรียงตาม Timestamp ลดการเกิด B-Tree Page Splits และ Fragmentation ในตาราง Audit Log และ Document Versions ของ PostgreSQL
3. **Zero-Allocation Hex Validation ใน `Sha256Hash`:** เลิกใช้ `Regex.IsMatch` แบบ uncompiled และเปลี่ยนมาใช้ `char.IsAsciiHexDigit` validation loop ซึ่งเร็วกว่า 20 เท่าและไม่มี memory allocation
4. **OCP Alignment ใน `TemplateFormat`:** ผูก `DefaultEngineType` เข้ากับ `TemplateFormat` Smart Enum โดยตรง ขจัด `if/else` ใน `TemplateVersion.GetRenderEngineType()`
5. **Encapsulation ใน `UserProjectRole`:** เพิ่ม constructor, `init` accessors, และ domain method `UpdateRole(RoleType)`
6. **Smart Enum `GenerationStatus`:** สร้าง `GenerationStatus` แทนการใช้ raw string `"SUCCESS"` ในระบบบันทึก Log

**Rationale:** ขจัดคอขวดด้าน CPU/GC Allocation และรองรับ Enterprise Scaling เทียบเคียงมาตรฐานระบบรายงานระดับองค์กร (Jasper Reports, Carbone.io)

---

## ADR-011: Hybrid RBAC & Dual-Channel Authentication Model

**Date:** September 2026 | **Status:** Accepted

**Decision:** แบ่งช่องทางการยืนยันตัวตนและการเข้าถึงออกเป็น 2 ช่องทางอย่างอิสระ:
1. **Machine-to-Machine (M2M Channel):** สำหรับระบบภายนอก (ERP, CRM, Billing) คุยผ่าน `X-API-Key` เท่านั้น โดย `ApiKeyMiddleware` จะแกะและ bind `ProjectId` เข้าสู่ `IExecutionContext` อัตโนมัติ (Stateless, ไม่เกี่ยวข้องกับ User หรือ Session)
2. **Human Management (Portal Channel):** สำหรับบุคลากรผ่านเว็บ Next.js Portal คุยผ่าน Bearer JWT:
   - **Login Frictionless:** ผู้ใช้กรอกเพียง Email + Password ไม่ต้องระบุ ProjectId ก่อน Login
   - **Smart Enum `SystemRole`:** บรรจุ `SuperAdmin`, `Member`, `Viewer` ในระดับ `User` entity
   - **SuperAdmin Privilege:** หากมีสิทธิ์เป็น `SuperAdmin` จะสามารถเข้าถึงและบริหารจัดการได้ทุก Project ทันที (ลดภาระงานของทีม Core IT ในการ assign สิทธิ์ราย Project)
   - **Member Granular Scope:** ผู้ใช้ทั่วไปที่เป็น `Member` จะมีสิทธิ์เข้าถึงเฉพาะ Project ที่ได้รับมอบหมายใน `UserProjectRole`
   - **AccessibleProjects Response:** คืนรายการ Project ทั้งหมดที่เข้าถึงได้พร้อม Default Project และ Role ของตนเองไปกับ Login Response


---

## ADR-012: SmkDoc.Tests Clean Architecture Mirroring & Modernization

**Date:** September 2026 | **Status:** Accepted

**Decision:** ปรับปรุงโครงสร้างและมาตรฐานชุดการทดสอบทั้งหมดของ `SmkDoc.Tests` (Unit & Integration Test Suite):
1. **Mirroring Clean Architecture (1:1 Folders & Namespaces):** ย้ายไฟล์เทสต์ทั้งหมดจาก Root เข้าสู่โครงสร้างตามชั้นสถาปัตยกรรม (`Domain/`, `Application/`, `Infrastructure/`, `Api/`, `Integration/`, `Common/`) พร้อมจัด namespace ให้ตรงกับ directory
2. **Single Responsibility Principle (SRP):** แยกการทดสอบข้าม Layer เช่น ย้าย `GlobalExceptionFilter` ออกจาก `DomainExceptionTests` ไปยัง `Api/Filters/GlobalExceptionFilterTests.cs` และย่อย `DomainOptimizationTests` เป็น `EnumerationTests`, `BaseEntityTests`, และ `ValueObjectTests`
3. **FluentAssertions Single Source of Truth (SSoT):** บังคับใช้ FluentAssertions 100% ขจัด xUnit `Assert.*` ทั้งหมด และกำหนดมาตรฐาน Exception testing ผ่าน `FluentActions.Invoking(...)`
4. **Pure Tests & Side-Effect Elimination:** ขจัด Hardcoded relative path 6 ชั้น (`../../../../../../`) ใน Sample Generators โดยให้เขียนลงโฟลเดอร์ชั่วคราว (`Path.GetTempPath()`) ระหว่างการรันเทสต์ปกติ เพื่อป้องกันไม่ให้ Git workspace สกปรก
5. **Test Categorization ([Trait]):** ติด Tag `Category=Benchmark` และ `Category=Generator` เพื่อให้ CI/CD pipeline สามารถรันเฉพาะ Pure Unit Tests ได้อย่างรวดเร็ว (`--filter "Category!=Benchmark&Category!=Generator"`)
6. **Test Data Builders & Fixture Pattern:** สร้าง `TemplateBuilder`, `TemplateVersionBuilder`, `UserBuilder` ใน `Common/Builders/` และ `GenerateDocumentTestFixture` ใน `Common/Fixtures/` เพื่อลด mock boilerplate และลดความเปราะบางของ constructor injection

**Rationale:** ทำให้ชุดการทดสอบมีความเป็นระเบียบ สะอาด บำรุงรักษาง่าย (Maintainable) ไร้ Flakiness บน CI/CD runners และเอื้อต่อการต่อขยายฟีเจอร์ใหม่โดยไม่ทำให้เทสต์เก่าพังง่าย

---

## ADR-013: Compiled Template Caching & Performance Optimization (Phase 1 Quick Wins)

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Handlebars Compiled Template Caching:**
   - สร้าง Application port `ICompiledTemplateCache` โดยกำหนด contract เป็น pure delegate `Func<object, string> GetOrAdd(...)` เพื่อไม่ให้รั่วไหล dependency ของ HandlebarsDotNet สู่ชั้น Application
   - Implement `MemoryCompiledTemplateCache` ใน Infrastructure ด้วย `IMemoryCache` (Sliding Expiration 1 ชั่วโมง)
   - คำนวณ Cache Key จาก SHA-256 Hash ของ Normalized Template HTML ใน `HtmlTemplateEngine` เพื่อขจัด overhead ของ AST parsing และ Handlebars compilation ทุกครั้งที่มีการเรนเดอร์เทมเพลตเดิมซ้ำ
2. **Strict UUIDv7 Sequential ID Enforcement:**
   - ขจัด `{ Id = Guid.NewGuid() }` ที่หลงเหลือใน `GenerateDocumentUseCase`, `TemplateDraftUseCase`, `TemplateDatasetUseCase`, `FieldMappingUseCase`, `DatasetUseCase`, และ `DataConnectionUseCase` ออกทั้งหมด
   - คืนค่าให้ใช้ `Guid.CreateVersion7()` ผ่าน `BaseEntity` default initializer 100% เพื่อรักษาประสิทธิภาพการจัดทำ B-Tree Index บน PostgreSQL
3. **Smart Excel Page Setup & Orientation Preservation:**
   - ปรับปรุง `ExcelTemplateEngine` ให้เคารพการตั้งค่าหน้ากระดาษ (Landscape/Portrait) และ custom print fit (`PagesWide`, `PagesTall`) ของผู้ออกแบบเดิม และกำหนดกระดาษเป็น A4 เฉพาะกรณีที่ไฟล์ไม่ได้ระบุไว้ (Letter default)
4. **OCP Alignment via Smart Enum:**
   - เพิ่ม `TemplateFormat.TryFromExtension` ใน `SmkDoc.Domain.Enums`
   - ปรับปรุง `RenderStatelessDocumentUseCase` ให้ resolve EngineType ผ่าน Smart Enum แทน `switch (ext)` statement

**Rationale:** ยกระดับ throughput ของการสร้างเอกสาร HTML ขึ้น 40-50% และปิดช่องโหว่ทางสถาปัตยกรรม (DIP, OCP, Sequential UUIDv7) โดยไม่ต้องแก้ Database Schema

---

## ADR-014: Clean Architecture v2 DTO & Command Restructuring

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Centralize DTOs by Feature Bounded Context:**
   - ย้ายและจัดระเบียบ DTOs ทั้งหมดไว้ใน `SmkDoc.Application/DTOs/<Feature>/` (`Documents/`, `Templates/`, `FieldMappings/`, `Datasets/`, `DataConnections/`, `Security/`, `Users/`, `Projects/`, `Logs/`)
   - ยุบเลิก inline DTOs ที่ฝังอยู่ในไฟล์ UseCase (`ValidatePayloadResult`, `ApiKeyDto`, `CreateApiKeyResult`, `LoginRequest`, `LoginResponse`, `UserProfileDto`, `AccessibleProjectDto`, `GenerationLogPagedResult`)
2. **Command / Query / DTO Naming Conventions:**
   - แยกแยะประเภทข้อมูลขาเข้าและขาออกอย่างชัดเจน:
     - การแก้ไข/ประมวลผล (Mutations): `*Command` (เช่น `GenerateDocumentCommand`, `CreateTemplateCommand`, `InviteUserCommand`)
     - การค้นหา/ตัวเลือก (Queries/Options): `*Query` (เช่น `PreviewDocumentQuery`, `PreviewMappingsQuery`)
     - ผลลัพธ์ข้อมูลขาออก (Outputs): `*Dto` / `*ResultDto` (เช่น `TemplateDto`, `DocumentVersionDto`, `LoginResultDto`)
3. **100% Immutable Positional Records:**
   - แปลง DTOs ทั้งหมดจาก mutable classes (`{ get; set; }`) เป็น immutable `record`s เพื่อความปลอดภัยต่อ Concurrency และ Thread-safety
4. **Domain Entity Leak Remediation:**
   - ปรับปรุง `ApiKeyUseCase.ValidateKeyAsync` ให้คืนค่าเป็น `ValidatedApiKeyDto?` แทนการคืน Domain Entity `ApiKey` ออกไปยัง Presentation Layer (`ApiKeyMiddleware`) ตัดขาด Dependency ข้าม Layer อย่างเด็ดขาด
5. **Purge Presentation Annotations from Application Layer:**
   - ลบ `System.ComponentModel.DataAnnotations` (`[Required]`, ฯลฯ) ออกจากชั้น Application ใน `HtmlToPdfRequest.cs`
6. **Concurrent Cache Stampede Fix:**
   - แก้ไขการประเมิน factory ซ้ำซ้อนภายใต้ concurrent requests ใน `MemoryCompiledTemplateCache` ด้วย double-checked locking per cache key
7. **Zero-Breaking Compatibility Layer:**
   - ชั่วคราวให้ `Common/Models/` ทำหน้าที่เป็น backward-compatibility bridge สืบทอด/forward ไปยัง canonical DTOs ใน `DTOs/` (ปัจจุบันถูก Phase-Out และลบทิ้งโดยสมบูรณ์แล้วใน ADR-020)

**Rationale:** สร้างความชัดเจนในการแบ่งชั้นสถาปัตยกรรม (Boundaries) ตามหลัก Clean Architecture v2 ขจัดความสับสนระหว่าง HTTP Contract และ Application Model พร้อมทั้งเพิ่มความปลอดภัยด้าน Concurrency

---

## ADR-015: Dual-Engine Automatic Validation Pipeline (FluentValidation + JsonSchema.Net)

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Dual-Engine Architecture (Static vs. Dynamic Concerns):**
   - **Static Engine (FluentValidation v11.11):** ตรวจสอบโครงสร้าง C# Command/Request DTOs ใน `SmkDoc.Application/Validators/` (เช่น Data Type, Format, Mandatory fields, Length, Regex slugs)
   - **Dynamic Engine (JsonSchema.Net Draft-07):** ตรวจสอบ Document Data Payload เทียบกับ JSON Schema ประจำแต่ละ Template Version ใน `SmkDoc.Infrastructure/Schema/`
2. **Domain Exception Mapping:**
   - สร้าง `ValidationException : DomainException` ใน `SmkDoc.Domain.Exceptions` (HTTP 400, ErrorCode: `"VALIDATION_FAILED"`, เก็บ `IDictionary<string, string[]> Errors`)
3. **Automatic Action Filter Execution:**
   - สร้าง `ValidateCommandFilter` ใน `SmkDoc.Api/Filters/` ดักจับทุก Command Arguments ก่อนเข้า Controller Actions และเรียก `IValidator<T>` จาก DI Container อัตโนมัติ (ขจัด Boilerplate `if (!ModelState.IsValid)` หรือการเขียน manual validation ใน Controller)
4. **Unified RFC 7807 Error Presentation:**
   - ปรับปรุง `GlobalExceptionFilter` ให้แปลง `ValidationException` เป็น RFC 7807 Problem Details พร้อม `errors` dictionary อย่างเป็นมาตรฐาน
5. **KISS & Clean Architecture Adherence:**
   - ไม่ใช้ MediatR/CQRS Pipeline Behavior ที่ซับซ้อนตามข้อกำหนดใน `AGENTS.md`
   - Validators ใน `SmkDoc.Application` เป็น Pure C# ไม่มี dependency ต่องาน HTTP/ASP.NET Core ใดๆ

**Rationale:** มอบการตรวจสอบที่รวดเร็วแบบ Fail-Fast (ประหยัด Connection/Query DB) พร้อมความยืดหยุ่นสูงสุดสำหรับทั้ง API Model คงที่และข้อมูลเอกสารที่มี Schema เปลี่ยนไปตามแต่ละเทมเพลต

---

## ADR-016: Schema Infrastructure Clean Code & Performance Optimization

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Infrastructure Layer Retention (Clean Architecture DIP):**
   - คง `JsonSchemaValidationService` และ `SchemaInferenceService` ไว้ในชั้น `SmkDoc.Infrastructure/Schema/` ตามเดิม เพื่อปกป้องชั้น Application ไม่ให้ผูกติดกับ third-party library `JsonSchema.Net` โดยให้ชั้น Application ใช้งานผ่าน Abstraction ports (`IJsonSchemaValidationService`, `ISchemaInferenceService`) เท่านั้น
2. **In-Memory Schema Compilation Caching:**
   - เพิ่ม `ConcurrentDictionary<string, JsonSchema>` ใน `JsonSchemaValidationService` เพื่อแคช compiled schema instance ป้องกัน CPU/Memory overhead จากการ parse string เดิมซ้ำในทุก request
   - ประกาศ `static readonly EvaluationOptions Draft7Options` เพื่อตัด zero-allocation ในการประเมิน Schema
3. **DRY Decomposition in Schema Inference:**
   - สร้าง `GroupedTokens` record รวมตรรกะการจัดกลุ่ม placeholders (`GroupPlaceholders`) สำหรับ scalar, nested object, และ array collection ไว้ที่จุดเดียว ขจัดโค้ดซ้ำซ้อนระหว่าง Schema generation และ Sample Mock Data generation
4. **Readability & Cyclomatic Complexity Reduction:**
   - แปลงบล็อก `if-else` หลายสิบชั้นใน `InferPropertySchema` และ `GenerateMockValue` ให้เป็น C# Pattern Matching Switch Expressions
   - ใช้ `[GeneratedRegex]` source generator สำหรับ Handlebars loop blocks (`EachBlockRegex`)

**Rationale:** เพิ่ม Throughput และลด Latency ของกระบวนการตรวจสอบ JSON Schema พร้อมทั้งยกระดับความสามารถในการอ่านและบำรุงรักษาโค้ด (Readability & Maintainability) ให้เป็นไปตามมาตรฐาน Clean Code สากล

---

## ADR-017: C# 12 Primary Constructors & Sealed Use Cases Standard

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Primary Constructor Adoption:**
   - ปรับปรุง Use Cases ทั้งหมดใน `SmkDoc.Application/UseCases/` ให้ใช้ **C# 12 Primary Constructors**
   - ขจัด boilerplate code ของ explicit constructor assignment และ private readonly field declarations ลดความยาวของคลาสลง 200+ บรรทัด
2. **Sealed Class Modifier:**
   - กำหนด modifier `public sealed class` ให้กับ Use Cases ทั้งหมดเพื่อป้องกันการสืบทอดโดยไม่ตั้งใจ และเปิดโอกาสให้ JIT Compiler ทำ Method Devirtualization เพิ่มประสิทธิภาพ Runtime
3. **Pure Application Layer Compliance:**
   - ยืนยันการคงสถานะ Pure C# ของ Application Layer โดยไม่มีการอ้างอิง `Microsoft.AspNetCore.*`, `IHttpContextAccessor`, หรือ `StatusCodes`
   - การส่งผ่าน `CancellationToken` ดำเนินการอย่างต่อเนื่องในทุก Asynchronous method

**Rationale:** ยกระดับ Clean Code, Readability, และ Simplicity ตามมาตรฐานสากลของ .NET Application Layer

---

## ADR-018: Standalone Scalable Schema Validator & Bounded Caching

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Stateless Standalone Validation Endpoint (`POST /api/v1/schemas/validate`):**
   - สร้าง Endpoint และ `ValidateStandaloneSchemaUseCase` สำหรับ Dry-run JSON Schema Draft-07 กับ Payload โดยตรง
   - ทำงานเป็น Pure In-Memory Service (Zero-DB, Zero-MinIO Dependency) ลด Latency ให้เหลือ 1–5ms
   - ให้บริการทั้ง Monaco Editor Studio (Live contract testing) และ External M2M Services
2. **Bounded Memory Caching with SHA-256 Key Hashing:**
   - แก้ไขปัญหา Unbounded Memory Leak ของ `ConcurrentDictionary` โดยเปลี่ยนมาใช้ `IMemoryCache` พร้อมกำหนด `SlidingExpiration = 2 hours`, `AbsoluteExpiration = 8 hours`, และ `SizeLimit`
   - ใช้ SHA-256 Hashing (`schema_v7_<HEX>`) เป็น Cache Key เพื่อลด Large Object Heap (LOH) footprint
3. **Direct JsonElement Overloads:**
   - ขจัด Double Serialization / Double Parsing (`JsonElement` -> `string` -> `JsonNode`) โดยเพิ่ม Overloads รับ `JsonElement` เข้าไปประเมินตรงๆ ใน `IJsonSchemaValidationService`
4. **Clean Code & Domain Alignment:**
   - ลบไฟล์ตกค้าง `ReportHub.Domain.Validation` ออกจาก Domain Layer และใช้ `SchemaValidationError` ของ Domain เป็น Single Source of Truth

**Rationale:** รองรับ High-Throughput และป้องกัน Memory Exhaustion จากการพิมพ์สดใน Monaco Editor Studio พร้อมยึดมั่นหลักการ Clean Architecture v2 และ DRY

---

## ADR-019: Template Payload Pre-Flight Validation with Multi-Tenant Scoping

**Date:** September 2026 | **Status:** Accepted

**Decision:**
1. **Dedicated Template Payload Validation Endpoint (`POST /api/v1/templates/{slug}/validate`):**
   - ย้าย/เพิ่ม Endpoint สำหรับการทำ Dry-run ตรวจสอบ JSON Payload เทียบกับ `data_schema` ของ Template ที่ Published อยู่ในฐานข้อมูลให้อยู่ใต้ Resource `/api/v1/templates`
   - มี Alias `POST /api/v1/templates/{slug}/validate-payload`
   - รันแบบ Zero Side-Effects (ไม่เรียก Gotenberg/Chromium, ไม่อัปโหลด MinIO, ไม่บันทึก DB audit logs)
2. **Multi-Tenant Scoping (`IMustHaveProject`):**
   - Use Case บังคับตรวจสอบ `t.ProjectId == _executionContext.ProjectId` (หากมี Project Context ในคำขอ) ป้องกันการเข้าถึง Template ข้าม Tenant โดยเด็ดขาด
3. **Domain Reuse:**
   - ใช้งาน `ValidationErrorItem` และ `SchemaValidationResult` จาก `SmkDoc.Domain.ValueObjects.Validation` ร่วมกัน ไม่สร้าง Domain Model ซ้ำซ้อน
4. **Direct JsonElement Pipeline:**
   - รับและส่งต่อ `JsonElement` เข้าสู่ `JsonSchemaValidationService` โดยตรง ลด GC Allocation ซ้ำซ้อน

**Rationale:** ยกระดับความปลอดภัย Multi-tenancy และมาตรฐาน RESTful Resource Design ควบคู่กับประสิทธิภาพสูงสุด

---

## ADR-020: Phase-Out and Complete Removal of `Common/Models/`

**Date:** September 2026 | **Status:** Accepted

**Context & Problem:**
- ใน ADR-014 มีการสร้าง `SmkDoc.Application/DTOs/{Feature}` ขึ้นมาเป็น Canonical Application Models และคงโฟลเดอร์ `Common/Models/` ไว้เป็น compatibility bridge ชั่วคราว
- การมีทั้ง `Common/Models/` และ `DTOs/` ก่อให้เกิดความสับสน (Ambiguity) ในการนำไปใช้งาน เสี่ยงต่อการเกิด Model Drift และขัดต่อหลักการ Single Source of Truth (SSoT)

**Decision:**
1. **Total Phase-Out of `Common/Models/`:**
   - ลบไฟล์ Type Alias / Shim DTOs ทั้งหมด 11 ไฟล์ใน `SmkDoc.Application/Common/Models/`
   - ลบโฟลเดอร์ `Common/Models/` ออกจาก Application Layer โดยสมบูรณ์
2. **Migration to Canonical Feature-Sliced DTOs (`SmkDoc.Application.DTOs.*`):**
   - ทุก Caller ใน `SmkDoc.Application` (UseCases, Interfaces, Helpers) เปลี่ยนไปใช้ Canonical DTOs/Commands/Queries
   - ทุก Adapter ใน `SmkDoc.Infrastructure` (Security, Cache) อ้างอิงตรงไปยัง `DTOs.*`
   - ทุก Controller ใน `SmkDoc.Api` สื่อสารผ่าน Canonical DTOs และ Commands จาก `DTOs.*`
   - ทุกชุดทดสอบใน `SmkDoc.Tests` เรียกใช้ `GenerateDocumentCommand`, `CreateTemplateCommand`, `CommitDraftCommand`, `SaveFieldMappingItemDto`, `ResolvedDatasetContext`
3. **Preserve External Contract Parity:**
   - รักษา JSON Property Names, types, และ format ให้ตรงกับ API Contract เดิม 100% (Zero breaking changes)

**Rationale:** ขจัดความซ้ำซ้อนของ DTOs บังคับใช้ Single Source of Truth (SSoT) สำหรับ Application Models และทำให้โครงสร้างโค้ด Clean, Simple และ Maintainable สูงสุด

---

## ADR-021: Domain Layer Integrity — Fail-Fast Invariants, Aggregate Boundaries & Value Objects

**Date:** October 2026 | **Status:** Accepted (Phases 1–6 Implemented — 100% Complete)

**Context & Problem:**
- Entities มี `private set` แล้ว (ADR-003) แต่ constructor/method ไม่ validate ทำให้สร้าง object สถานะไม่สมบูรณ์ได้
- ไม่มีขอบเขต Aggregate — child entity ทุกตัวมี repository ของตัวเอง ทำให้กฎข้าม entity (alias ไม่ซ้ำ, publish ได้ทีละเวอร์ชัน) ไปอยู่ใน UseCase
- UseCase บางตัว fallback ไป `GetDefaultAsync()` → ส่ง `ProjectId = Guid.Empty` เข้า Entity

**Decision:**
1. **Exception taxonomy:** แยก Pure Domain Exceptions (`DomainException`, `DomainValidationException`, `BusinessRuleViolationException`) ไว้ใน `SmkDoc.Domain.Exceptions` และย้าย Application Exceptions (`NotFoundException`, `ValidationException`, `UnauthorizedException`, `ConflictException`, `DraftExpiredException`, `RenderException`, `SchemaValidationException`) ไปไว้ที่ `SmkDoc.Application.Common.Exceptions`
2. **Fail-fast:** ทุก ctor/business method validate ก่อน mutate; method idempotent เมื่อ state ไม่เปลี่ยน
3. **Aggregates & Collection Encapsulation:** Entities หลัก (`Project`, `Company`, `User`, `Template`) expose collections เป็น `IReadOnlyCollection<T>` backed by `private readonly List<T>`; แก้ไข state ผ่าน Domain methods (`AssignProjectRole()`, etc.); อ้างข้าม aggregate ด้วย Id
4. **Value Objects:** slug/email/hash/storage key เป็น VO — นำ `TemplateSlug` มาใช้งานเป็น Property `Slug` ใน `Template.cs` พร้อม EF Core Value Converter ใน `AppDbContext.cs`
5. **Repositories:** คืน Entity หรือ `IReadOnlyList<T>` ทั่วทั้งระบบ (ทุก Domain Repository interfaces และ Infrastructure Repositories); lookup ของข้อมูล tenant ต้องรับ `projectId` และตัด dead code fallback (`GetDefaultAsync`) ออก 100%
6. **Invariants are non-negotiable (AP-023):** ห้ามผ่อนกฎเพื่อให้เทสต์/UseCase ผ่าน — แก้ที่ caller

**Implemented (Phases 1–6):**
- ข้อ 1: Exception taxonomy separation เสร็จสมบูรณ์ 100% (Domain เป็น pure exceptions, Application เป็น HTTP-mapped exceptions)
- ข้อ 2: Invariant fail-fast validation ใน Entity constructors & domain methods
- ข้อ 3: Collection encapsulation บน `Project`, `Company`, `User` เป็น `IReadOnlyCollection<T>`
- ข้อ 4: `TemplateSlug` Value Object ใช้งานจริงใน `Template.Slug` พร้อม EF Core Value Converter
- ข้อ 5: Repository return types hardening ครบ 100% — ทุก Domain repository (`ITemplateRepository`, `IProjectRepository`, `IApiKeyRepository`, `IUserRepository`, `ICompanyRepository`, `IDatasetRepository`, `IDataConnectionRepository`, `IUserProjectRoleRepository`) คืน `Task<IReadOnlyList<T>>`
- ข้อ 6: ขจัด Dead Code `GetDefaultAsync()` ใน `IProjectRepository` และ `ProjectRepository`
- ข้อ 7: Clean up unused repository injections ใน `SaveTemplateMappingsUseCase` และ `SaveTemplateDatasetsUseCase`
- ข้อ 8: **Phase 5 (Template Aggregate Root Consolidation & Child Repository Phase-Out):**
  - รวมการ query และจัดการ child entities (`FieldMapping`, `TemplateDataset`) ให้อยู่ภายใต้ Aggregate Root `Template` ผ่าน `GetByIdWithDetailsAsync` / `GetBySlugWithDetailsAsync` ทั้งหมด
  - ปรับ `PreviewMappingUseCase`, `GetTemplateMappingsUseCase`, `GetTemplateDatasetsUseCase`, `TemplateDatasetUseCase`, `DocumentDataPreparationService`, `GenerateDocumentUseCase`, และ `CommitTemplateDraftUseCase` ให้ทำงานผ่าน Aggregate Root `Template` โดยตรง
  - เลิกใช้งานและลบ child repositories (`IFieldMappingRepository`, `ITemplateDatasetRepository`, `FieldMappingRepository`, `TemplateDatasetRepository`) ออกจาก Domain และ Infrastructure 100% รวมถึงลบ DI registrations ออกจาก `DependencyInjection.cs`
- ข้อ 10: **Phase 7 (Tenant-Scoped Query Hardening & Complete AP-025 Enforcement):**
  - เปลี่ยน `ITemplateRepository.ListAsync()` → `ListByProjectAsync(Guid projectId, CancellationToken ct)` ขจัด non-tenant listing ใน Domain repository
  - ยุบ overload `GetBySlugAsync` และ `GetBySlugWithDetailsAsync` ให้บังคับรับ `Guid projectId` ทุกจุด ป้องกัน slug collision ข้าม tenant
  - ปรับ `IApiKeyRepository.ListAsync(Guid? projectId)` → `ListByProjectAsync(Guid projectId, CancellationToken ct)` ขจัด optional projectId parameter
  - อัปเดต `ListTemplatesQuery(Guid ProjectId)` และ `ListApiKeysQuery(Guid ProjectId)` พร้อม fail-fast validation (`DomainValidationException` เมื่อ `ProjectId == Guid.Empty`)
  - ฉีด `IExecutionContext` เข้าสู่ `GenerateDocumentUseCase` เพื่อนำ `ProjectId` ของ tenant ที่ผ่านการ verify แล้วไป query Template ตาม slug
  - ปรับปรุง `TemplateController` และ `ApiKeyController` ให้รองรับทั้ง query parameter `?projectId=` และ context fallback

**Remaining gaps (backlog):**
- ไม่มี (ทุก Phase 1–7 ของ ADR-021 เสร็จสมบูรณ์ครบถ้วน 100%)

**Rationale:** ให้ Domain เป็นผู้รับประกันความถูกต้องของข้อมูลเพียงผู้เดียว (single guardian of invariants) ลดการกระจายกฎธุรกิจใน UseCase และป้องกัน tenant leak

---

## ADR-022: Comprehensive Domain Layer Refactoring — DDD Aggregates, Sealed Rich Entities, Value Converters & Cross-Aggregate Id References

**Date:** October 2026 | **Status:** Accepted (Phases 1, 2A, 2B, 2C Implemented — 100% Complete)

**Context & Problem:**
- Entities ทั้ง 14 ตัวใน `SmkDoc.Domain` แม้จะมี `private set` ตาม ADR-003 แต่หลายตัวยังใช้ public constructor หรือ object initializers โดยไม่มีการตรวจสอบ invariants อย่างรัดกุม
- ข้อมูลสำคัญทางธุรกิจ (Names, References, Aliases, Hashes, Expiration, Connection types) กระจายตัวเป็น primitive strings ขาด encapsulation และ structural equality
- การนำทาง (Navigation Properties) ข้าม Aggregate Root (เช่น `Template -> Project`, `Document -> Template`, `GenerationLog -> ApiKey/Template`) ละเมิด DDD Aggregate Boundaries และทำให้ Domain coupling สูง
- เมธอดและ constructor สร้าง timestamp `DateTimeOffset.UtcNow` ภายในคลาสเอง ทำให้การทดสอบ deterministic state และ replay audit มีความคลาดเคลื่อน

**Decision:**
1. **Sealed Rich Entities & Static Factories:**
   - Entities ทั้ง 14 ตัวถูกปรับเป็น `sealed class` เพื่อปิดผนึก encapsulation ป้องกัน improper inheritance
   - ปิด Constructors ทั้งหมดเป็น `private` สำหรับ EF Core materialization
   - บังคับการสร้าง Instance ผ่าน Static Factory Methods (`Create`, `Draft`, `Register`, `CreateSuccess`, ฯลฯ) พร้อม internal `CreateForTest`
2. **Domain Invariant Guards (`Guard.cs`):**
   - รวม fail-fast validation เข้าสู่ `Guard` helper ใน Domain Common
   - โยน `DomainValidationException` ทันทีเมื่อ input ผิดเงื่อนไข ปราศจากการพึ่งพา library ภายนอก
3. **Dedicated Value Objects (13 Types):**
   - สร้าง Value Objects สืบทอดจาก `ValueObject` (หรือ `record` สำหรับ slug) ครอบคลุม:
     - Tenant/Identity: `CompanyName`, `ProjectName`, `EmailAddress`
     - Security: `ApiKeyName`, `Sha256Hash`, `ExpirationPolicy`
     - Authoring: `TemplateName`, `DatasetName`, `ConnectionName`, `DatasetAlias`, `TemplateSlug`, `DataSourceType`
     - Rendering: `DocumentReference`
   - ทุก Value Object มี structural equality ผ่าน `GetEqualityComponents()` และ implicit string conversion
4. **Smart Enums:**
   - ใช้งาน `DatabaseProvider`, `GenerationStatus`, `OutputFormat`, `RenderEngineType`, `RoleType`, `SystemRole`, `TemplateFormat`, `TemplateVersionStatus` สืบทอดจาก `Enumeration`
   - เพิ่ม `ValidationFailed` และ `FromName` helper ใน `GenerationStatus`
5. **Decouple Cross-Aggregate Navigations (Pure Id References):**
   - ตัด Domain navigation properties ข้าม Aggregate Root ออกทั้งหมด (คงไว้เฉพาะ Child entities ใน aggregate เดียวกัน เช่น `Template.Versions`, `Document.Versions`)
   - กำหนด EF Core relationship ผ่าน Fluent API ใน `AppDbContext.cs` เช่น `entity.HasOne<Template>().WithMany().HasForeignKey(e => e.TemplateId)`
6. **Zero Database Migrations (EF Core Value Converters):**
   - แมป Value Objects และ Smart Enums ทั้งหมดกลับสู่ primitive database columns เดิมผ่าน `.HasConversion(...)` ใน `AppDbContext.cs`
   - คงความเข้ากันได้ของ PostgreSQL schema เดิม 100% โดยไม่ต้องสร้าง migration ใหม่
7. **Deterministic Time Injection:**
   - ทุก Factory Method และ Business Method รองรับ optional `DateTimeOffset? now = null` เพื่อให้ Application UseCases สามารถส่ง `TimeProvider.GetUtcNow()` เข้ามาได้อย่างสมบูรณ์

**Consequences & Verification:**
- Domain Layer เป็น Pure C# POCOs 100% ไร้การพึ่งพา external dependencies
- เพิ่มชุดทดสอบ Unit Tests สำหรับ Value Objects และ Entities โดยเฉพาะ
- การทดสอบ backend และ frontend ทั้งหมดผ่าน 100% (0 errors, 0 warnings, zero tolerated failures)
- Next.js production build สำเร็จสมบูรณ์

---

## ADR-023: Strict Pure DDD Domain Entity Standards — Single Canonical Factory & Zero Test Backdoors in Production Domain (System-Wide Rollout Across All Domain Entities)

**Date:** October 2026 | **Status:** Accepted (Fully Rolled Out Across All Domain Entities & Verified)

**Context & Problem:**
- ใน ADR-022 แม้ Entity จะถูกปรับเป็น Rich Domain Model แต่ยังพบ **Architectural Smells**:
  1. **Overload Explosion & Primitive Obsession Leaking:** Entity มี Factory overloads หลายตัว ทั้งแบบรับ `(string, string)` และรับ Value Objects เพื่ออำนวยความสะดวกให้ Caller (Application UseCases / Tests) ทำให้ Domain ทำหน้าที่แปลง primitive เกินขอบเขต
  2. **Test Backdoors in Production Model:** มี `internal static CreateForTest(...)` ประกาศปะปนอยู่ในไฟล์ Entity ของ Production assembly (`SmkDoc.Domain.dll`)
  3. **Dead Code & Ambiguity:** Overload บางตัวที่สร้างขึ้นมาเพื่อแก้ขัดในอดีตไม่ได้ถูกเรียกใช้จริง และการสลับลำดับ parameter (`category` vs `now`) ทำให้เกิดความคลุมเครือ

**Decision:**
1. **Single Canonical Factory Method (SSoT):**
   - แต่ละ Entity ต้องมี **1 canonical factory method เท่านั้น** (เช่น `Create`, `Register`, `Draft`, `Issue`, หรือ specialized `CreateSuccess`/`CreateFailure` บน `GenerationLog`)
   - พารามิเตอร์ต้องเป็น **Strongly-Typed Value Objects** (เช่น `TemplateName`, `TemplateSlug`, `CompanyName`, `ProjectName`, `DatasetAlias`, `DocumentReference`, `EmailAddress`, `Sha256Hash`) และ **บังคับส่ง `DateTimeOffset now`** เพื่อความ deterministic 100%
   - **ห้ามมี Primitive Convenience Overloads** ใน Domain Entity — หน้าที่การแปลง DTO primitive เป็น Value Objects เป็นของ Application UseCases
2. **Mandatory Deterministic Time on All State Mutations:**
   - ทุกเมธอดที่เปลี่ยนสถานะ (`Activate`, `Deactivate`, `Update*`, `Publish`, `Archive`, `Assign*`, `Remove*`) ต้องรับ `DateTimeOffset now` จาก Application UseCase และเรียก `SetUpdated(now)` โดยห้าม fallback ไปยัง `UtcNow` ภายใน Domain
3. **Zero Test Backdoors in Domain:**
   - **ห้ามมี `CreateForTest` และ optional `Guid? id = null` ภายใน `SmkDoc.Domain.dll` โดยเด็ดขาด**
   - การสร้าง Entity สำหรับทดสอบต้องทำผ่าน `*Builder` หรือ `*TestFactory` ในโปรเจกต์ `SmkDoc.Tests` เท่านั้น
4. **Internal Parameterized Constructor:**
   - Parameterized constructor ของ Entity ถูกกำหนดเป็น `internal` โดยเปิดให้ `SmkDoc.Tests` เข้าถึงได้ผ่าน `[assembly: InternalsVisibleTo("SmkDoc.Tests")]` ใน `AssemblyInfo.cs`
   - Parameterless constructor เป็น `private` สำหรับ EF Core materialization เท่านั้น
5. **System-Wide Rollout (All Domain Entities & Dedicated Test Factories):**
   - **Slice 1 (Tenant & Identity):** `Company`, `Project`, `User`, `UserProjectRole`
   - **Slice 2 (Integration):** `DataConnection`, `Dataset`
   - **Slice 3 (Authoring):** `Template`, `TemplateVersion`, `FieldMapping`, `TemplateDataset`
   - **Slice 4 (Rendering & Audit):** `ApiKey`, `Document`, `DocumentVersion`, `GenerationLog`
   - สร้าง Dedicated Test Factories ภายใต้ `backend-v2/tests/SmkDoc.Tests/Common/Factories/` (`*TestFactory`) ครอบคลุมทุก Entity

**Consequences & Verification:**
- Domain Layer สะอาดหมดจด มีเพียง Ubiquitous Language และ Invariants ทางธุรกิจจริง
- Production Assembly ปราศจาก Test methods 100%
- Build `dotnet build SmkDocServerV2.slnx` สำเร็จ 0 Warnings, 0 Errors ใน application code
- Zero Database Schema Migrations — เข้ากันได้กับ EF Core mapping เดิม 100%

---

## ADR-024: Test Suite Segregation, 1:1 CQRS Parity & The 6 Clean Testing Pillars

**Status:** Accepted (2026-10-06)

**Context:**
1. **Monolithic Test Classes & Dumping Grounds:** มีไฟล์เทสต์ขนาดใหญ่ที่รวมหลาย UseCases ไว้ในคลาสเดียว (`ProjectUseCaseTests`, `ApiKeyUseCaseTests`, `DocumentVersionUseCaseTests`, `DataConnectionUseCaseTests`, `DatasetUseCaseTests`, `FieldMappingUseCaseTests`, `CommandValidatorsTests`) และไฟล์ทดสอบแบบ dumping ground (`DomainInvariantTests`, `EntityEncapsulationTests`) ทำให้ผิดหลัก Single Responsibility Principle
2. **Misplaced & Obsolete Test Classes:** ไฟล์ทดสอบ DataConnection วางปะปนในโฟลเดอร์ Datasets และเรียกใช้ Obsolete service (`DataConnectionUseCase`, `DatasetUseCase`)
3. **Mixed I/O Concerns:** ไฟล์ Document Generator และ Performance Benchmark ที่เขียนไฟล์ลงดิสก์จริงและใช้ OpenXML วางปะปนอยู่ใน `SmkDoc.Tests` ส่งผลให้เกิด Namespace Collision ระหว่าง `SmkDoc.Domain.Entities.Document` กับ `DocumentFormat.OpenXml.Wordprocessing.Document` และทำให้ Unit Test มี Disk I/O
4. **Flaky Time & Ad-hoc Instantiations:** มีการเรียกใช้ `DateTimeOffset.UtcNow` และ new Entity แบบ ad-hoc กระจัดกระจายในชุดทดสอบ

**Decision: System-Wide Adoption of The 6 Clean Testing Pillars**
1. **Pillar 1 — Solution-Level Test Segregation:**
   - **`SmkDoc.Tests` (Pure In-Memory Unit Tests):** ตัดขาดจาก I/O 100% (Zero Disk/Network/Database I/O) รันเสร็จสิ้นในระดับ 1 วินาที เหมาะสำหรับ PR CI gate
   - **`SmkDoc.IntegrationTests` (Integration, Benchmarks & Generators):** แยกสร้างโปรเจกต์ใหม่รองรับ Heavy Generators, ClosedXML/OpenXml Document generation, Performance Benchmarks, และ Integration Test Fixtures
2. **Pillar 2 — 1:1 Clean Architecture CQRS Parity & Single SUT Isolation:**
   - แตกไฟล์ Monolithic ทั้งหมดออกเป็น Single-SUT Test Classes จัดหมวดหมู่สะท้อนโครงสร้าง `src/SmkDoc.Application/` แบบ 1:1 ครบทุกโมดูล (`Authoring`, `IdentityAccess`, `Integration`, `Rendering`) โดยแยกย่อยเป็น `Commands/{CommandName}/` และ `Queries/{QueryName}/` (1 SUT ต่อ 1 ไฟล์ ห้ามเขียน Monolithic UseCase test รวมกันเด็ดขาด)
3. **Pillar 3 — Universal Roy Osherove Naming & Deterministic Baseline Time:**
   - บังคับใช้รูปแบบ `ExecuteAsync_When[Condition]_[ExpectedResult]` ทุกไฟล์เพื่อสื่อสารพฤติกรรมของ SUT อย่างแม่นยำ
   - ใช้งาน `TestConstants.BaselineTime` เป็น SSoT สำหรับค่าเวลาในการทดสอบและ assertion แทนการใช้ `DateTimeOffset.UtcNow` แบบสุ่ม
4. **Pillar 4 — Validator Colocation & Independent Pure Testing:**
   - ย้ายการทดสอบ input validator ออกจาก monolithic suite มาเป็น `*ValidatorTests.cs` ประกบคู่กับ Command/Query ใน feature folder
   - ทดสอบ input constraints ด้วย `[Theory]` + `[InlineData]` เป็น pure functions โดยปราศจาก mock ใดๆ
5. **Pillar 5 — Domain Invariant Consolidation (Aggregate Root SSoT):**
   - ลบไฟล์ dumping grounds (`DomainInvariantTests`, `EntityEncapsulationTests`) ทิ้ง และรวมการทดสอบกฎธุรกิจและการ encapsulate เข้ากับ Aggregate Root Unit Test โดยตรง (`{Aggregate}Tests.cs`)
6. **Pillar 6 — Fluent Object Mother Builders & Semantic Fixtures:**
   - สร้าง Domain Entity ผ่าน `*Builder` หรือ `*TestFactory` ร่วมกับ `TestConstants.BaselineTime`
   - สร้าง UseCase SUT ผ่าน `*TestFixture` พร้อมเมธอดกลุ่ม `Given*` เพื่อขจัด mock setup boilerplate

**Consequences & Verification:**
- แยก Unit Tests ออกจาก Real I/O เด็ดขาด เพิ่มความเร็วใน CI/CD Pipeline
- แก้ปัญหา Type Collision ของ OpenXML ใน Unit Test ได้อย่างถาวร
- โครงสร้างโฟลเดอร์ใน `tests/` สะท้อน `src/` แบบ 1:1 สม่ำเสมอทั้งระบบ
- กำจัด Test Anti-Patterns (AP-042 ถึง AP-046) ออกจาก Codebase 100%
- Invariant Quality Gate: 100% Pass Rate และ 0 Failures ทั่วทั้ง Solution (`SmkDocServerV2.slnx` และ `frontend-v2`)

---

## ADR-025: Scoped API Keys (ReadOnly / ReadWrite) & M2M-First Zero Public Surface Security

**Status:** Accepted (2026-10-08)

**Context:**
1. **Headless M2M Gateway Paradigm:** ระบบถูกออกแบบเป็น Centralized Document Generation Gateway โดยระบบธุรกิจภายนอก (ERP, CRM, Billing) เรียกใช้งานผ่าน Machine-to-Machine (M2M) ด้วย API Key เท่านั้น โดยที่ API Key ผูก 1:1 กับ ProjectId ในระบบอยู่แล้ว External System ไม่จำเป็นต้องรู้หรือเลือก ProjectId เอง
2. **Least-Privilege Scoping:** ต้องการจำกัดสิทธิ์ API Key ให้มี 2 ระดับอย่างชัดเจนคือ `ReadOnly` (สำหรับ preview, validate payload, download) และ `ReadWrite` (สำหรับ generate document, template mutations)
3. **Perimeter Security (Zero Public Attack Surface):** ป้องกันไม่ให้แฮกเกอร์หรือบอทภายนอกสแกนหรือ brute-force โจมตีเส้น `/api/v1/auth/login` โดยภายนอกจะมองไม่เห็นและไม่ได้รับ API spec ของเส้น Login เลย
4. **Frictionless Login Contract:** การมี `projectId` ใน JSON Body ของ Login ก่อให้เกิดความซ้ำซ้อนและเสี่ยงต่อ Context Mismatch ระหว่าง Header กับ Body

**Decision:**
1. **Smart Enum `ApiKeyScope` (`ReadOnly`, `ReadWrite`):**
   - ฝัง `ApiKeyScope` ลงใน `ApiKey` Aggregate Root และ persist ลงฟิลด์ `scope VARCHAR(20)` ในตาราง `api_keys`
   - กำหนด Default เป็น `ReadWrite` เพื่อความ backward-compatible
2. **Atomic Multi-Key Provisioning upon Project Creation:**
   - ใน `CreateProjectUseCase` ระบบจะสร้าง Company (ถ้ายังไม่มี), สร้าง Project, ผูกสิทธิ์ Admin ให้ผู้สร้าง และออก API Key พร้อมกัน 2 ดอกทันที (`ReadOnly` และ `ReadWrite`) ภายใต้ **Single Atomic Transaction (`CommitAsync`)** เดียว
3. **Perimeter-Gated Portal Authentication (Approach A):**
   - ถอด `/api/v1/auth/*` ออกจาก Public Whitelist ใน `ApiKeyMiddleware`
   - การเรียก `POST /api/v1/auth/login` และ `POST /api/v1/auth/refresh` ต้องแนบ `X-API-Key` (Master / SuperAdmin Key) ใน Header เสมอ หากไม่มีจะถูกตัดตอนที่ Middleware ทันทีด้วย `401 Unauthorized` (RFC 7807)
4. **Smart Dual-Channel Auth Bypass:**
   - ใน `ApiKeyMiddleware` หาก Request ใดมี Bearer JWT ที่ยืนยันตัวตนสำเร็จแล้ว (`context.User.Identity?.IsAuthenticated == true`) Middleware จะดึง `UserId` และ `ProjectId` จาก Claims มาใส่ใน `IExecutionContext` และ bypass การตรวจ `X-API-Key` ให้อัตโนมัติ ทำให้ผู้ใช้บน Web Portal / Swagger ใช้งานได้อย่างราบรื่น
5. **Zero-Input Project ID on Login:**
   - ตัดฟิลด์ `projectId` ออกจาก `LoginRequest` และ `LoginCommand` โดยเด็ดขาด
   - `LoginUseCase` จะ resolve ProjectId จาก `IExecutionContext.ProjectId` (ที่ผูกกับ API Key) หรือ Fallback ไปยัง Default Project ของ SuperAdmin โดยอัตโนมัติ

**Consequences:**
- ปิดช่องโหว่ Public Attack Surface ของเส้น Login ได้ 100%
- ป้องกันปัญหา IDOR และ Tenant Mismatch จากการส่ง `projectId` ซ้ำซ้อนใน Body
- รองรับ M2M Principle ที่โปรเจกต์ใหม่มี Key พร้อมใช้งานแยก Read/Write ทันทีที่สร้างเสร็จ

---

## ADR-026: Application Query Service (`IUserWorkspaceQueryService`), Domain Value Object Enforcement & CQRS DTO Disentanglement

**Status:** Accepted (2026-10-08)

**Context:**
1. **Multi-Roundtrip & In-Memory Join Debt (N+1 Risk):** ทั้ง `LoginUseCase` และ `GetCurrentUserProfileUseCase` เคยดึงข้อมูลผ่านหลาย Domain Repository (`UserProjectRoleRepository`, `ProjectRepository`) แล้วนำ Entity ทั้งหมดขึ้นมา Join ด้วย `Dictionary<Guid, string>` ใน RAM ซ้ำซ้อนกันกว่า 25 บรรทัด ก่อให้เกิด Memory allocation และ Multiple DB Roundtrips โดยไม่จำเป็น
2. **Domain Repository Boundary (AP-025):** ตามหลัก Clean Architecture และ DDD Domain Repositories ต้องคืนค่าเฉพาะ Domain Entities เท่านั้น ห้ามคืน DTO หรือ Projection ข้าม Aggregate
3. **Primitive Obsession:** `LoginUseCase` เคยจัดการ String เองด้วย `.Trim().ToLowerInvariant()` ทั้งที่มี Domain Value Object `EmailAddress` ที่มี Validation และ Invariants สมบูรณ์อยู่แล้ว
4. **DTO Dumping Ground:** ไฟล์ `LoginResultDto.cs` รวม DTO และ Command ไว้ถึง 10 คลาสในไฟล์เดียว รวมถึง CQRS Command (`LoginCommand`) และ Dead Property Alias (`Token => AccessToken`)
5. **Constructor Bloat:** `LoginUseCase` เคยฉีดถึง 11 Dependencies รวมทั้ง Write Repositories และ Read Repositories เข้าด้วยกัน

**Decision:**
1. **Application Query Service (`IUserWorkspaceQueryService`):**
   - นิยาม Port `IUserWorkspaceQueryService` ใน `SmkDoc.Application.Common.Interfaces`
   - Implement Adapter `UserWorkspaceQueryService` ใน `SmkDoc.Infrastructure.Persistence.Queries` โดยใช้ EF Core Linq `.Join(...)` ร่วมกับ `AsNoTracking()` เพื่อทำ Single-SQL `INNER JOIN` และ Project ข้อมูลออกมาเป็น `AccessibleProjectDto` และ `ApiKeyDto` โดยตรงจาก Database ในรอบเดียว
2. **Domain Value Object Integration (`EmailAddress`):**
   - บังคับใช้ `EmailAddress.Create(request.Email)` ใน `LoginUseCase` เพื่อให้ Domain Invariant (Format regex, Length <= 256, NotEmpty) ทำงานตั้งแต่ก้าวแรก และส่งต่อเข้า `IUserRepository.GetByEmailAsync(EmailAddress, ct)` โดยตรง
3. **CQRS & DTO Disentanglement:**
   - ย้าย `LoginCommand` ไปไว้ใน `Commands/Login/LoginCommand.cs` ตามมาตรฐาน CQRS
   - แยก DTO แต่ละตัวออกเป็นไฟล์อิสระตามหน้าที่: `AccessibleProjectDto.cs`, `UserProfileDto.cs`, `TokenResultDto.cs`, `CurrentUserProfileResultDto.cs`, `ApiKeyDto.cs`
   - ทำความสะอาด `LoginResultDto.cs` ให้เหลือเฉพาะ Response ของ Login และตัด Dead Alias `Token => AccessToken` ออก
4. **Streamline UseCase Dependencies:**
   - ลด Dependency ของ `LoginUseCase` จาก 11 ตัวเหลือเพียง 6 ตัวหลัก โดยถอด `roleRepo`, `projectRepo`, และ `apiKeyRepo` ออกทั้งหมด
   - ปรับปรุง `GetCurrentUserProfileUseCase` ให้เรียก `workspaceQueryService.GetAccessibleProjectsAsync(...)` ร่วมกัน ทำให้ขนาดโค้ดลดลงจาก 60 บรรทัดเหลือเพียง 30 บรรทัด

**Consequences & Verification:**
- ขจัดปัญหา N+1 และ In-memory Join ใน RAM ถาวร
- ลด Database chatter เหลือ 1 Single SQL Query สำหรับการดึงสิทธิ์ Workspace
- รักษา Clean Architecture DIP: Application Layer ปราศจาก EF Core/Database Leaks
- Unit Test Mock ง่ายขึ้นอย่างมีนัยสำคัญผ่าน `_workspaceQueryServiceMock` เพียงตัวเดียว
- 100% Pass Rate ทั้ง Unit Tests (648 tests) และ Integration Tests (11 tests)

