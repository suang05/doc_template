# PATTERNS.md — Canonical Architectural Patterns & Data Flow

<ai_context>
<purpose>
เอกสารฉบับนี้คือ "คัมภีร์รูปแบบสถาปัตยกรรม (Pattern Blueprint)" สำหรับระบบ smk-doc-server ทำหน้าที่เป็น Single Source of Truth สำหรับโครงสร้างการเขียนโค้ดทั้งหมด
</purpose>
<directives>
1. STRICT ENFORCEMENT: คุณต้องเขียนโค้ดตาม Pattern ที่ระบุในเอกสารนี้เท่านั้น ห้ามใช้ Pattern อื่นที่ขัดแย้ง (ห้ามเกิด Regression)
2. LEARNING FROM CONTRAST: สังเกตและหลีกเลี่ยงโค้ดในบล็อก `❌ Bad` (Anti-patterns) อย่างเด็ดขาด และเลียนแบบโค้ดในบล็อก `✅ Good` เสมอ
3. DATA FLOW AWARENESS: สถาปัตยกรรมฝั่ง Backend ถูกจัดกลุ่มตาม "วงจรชีวิตข้อมูล" (Transport -> Domain -> Payload -> Binary) คุณต้องรู้เสมอว่าโค้ดที่กำลังเขียนอยู่ใน Data Phase ไหน
</directives>
<architecture_style>
Clean Architecture, CQRS (Without MediatR), Domain-Driven Design (DDD), Fail-Fast, and Zero-LOH (Stream-over-RAM).
</architecture_style>
</ai_context>

> **Purpose:** Authoritative architectural implementation patterns for `backend-v2/` and `frontend-v2/`.  
> **Related Docs:** [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Syntax & Naming), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (Prohibited Patterns).

### ⚡ Quick-Lookup: Canonical Architectural Patterns

| Pattern | Where to Use | Key Rule |
|---|---|---|
| **Use Case Pattern** | `SmkDoc.Application/UseCases/` | `sealed class`, C# 12/13 Primary Constructor, return DTOs only |
| **Thin Controllers** | `SmkDoc.Api/Controllers/` | Receive request, delegate to UseCase, wrap in `ApiResponse<T>`, ≤ 5 lines |
| **Strategy Pattern** | `SmkDoc.Infrastructure/Engines/` | `IRenderEngine` keyed by `RenderEngineType`; zero `switch`/`if-else` |
| **Rich Domain Model** | `SmkDoc.Domain/Entities/` | Parameterized constructors, `private set`, business methods, zero DTOs |
| **Command/Query Boundary** | `SmkDoc.Application/DTOs/` | Positional records, `*Command` for mutations, `*Query` for reads, `*Dto` for results |
| **Dual Validation** | Application & Infrastructure | `FluentValidation` for Commands, `JsonSchema.Net` for dynamic payloads |
| **Unit of Work** | `IUnitOfWork` | Commit atomic DB transactions across multiple repositories |
| **Stream-over-RAM** | `IRenderEngine`, `IPdfRenderer`, `GenerateDocumentUseCase` | 100% Zero-LOH streaming; Gotenberg response stream pipes directly into MinIO/HTTP response |
| **Polly v8 Resilience** | `SmkDoc.Infrastructure/DependencyInjection.cs` | Dual-tier timeout, adaptive retry with jitter, circuit breaker for Gotenberg |

---

## Part 1: Backend Patterns (Data Flow Lifecycle)

### Phase 1: Transport Data
**เป้าหมาย:** จัดการข้อมูลที่วิ่งเข้าและออกจาก HTTP Boundary อย่างเป็นมาตรฐาน, ปลอดภัย, และรองรับสเกล

#### 1.1 Use Case Pattern (Thin Controllers & C# 12 Primary Constructors)
1. Controllers ต้อง **บาง (Thin)** — รับ Request, เรียก UseCase, return Response เท่านั้น (**≤ 5 บรรทัด**)
2. Use Cases ทั้งหมดต้องเป็น **`sealed class`** และใช้ **C# 12 Primary Constructor** เพื่อลด Boilerplate Code (ห้ามเขียน field declarations + constructor assignments แบบเดิม):

```csharp
// ✅ CORRECT — Thin Controller with Explicit Query & Command
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(ApiResponse<TemplateDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
{
    var query = new GetTemplateByIdQuery(id);
    var result = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
    return Ok(new ApiResponse<TemplateDto>(result));
}

// ✅ CORRECT — Creation Endpoint with 201 Created
[HttpPost]
[ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status201Created)]
public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
{
    var command = new CreateTemplateCommand(request.Name, request.Slug);
    var result = await createTemplateUseCase.ExecuteAsync(command, ct);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResponseDto>(result));
}

// ✅ CORRECT — Use Case with C# 12 Primary Constructor, sealed class & IUseCase<TReq, TRes>
public sealed class UpdateTemplateDetailsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    IValidator<UpdateTemplateDetailsCommand> validator) : IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>
{
    // โค้ด UseCase เริ่มทำงานได้ทันที โดยไม่มี boilerplate fields/constructors
}

// ❌ WRONG — Business logic หรือ Service/Repo ใน Controller
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id)
{
    var template = await templateRepo.GetByIdAsync(id); // ❌ ห้าม inject IRepository หรือ Service ใน Controller
    if (template == null) return NotFound();
    return Ok(template); // ❌ ห้าม return Entity โดยตรง
}
```

#### 1.2 API Envelope Pattern
1. **ทุก JSON Response ต้องห่อด้วย `ApiResponse<T>` หรือ `PagedApiResponse<T>` เสมอ:**
```csharp
// ✅ Success (single object)
return Ok(new ApiResponse<TemplateDto>(result));

// ✅ Success (created single object — 201 Created)
return StatusCode(StatusCodes.Status201Created, new ApiResponse<TemplateDto>(result));

// ✅ Success (list)
return Ok(new ApiResponse<IEnumerable<TemplateDto>>(results));

// ✅ Success (paginated)
return Ok(new PagedApiResponse<TemplateDto>(items, total, page, limit));

// ✅ Mutation without body
return NoContent();  // HTTP 204

// ❌ NEVER
return Ok(new { success = true }); // ❌ Anonymous object
return Ok(entity);   // ❌ Domain Entity ห้ามออกจาก API layer
```

2. **ข้อยกเว้นสำหรับ Binary / Media Stream:**
เมื่อส่งออกไฟล์ PDF, Excel, Word หรือ Raw HTML (`application/pdf`, `text/html`) **ต้องคืน Stream ดิบโดยตรง ไม่ต้องครอบด้วย `ApiResponse<T>`**:
```csharp
// ✅ Stream Direct Delivery
Response.Headers.ContentDisposition = "inline";
return File(pdfStream, "application/pdf");
```

#### 1.3 Global Exception Handling (RFC 9457 & DomainException)
**ห้ามใช้ try-catch ใน Controllers.** ใช้ Domain Exceptions แทน และให้ `GlobalExceptionHandler` (ที่ implement `IExceptionHandler`) แปลงเป็น RFC 9457 Problem Details พร้อม extensions `errorCode` อัตโนมัติ:

```csharp
// ✅ CORRECT — throw Domain Exception ใน UseCase
var template = await _templateRepo.GetByIdAsync(id, ct)
    ?? throw new NotFoundException($"Template '{id}' not found.");

// GlobalExceptionHandler จะแปลงเป็น:
// HTTP 404 { "type": "...", "title": "Not Found", "status": 404, "detail": "...", "errorCode": "RESOURCE_NOT_FOUND" }
```

**Exception → HTTP Status & ErrorCode Mapping:**

| Exception | HTTP Status | ErrorCode | คำอธิบาย |
|---|---|---|---|
| `NotFoundException` | 404 Not Found | `RESOURCE_NOT_FOUND` | Template, Version, Document, Key ไม่พบ |
| `ConflictException` | 409 Conflict | `RESOURCE_CONFLICT` | Slug หรือ Resource ซ้ำซ้อน |
| `DraftExpiredException` | 410 Gone | `DRAFT_EXPIRED` | RAM Draft Cache หมดอายุหรือไม่อยู่แล้ว |
| `SchemaValidationException` | 400 Bad Request | `SCHEMA_VALIDATION_FAILED` | Payload ละเมิด JSON Schema (มี `errors[]`) |
| `DomainValidationException` | 400 Bad Request | `DOMAIN_VALIDATION_ERROR` | Entity/Value Object invariant ผิด (ค่าว่าง, format ผิด, ค่าติดลบ) |
| `BusinessRuleViolationException` | 400 Bad Request (fallback `DomainException`) | เฉพาะกฎ เช่น `ARCHIVED_VERSION_CANNOT_BE_PUBLISHED`, `INACTIVE_TEMPLATE` | ผิดกฎธุรกิจ / state transition ไม่ถูกต้อง |
| `RenderException` | 500 Internal Error | `DOCUMENT_RENDER_FAILED` | Engine / Chromium / LibreOffice render ไม่ผ่าน |
| `ArgumentException` | 400 Bad Request | — | พารามิเตอร์ผิดพลาด |
| `UnauthorizedAccessException` | 401 Unauthorized | — | สิทธิ์ไม่ถูกต้อง |
| `InvalidOperationException` | 409 Conflict | — | ทำงานผิด State / Validation |
| `Exception` (fallback) | 500 Internal Error | — | Unhandled Technical Errors (Log warning) |

#### 1.4 ApiKeyMiddleware Pattern
```
Request → ApiKeyMiddleware
  → Skip: /, /health, /swagger/*, /api/documents/preview
  → Check X-API-Key header → 401 RFC 9457 if missing
  → Check MASTER_API_KEY env → bypass (bootstrapping)
  → ApiKeyUseCase.ValidateKeyAsync(rawKey) → 401 RFC 9457 if invalid
  → Set ExecutionContext.CallerApp, ApiKeyId → next middleware
```

#### 1.5 OpenTelemetry & Real-time Metrics (APM) Pattern
เพื่อการมอนิเตอร์และวิเคราะห์สมรรถนะของเอนจินเรนเดอร์เอกสารแบบเรียลไทม์ตามมาตรฐาน Cloud-Native APM (OpenTelemetry, Prometheus, Datadog):
- **Vendor-Neutral Abstraction (`IDocumentMetrics`):**
  - ประกาศ Port ใน `SmkDoc.Application/Common/Interfaces/IDocumentMetrics.cs` ทำให้ Application Layer ไม่ผูกติดกับ SDK ของคลาวด์หรือแพ็กเกจ Monitoring ใดๆ
- **Zero-Dependency Native .NET 10 Observability:**
  - พัฒนาใน `SmkDoc.Infrastructure/Observability/DocumentMetrics.cs` โดยใช้คลาสแกน `System.Diagnostics.Metrics.Meter` (`"SmkDoc.DocumentEngine"`) และ `System.Diagnostics.ActivitySource`
  - มีคุณสมบัติ $O(1)$ Zero-Allocation เมื่อไม่มี APM Listener หรือ Metric Collector เชื่อมต่ออยู่
- **Core Standard Instruments & Tags:**
  - `document.render.duration` (`Histogram<double>`, seconds): วัดเวลาเรนเดอร์เอกสาร พร้อม Tag มิติ
  - `document.render.total` (`Counter<long>`): นับจำนวนงานเรนเดอร์เอกสารทั้งหมด
  - `document.render.bytes` (`Counter<long>`): นับผลรวมขนาดไบต์ของเอกสารที่ผลิตได้
  - `template.cache.requests` (`Counter<long>`): นับการเรียกใช้งาน Template Cache
- **Pipeline Integration:**
  - ฝังใน `DocumentAuditService` บันทึก execution duration และ payload size
  - ฝังใน `MemoryCompiledTemplateCache` บันทึก cache hit/miss ratio

```csharp
// ใน DocumentAuditService.cs:
metrics?.RecordRenderDuration(renderDurationSeconds, engineType, outputFormat, isSuccess);
metrics?.RecordRenderTotal(engineType, outputFormat, isSuccess);
if (isSuccess && fileSizeBytes > 0)
{
    metrics?.RecordRenderBytes(fileSizeBytes, engineType, outputFormat);
}

// ใน MemoryCompiledTemplateCache.cs:
metrics?.RecordCacheRequest(cacheHit: true);
```

### Phase 2: Domain Data
**เป้าหมาย:** ปกป้องกฎเกณฑ์ทางธุรกิจ (Business Rules) ให้อยู่ในสถานะที่ถูกต้องเสมอ (Always-Valid State)

#### 2.1 Rich Domain Model Pattern (Canonical Pure DDD — ADR-023)
```csharp
// ✅ CORRECT — Usage: Canonical Factory Method with Value Objects & Deterministic Time
var template = Template.Create(projectId, TemplateName.Create("Invoice"), TemplateSlug.Create("invoice"), "Finance", now);
template.SetCurrentVersion(versionId, now);

// ❌ WRONG — Object Initializer / overriding identity / primitive overload
var template = new Template { Name = "Invoice", IsActive = true }; // ห้าม (AP-008, AP-021)
var template = Template.Create(projectId, "Invoice", "invoice", now); // ❌ ห้าม Primitive Overload ใน Domain (AP-039)
var version  = new TemplateVersion(...) { Id = someId };           // ❌ ห้าม (AP-008, AP-021)
```

```csharp
// ✅ CORRECT — Entity shape (Strict Pure DDD Reference Model)
public sealed class Template : BaseEntity, IMustHaveProject
{
    private readonly List<TemplateVersion> _versions = [];       // child entities (aggregate-owned)

    public Guid ProjectId { get; private set; }                  // cross-aggregate ref = Id only
    public TemplateName Name { get; private set; } = null!;      // Value Object, not string
    public TemplateSlug Slug { get; private set; } = null!;      // Value Object, not string
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();

    private Template() { }                                       // EF Core materialization only

    // Internal constructor accessible to Test Factories/Builders via InternalsVisibleTo
    internal Template(Guid? id, Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now)
        : base(id, createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        IsActive = true;
    }

    // Single Canonical Factory Method (SSoT)
    public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
        new(null, projectId, name, slug, category, now);

    public void SetCurrentVersion(Guid versionId, DateTimeOffset now)
    {
        Guard.NotEmpty(versionId, nameof(versionId));
        if (!IsActive)
            throw new BusinessRuleViolationException("Cannot assign a current version to an inactive template.", "INACTIVE_TEMPLATE");
        if (_versions.Count > 0 && !_versions.Any(v => v.Id == versionId))
            throw new VersionNotInTemplateException(Id, versionId);

        CurrentVersionId = versionId;
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;                                   // idempotent
        IsActive = false;
        SetUpdated(now);
    }
}
```

**Value Object (self-validating, value equality):**
```csharp
public sealed partial record TemplateSlug
{
    public string Value { get; }
    public TemplateSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainValidationException("Slug cannot be empty.");
        var normalized = value.Trim().ToLowerInvariant();
        if (!SlugRegex().IsMatch(normalized)) throw new DomainValidationException($"Slug '{value}' is invalid.");
        Value = normalized;
    }
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugRegex();
}
```

**Repository contract (Domain.Interfaces):**
```csharp
public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Template?> GetBySlugAsync(Guid projectId, TemplateSlug slug, CancellationToken ct = default); // tenant-scoped
    Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);  // read-only, no IQueryable
    Task AddAsync(Template template, CancellationToken ct = default);
    void Remove(Template template);
}
```

> [!NOTE]
> รูปแบบข้างบนคือ **Canonical Repository Pattern** ที่ระบบใช้งานจริง (สอดคล้องกับ ADR-021): aggregate-scoped, tenant-scoped, คืน `IReadOnlyList<T>` เท่านั้น และไม่มีการรั่วไหล `IQueryable` ออกมา

#### 2.2 Unit of Work Pattern (Atomic Transactions)
```csharp
// ✅ CORRECT — หลาย operations ใน 1 transaction
await templateRepo.AddAsync(template, ct);
await unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 1 (generate ID)

await versionRepo.AddAsync(version, ct);
await unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 2

template.SetCurrentVersion(version.Id);
templateRepo.Update(template);
await unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 3 (link version)
```

#### 2.3 High-Performance Smart Enum Pattern
หลีกเลี่ยงการใช้ `enum` ธรรมดาของ C# ที่พึ่งพา Reflection ซึ่งทำให้ช้า Smart Enums (`TemplateFormat`, `OutputFormat`, `RenderEngineType`, `RoleType`, `TemplateVersionStatus`, `GenerationStatus`) สืบทอดจาก `Enumeration`:
- ใช้ Generic Static Type Caching (`Cache<T>`) เพื่อ inspect reflection เพียงครั้งเดียวต่อ generic type
- Lookup ด้วย `FromValue<T>()` และ `FromDisplayName<T>()` เป็น **$O(1)$** ผ่าน Pre-computed Dictionaries
- มี `TryFromValue<T>()` และ `TryFromDisplayName<T>()` เพื่อหลีกเลี่ยงการ throw Exception เมื่อตรวจจับค่าไม่ถูกต้อง

```csharp
// ✅ CORRECT — ใช้ Smart Enum properties
string ext = version.FileFormat?.Extension ?? ".html";       // ".docx"
string mime = version.FileFormat?.MimeType ?? "text/html";   // "application/vnd.openxmlformats..."

// ✅ CORRECT — เปรียบเทียบ
if (version.FileFormat == TemplateFormat.Html) { ... }

// ❌ WRONG — ใช้ string magic values
if (version.FileFormat == "html") { ... }
if (ext == ".html") { ... }  // ใช้ TemplateFormat.Html.Extension แทน
```

#### 2.4 UUIDv7 Entity Pattern (Time-Ordered Primary Key)
ทุก Entity ที่สืบทอดจาก `BaseEntity` จะใช้ `Guid.CreateVersion7()` อัตโนมัติ (.NET 10):
- เรียงลำดับตาม Timestamp ทางกายภาพใน B-Tree Index ของ PostgreSQL
- กำจัดปัญหา B-Tree Page Splits และ Index Fragmentation เมื่อตาราง Audit Log / Document Version มีข้อมูลหลักล้านเรคคอร์ด
- **กฎเหล็ก:** ห้าม override ด้วย `Id = Guid.NewGuid()` ใน Use Cases หรือที่ใดๆ ในระบบ

### Phase 3: Template Payload Data
**เป้าหมาย:** ตรวจสอบความถูกต้องและแปลงรูปแบบข้อมูล (JSON) ก่อนส่งให้ Engine เรนเดอร์

#### 3.1 DTO Boundary & Command Pattern (Clean Architecture v2)
โครงสร้าง DTO และคำสั่งใน `SmkDoc.Application/DTOs/<Feature>/`:
```
Presentation  → HTTP Request Model (มี validation attributes) → Map ไปยัง Application Command/Query
Application   → รับ Command/Query → ประมวลผล UseCase → คืน Immutable DTO / ResultDto (ห้ามคืน Entity เด็ดขาด)
Domain        → Rich Domain Entity มี invariants และ business methods (ห้ามมี DTO หรือ Request/Response)
```

**กฎเหล็ก 5 ข้อของ DTOs และ Vertical Slice ใน Application Layer:**
1. **100% Immutable Positional Records:** ทุก DTO ต้องเป็น `public record ...` (ห้ามใช้ mutable class `{ get; set; }`)
2. **แยกแยะบทบาทชัดเจน (Command vs Query vs ResultDto):**
   - Mutation Inputs: ลงท้ายด้วย `*Command` อยู่ใน `Commands/<Action>/`
   - Read/Search Inputs: ลงท้ายด้วย `*Query` อยู่ใน `Queries/<Action>/`
   - Outputs: ลงท้ายด้วย `*ResultDto` หรือ `*Dto`
3. **ห้าม Domain Entity รั่วไหลออกนอก UseCase:**
   - ❌ `public async Task<ApiKey?> ValidateKeyAsync(...)`
   - ✅ `public async Task<ValidatedApiKeyDto?> ValidateKeyAsync(...)`
4. **ห้ามมี Presentation Attributes ใน Application DTOs:**
   - ❌ `using System.ComponentModel.DataAnnotations; [Required] string Html` (ย้ายไปไว้ที่ Presentation Layer ใน `SmkDoc.Api/Contracts/`)
5. **จัดโฟลเดอร์ตาม Single-Intent Vertical Slices:**
   - Colocate UseCase ไว้กับ Command/Query ในโฟลเดอร์เดียวกัน (เช่น `Commands/CreateTemplate/CreateTemplateUseCase.cs`)
   - Decoupled Request Models อยู่ใน `SmkDoc.Api/Contracts/<Module>/` แยกขาดจาก Application Layer

```csharp
// ✅ CORRECT — UseCase รับ Command และคืน Application DTO
public async Task<TemplateVersionDto> GetVersionAsync(Guid id, CancellationToken ct)
{
    var version = await versionRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException(...);
    return new TemplateVersionDto(version.Id, version.TemplateId, version.Version, ...);
}

// ❌ WRONG — UseCase คืน Domain Entity ออกไปสู่ชั้น Controller หรือ Middleware
public async Task<TemplateVersion> GetVersionAsync(Guid id) // ห้าม
public async Task<ApiKey?> ValidateKeyAsync(string key)      // ห้าม (ต้องคืน ValidatedApiKeyDto)
```

#### 3.2 Dual-Engine Automatic Validation Pipeline Pattern
ระบบแบ่งการตรวจสอบข้อมูลออกเป็น 2 กลไก (Static C# vs. Dynamic Document Schema):

1. **Static Command Validation (FluentValidation):**
   - เขียน Validator ใน `SmkDoc.Application/Validators/<Feature>/` สืบทอดจาก `AbstractValidator<TCommand>`
   - ลงทะเบียนอัตโนมัติด้วย `services.AddValidatorsFromAssemblyContaining<...>()`
   - ถูกดักจับและ execute อัตโนมัติที่ชั้น Presentation (API) ผ่าน **`ValidateCommandFilter` (Action Filter)** ก่อนถึง UseCase (Fail-Fast)
   - หากไม่ผ่าน โยน `ValidationException` (`SmkDoc.Domain.Exceptions`) และแปลงเป็น HTTP 400 Bad Request ผ่าน `GlobalExceptionHandler`
2. **Dynamic Document Payload Validation (JsonSchema.Net Draft-07):**
   - ตรวจสอบความถูกต้องของ JSON Payload เทียบกับ Template Schema ผ่าน `IJsonSchemaValidationService` ใน Use Case
   - รองรับการข้ามการตรวจสอบเมื่อ `command.SkipValidation == true` เพื่อรองรับ High-throughput batch workloads

```csharp
// ✅ CORRECT — FluentValidation สำหรับ C# Command DTO
public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 150);
        RuleFor(x => x.Slug).NotEmpty().Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

// ✅ CORRECT — ตรวจสอบ Fail-Fast Dynamic Payload ผ่าน IDocumentDataPreparationService ก่อนแตะ Storage/MinIO
var preparedData = await dataPreparationService.PrepareDataAsync(
    template, currentVersion, request.Data, request.SkipValidation, ct);

if (preparedData.ValidationResult is { IsValid: false })
{
    await auditService.LogValidationFailureAsync(
        template.Id, currentVersion.Id, preparedData.DataJson, outputFormat, (int)sw.ElapsedMilliseconds, errorMessage, ct);
    throw new SchemaValidationException(slug, currentVersion.Version, preparedData.ValidationResult.Errors);
}
```

> **Performance Note:** `JsonSchemaValidationService` ใน Infrastructure มีการทำ Thread-safe In-Memory Caching (`ConcurrentDictionary`) เพื่อหลีกเลี่ยงการ parse Schema string ซ้ำซ้อน และใช้ `static readonly EvaluationOptions` เพื่อประสิทธิภาพสูงสุด

#### 3.3 Thai Data Formatting (SSoT)
**SSoT:** `SmkDoc.Application.Common.Helpers.ThaiDataTransformer` — ห้ามเขียน Thai formatting logic ที่อื่น

```csharp
// ✅ CORRECT
ThaiDataTransformer.ToThaiBahtText(2500000m);  // "สองล้านห้าแสนบาทถ้วน"
ThaiDataTransformer.FormatThaiDate("2026-09-15"); // "15 กันยายน 2569"
ThaiDataTransformer.FormatPhone("0812345678");   // "081-234-5678"
ThaiDataTransformer.FormatPhone("021234567");    // "02-123-4567" (9 digits)
ThaiDataTransformer.FormatThaiIdCard("1234567890123"); // "1-2345-67890-12-3"

// Transform by string key (used by template engine)
ThaiDataTransformer.Transform(value, "baht");   // → ToThaiBahtText
ThaiDataTransformer.Transform(value, "date");   // → FormatThaiDate
```

### Phase 4: Binary & Stream Data
**เป้าหมาย:** ประมวลผลและส่งออกไฟล์ขนาดใหญ่ (PDF, Word) โดยไม่ทำให้ Server กินแรม (OOM)

#### 4.1 Zero-LOH Direct Streaming Pipeline Pattern (Stream-over-RAM)
เพื่อรองรับการประมวลผลเอกสาร PDF/Office หลายพันหน้าพร้อมกันโดยไม่เกิด Out-Of-Memory (OOM) หรือ Large Object Heap (LOH) Fragmentation:
- **Hard Invariant:** ห้ามพักไฟล์ PDF / Docx / Excel ขนาดหลายสิบเมกะไบต์ลงใน Memory Buffer (`byte[]`)
- **Port:** `IRenderEngine.RenderStreamAsync(...)` และ `IPdfRenderer.RenderHtmlToPdfStreamAsync(...)` / `RenderOfficeToPdfStreamAsync(...)`
- **Adapter (Infrastructure):** `GotenbergPdfRenderer` ดึง Network Stream ตรงจาก `HttpResponseMessage.Content.ReadAsStreamAsync()` โดยไม่ buffer ลงแรม
- **Pipelined Storage Upload:** ใน `GenerateDocumentUseCase` สตรีมจาก Engine จะถูกส่งผ่านตรงเข้า `IStorageService.UploadAsync(..., Stream data, ...)` ซึ่ง MinIO client รองรับ dynamic stream upload (`length = -1` หรือ `stream.Length`)
- **Stateless Direct Stream Response:** ใน Preview และ Stateless Render (`PreviewDocumentUseCase`, `RenderStatelessDocumentUseCase`), `DocumentController` จะ return `File(pdfStream, "application/pdf")` สู่ HTTP response โดยตรงแบบ zero-copy
- **Backward Compatibility:** เมธอด legacy `RenderAsync` จะ delegate เข้าสู่ `RenderStreamAsync` พร้อม fallback อัตโนมัติ

```csharp
// ✅ CORRECT — Zero-LOH Stream Pipeline ใน GenerateDocumentUseCase
using var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
await using var outputStream = await engine.RenderStreamAsync(templateStream, preparedData.DataJson, outputFormat, ct);

long fileSizeBytes = outputStream.CanSeek ? outputStream.Length : 0;
await storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

// ❌ WRONG — Buffer multi-MB byte[] ลงใน LOH
byte[] outputBytes = await engine.RenderAsync(...);
using var outputStream = new MemoryStream(outputBytes);
```

#### 4.2 Strategy Pattern (Template Engines)
```csharp
// ✅ CORRECT — ใช้ IEnumerable<IRenderEngine> + LINQ
var engine = engines.FirstOrDefault(e => e.EngineType == format.RenderEngineType)
    ?? throw new InvalidOperationException($"No engine registered for {format.RenderEngineType}");
await engine.RenderAsync(stream, payload, outputFormat, ct);

// ❌ WRONG — if/else หรือ switch บน engine type
if (format == "html") { new HtmlTemplateEngine()... }
else if (format == "docx") { new DocxTemplateEngine()... }
```

#### 4.3 Compiled Template Caching Pattern
เพื่อป้องกันการ Parse AST และ Compile Handlebars Delegate ซ้ำซ้อนในทุก HTTP Request:
- **Port (Application):** `ICompiledTemplateCache` นิยาม `Func<object, string> GetOrAdd(...)` แบบ Pure C# ไม่ผูกกับ Handlebars NuGet
- **Adapter (Infrastructure):** `MemoryCompiledTemplateCache` ใช้ `IMemoryCache` กำหนด Sliding Expiration 1 ชม.
- **Key Strategy:** คำนวณ SHA-256 Hash จาก Normalized Template Content ป้องกัน Invalidation bug เมื่อเนื้อหาเทมเพลตเปลี่ยน

```csharp
// ใน HtmlTemplateEngine:
string cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHtml)));
var evaluate = templateCache.GetOrAdd(cacheKey, () =>
{
    var compiled = handlebars.Compile(normalizedHtml);
    return data => compiled(data);
});
string rendered = evaluate(dataHierarchy);
```

#### 4.4 Polly v8 Resilience Pipeline Pattern
เพื่อป้องกันปัญหา Cascading Failures, Socket Exhaustion และ Chromium/LibreOffice Hang เมื่อมีภาระงานเรนเดอร์เอกสารพร้อมกันเป็นจำนวนมาก:
- **Dual-Tier Timeout Protection:**
  - `AttemptTimeout`: 30s ป้องกัน Chromium Worker ค้าง socket เมื่อเจอ recursive DOM หรือ Infinite CSS
  - `TotalRequestTimeout`: 60s ครอบคลุมรอบการ Retry ทั้งหมด
- **Adaptive Selective Retry with Jitter:**
  - Retry สูงสุด 3 ครั้งแบบ Exponential Backoff พร้อม Full Jitter
  - **Selective Filter:** Retry เฉพาะ Transient Network / Socket Exceptions และ HTTP 503/504
  - **Bypass 4xx Client Errors:** ไม่ Retry เมื่อเกิด HTTP 400 Bad Request, 422 Unprocessable หรือ Template/Schema Error
- **Circuit Breaker Strategy:**
  - ตัดวงจร (Trip to Open) เมื่อเกิด Transient Failure เกิน 50% (Minimum throughput: 5 requests ใน Sampling window 30s)
  - พักวงจร (Break duration) 15s เพื่อเปิดโอกาสให้ Gotenberg Pod/Container ฟื้นตัว โดยไม่สะสม connection ค้างใน API Gateway

```csharp
// ใน DependencyInjection.cs:
services.AddHttpClient<IPdfRenderer, GotenbergPdfRenderer>(...)
    .AddResilienceHandler("gotenberg-resilience", builder =>
    {
        builder.AddTimeout(TimeSpan.FromSeconds(60));
        builder.AddRetry(new HttpRetryStrategyOptions { ... });
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { ... });
        builder.AddTimeout(TimeSpan.FromSeconds(30));
    });
```

### Phase 5: Readability, Simplicity & Clean Testing Standards
**เป้าหมาย:** โค้ดต้องอ่านง่าย แบนราบ ลดภาระสมอง (Cognitive Load) ปราศจาก Side Effects แอบแฝง และมีความน่าเชื่อถือจากการทดสอบ

#### 5.1 Guard Clauses (Early Return)
ลดการใช้ `if-else` ซ้อนกันหลายชั้น (Arrow Code) เพื่อบังคับกระบวนการ Fail-Fast และทำให้โค้ดอ่านจากบนลงล่างได้ราบรื่น

```csharp
// ❌ Bad: Nested Ifs (Arrow Code) ทำให้อ่านยากและพลาดเงื่อนไขง่าย
public async Task ProcessAsync(Payload data) 
{
    if (data != null) {
        if (data.IsValid) {
            // Do work... (Indent ลึกไปเรื่อยๆ)
        }
    }
}

// ✅ Good: Guard Clauses ตัดจบแต่เนิ่นๆ (Fail-Fast)
public async Task ProcessAsync(Payload data) 
{
    ArgumentNullException.ThrowIfNull(data);
    if (!data.IsValid) throw new ValidationException("Invalid payload");
    
    // Do work... (บรรทัดหลักไม่ต้องมี Indent ลึก)
}
```

#### 5.2 Explicit Variable Instantiation
ห้ามประกาศ `new object()` ซ้อนไว้ใน arguments ของ method (Nested Instantiation) ให้ประกาศตัวแปรโลคัลที่ชัดเจนเสมอ เพื่อให้ง่ายต่อการตั้ง Breakpoint และอ่านโค้ด

```csharp
// ❌ Bad: Nested instantiation ซ่อนอยู่ในวงเล็บ อ่านยากและ Debug ลำบาก
var result = await useCase.ExecuteAsync(new CreateTemplateCommand(request.Name, request.Slug), ct);

// ✅ Good: แยกตัวแปรชัดเจน
var command = new CreateTemplateCommand(request.Name, request.Slug);
var result = await useCase.ExecuteAsync(command, ct);
```

#### 5.3 Explicit Dependency over Service Locator
ต้องระบุ Dependency ไว้ที่ Primary Constructor ให้เห็นชัดเจนเท่านั้น ห้ามแอบไปดึง Service กลางอากาศ และ **ห้ามใช้ `_` นำหน้าชื่อตัวแปรใน Primary Constructor** (ใช้ `camelCase` บริสุทธิ์ตามกฎ C# 12)

```csharp
// ❌ Bad: Service Locator (Hidden Side-effect) หรือประกาศ field ซ้ำซ้อน
public class GenerateDocumentUseCase(IRenderEngine engine) 
{
    private readonly IRenderEngine _engine = engine; // ❌ ห้ามประกาศซ้ำ (AP-015)
    public void Render() 
    {
        var cache = HttpContext.RequestServices.GetService<ICache>(); // ❌ ห้ามซ่อน Service Locator
    }
}

// ✅ Good: C# 12 Primary Constructor Injection (Explicit, no underscores)
public class GenerateDocumentUseCase(IRenderEngine engine) 
{
    public void Render() { engine.Render(...); }
}
```

#### 5.4 Test Architecture & Clean Testing Standards
โครงสร้างการทดสอบแบ่งเป็น **2 โปรเจกต์คู่ขนาน** ในระดับ Solution:
1. **`SmkDoc.Tests` (Pure In-Memory Unit Tests):** ตัดขาดจาก I/O 100% (Zero Disk/Network/Database), รันในระดับ 1 วินาที เหมาะสำหรับ PR CI gate, และ **Mirror โครงสร้าง `src/SmkDoc.Application/` แบบ 1:1 CQRS Parity** (`Commands/{CommandName}/` และ `Queries/{QueryName}/`)
2. **`SmkDoc.IntegrationTests` (Integration, Benchmarks & Generators):** แยกจัดเก็บ Generators, Performance Benchmarks, OpenXml/ClosedXML File Writers, และ Container Fixtures (`Fixtures/`, `Repositories/`, `Storage/`, `Generators/`, `Benchmarks/`)

#### 📌 Implementation Standards & Archetypes (SSoT Delegation)
รายละเอียดโค้ดและพิมพ์เขียวการเขียน Unit Test ทั้งหมด ถูกกำหนดเป็นมาตรฐานเดียวที่ [docs/AI/CODING_CONVENTIONS.md §2.7](CODING_CONVENTIONS.md#27--unit-testing-standards--the-golden-archetypes):
- **Archetype A (UseCase SUT):** Direct Mocks (`_repoMock`), SSoT `CreateSut()` factory, and clean 3-A invocation (ห้าม `var sut = ...`)
- **Archetype B (Input Validator):** Pure function parameterization ผ่าน `[Theory]` + `[InlineData]` โดยปราศจาก mock
- **Archetype C (Domain Aggregate):** Business invariants และ state mutations ภายใน Aggregate Root โดยตรง
- **Prohibited Patterns:** รายการข้อห้ามและ Anti-patterns ทั้งหมดถูกรวบรวมไว้ที่ [docs/AI/ANTI-PATTERNS.md](ANTI-PATTERNS.md) (AP-042 ถึง AP-048)

#### 🧪 Test Verification & CI/CD Commands
```bash
# 1. Fast PR Gate — 100% In-Memory Unit Tests (~1.0s, Zero I/O)
dotnet test tests/SmkDoc.Tests/SmkDoc.Tests.csproj

# 2. Heavy Validation / Release Pipeline — Generators & Benchmarks
dotnet test tests/SmkDoc.IntegrationTests/SmkDoc.IntegrationTests.csproj

# 3. Full Solution Verification
dotnet test SmkDocServerV2.slnx
```

---

## Part 2: Frontend Patterns (Next.js 15)

| Pattern | ไฟล์ | หน้าที่ |
|---|---|---|
| **Design Tokens SSoT** | `frontend-v2/src/tokens/index.ts` | สี, ขนาด, ช่องว่าง — ห้าม Hardcode |
| **Zod Schemas** | `frontend-v2/src/schemas/` | Validate API Request/Response — ห้ามใช้ `any` |
| **API Adapters** | `frontend-v2/src/lib/api/` | fetch wrappers — Component ห้าม fetch ตรงๆ |
| **RFC 9457 Problem Details** | `frontend-v2/src/lib/api/client.ts` | ถอดรหัส `errorCode`, `detail`, `errors` เป็น strongly-typed `ApiError` |
| **Race-Condition-Free Preview** | `frontend-v2/src/hooks/useLivePreview.ts`, `useFormPreview.ts` | ยกเลิก HTTP request เก่าด้วย `AbortController` เมื่อพิมพ์เร็ว |
| **Custom Hooks** | `frontend-v2/src/hooks/` | ซ่อน Business Logic + Data fetching |
| **Atomic UI** | `frontend-v2/src/components/ui/` | Reusable building blocks ห้ามเขียนซ้ำ |

### 2.1 AbortController & Live Preview APM Telemetry Pattern

เพื่อป้องกันการเกิด Race Condition เมื่อผู้ใช้แก้ไขแม่แบบใน Monaco Editor หรือเปลี่ยนข้อมูลฟอร์มอย่างรวดเร็ว:
- **In-flight Request Abort:** ทุกครั้งที่เริ่มการเรนเดอร์ใหม่ จะสั่ง `abortControllerRef.current.abort()` ทันทีเพื่อยกเลิกคำขอเดิมที่ค้างอยู่ใน Network Layer
- **Silent Ignore on Abort:** ตรวจสอบ `if (err.name === 'AbortError' || controller.signal.aborted) return;` เพื่อไม่แสดง Error Banner หลอกตาผู้ใช้
- **Client-Side Telemetry Bench:** วัด Round-trip Latency ด้วย `performance.now()` และขนาด Payload (`blob.size`) ส่งต่อไปแสดงผลที่ Badge บน Preview Toolbar (เช่น `⚡ 182ms · 34.2 KB`)

### 2.2 RFC 9457 Client Diagnostic Pattern

เมื่อ Backend ส่ง Error ตอบกลับมาตามมาตรฐาน RFC 9457 (`application/problem+json`):
- `parseErrorPayload` ถอดรหัส `type`, `title`, `detail`, `errorCode`, และ dictionary รายการข้อผิดพลาด `errors`
- สกัดและจัดเก็บลงใน `ApiError.problemDetails`
- UI แสดงผล Error Code badge (เช่น `[TEMPLATE_SYNTAX_ERROR]`) และ Diagnostic Drawer ระบุบรรทัด/ฟิลด์ที่ผิดพลาดอย่างชัดเจนแทนข้อความ error ทั่วไป

---

## Part 3: UI/UX Patterns

- **Split-Screen over Modals:** ฟอร์มที่ซับซ้อน → Split-screen (ซ้ายฟอร์ม / ขวาพรีวิว) ไม่ใช่ Modal ขนาดใหญ่
- **Primary Action:** ปุ่มหลักอยู่มุมขวาล่างของ Card เสมอ
- **Overflow Menu:** `⋮` Dropdown อยู่มุมบนขวาของ Card (ไม่ล้นจอ)
- **Geometry:** Border radius 2–4px เท่านั้น (ไม่ใช้ `rounded-full` หรือ `rounded-2xl`)
