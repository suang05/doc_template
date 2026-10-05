# PATTERNS.md — Canonical Architectural Patterns

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

## 1. Backend Patterns (C# / Clean Architecture)

### 1.1 Use Case Pattern (Thin Controllers & C# 12 Primary Constructors)

1. Controllers ต้อง **บาง (Thin)** — รับ Request, เรียก UseCase, return Response เท่านั้น (**≤ 5 บรรทัด**)
2. Use Cases ทั้งหมดต้องเป็น **`sealed class`** และใช้ **C# 12 Primary Constructor** เพื่อลด Boilerplate Code (ห้ามเขียน field declarations + constructor assignments แบบเดิม):

```csharp
// ✅ CORRECT — Thin Controller
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(ApiResponse<TemplateDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
{
    var dto = await getTemplateByIdUseCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
    return Ok(new ApiResponse<TemplateDto>(dto));
}

// ✅ CORRECT — Creation Endpoint with 201 Created
[HttpPost]
[ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status201Created)]
public async Task<IActionResult> Create([FromBody] CreateTemplateRequest req, CancellationToken ct)
{
    var result = await createTemplateUseCase.ExecuteAsync(new CreateTemplateCommand(req.Name, req.Slug), ct);
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
    var template = await _templateRepo.GetByIdAsync(id); // ❌ ห้าม inject IRepository หรือ Service ใน Controller
    if (template == null) return NotFound();
    return Ok(template); // ❌ ห้าม return Entity โดยตรง
}
```

### 1.2 API Envelope Pattern

1. **ทุก JSON Response ต้องห่อด้วย `ApiResponse<T>` หรือ `PagedApiResponse<T>` เสมอ:**
```csharp
// ✅ Success (single object)
return Ok(new ApiResponse<TemplateDto>(dto));

// ✅ Success (created single object — 201 Created)
return StatusCode(StatusCodes.Status201Created, new ApiResponse<TemplateDto>(dto));

// ✅ Success (list)
return Ok(new ApiResponse<IEnumerable<TemplateDto>>(dtos));

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

### 1.3 Global Exception Handling (RFC 7807 & DomainException)

**ห้ามใช้ try-catch ใน Controllers.** ใช้ Domain Exceptions แทน และให้ `GlobalExceptionFilter` แปลงเป็น RFC 7807 Problem Details พร้อม extensions `errorCode` อัตโนมัติ:

```csharp
// ✅ CORRECT — throw Domain Exception ใน UseCase
var template = await _templateRepo.GetByIdAsync(id, ct)
    ?? throw new NotFoundException($"Template '{id}' not found.");

// GlobalExceptionFilter จะแปลงเป็น:
// HTTP 404 { "title": "Not Found", "status": 404, "detail": "...", "errorCode": "RESOURCE_NOT_FOUND" }
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

### 1.4 Rich Domain Model Pattern (Canonical — Domain Layer)

```csharp
// ✅ CORRECT — Usage: Parameterized Constructor + Business Methods
var template = new Template(projectId, "Invoice", new TemplateSlug("invoice"), "Finance");
template.SetCurrentVersion(versionId);

// ❌ WRONG — Object Initializer / overriding identity
var template = new Template { Name = "Invoice", IsActive = true };
var version  = new TemplateVersion(...) { Id = someId };   // ห้าม (AP-008, AP-021)
```

```csharp
// ✅ CORRECT — Entity shape
public class Template : BaseEntity, IMustHaveProject
{
    private readonly List<TemplateVersion> _versions = [];       // child entities (aggregate-owned)

    public Guid ProjectId { get; private set; }                  // cross-aggregate ref = Id only
    public string Name { get; private set; } = string.Empty;
    public TemplateSlug Slug { get; private set; } = null!;      // Value Object, not string
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();

    private Template() { }                                       // EF Core materialization only

    public Template(Guid projectId, string name, TemplateSlug slug, string? category = null)
    {
        if (projectId == Guid.Empty) throw new DomainValidationException("ProjectId cannot be empty.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Template name cannot be empty.");

        ProjectId = projectId;
        Name = name.Trim();
        Slug = slug ?? throw new DomainValidationException("Slug is required.");
        IsActive = true;
    }

    public void SetCurrentVersion(Guid versionId)
    {
        if (!IsActive)
            throw new BusinessRuleViolationException("Template is inactive.", "INACTIVE_TEMPLATE");
        if (_versions.All(v => v.Id != versionId))
            throw new BusinessRuleViolationException("Version does not belong to this template.", "VERSION_NOT_IN_TEMPLATE");

        CurrentVersionId = versionId;
        SetUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive) return;                                   // idempotent
        IsActive = false;
        SetUpdated();
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

### 1.5 Smart Enum Pattern

Smart Enums สืบทอดจาก `Enumeration` base class และมี Behavior:

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

### 1.6 DTO Boundary & Command Pattern (Clean Architecture v2)

โครงสร้าง DTO และคำสั่งใน `SmkDoc.Application/DTOs/<Feature>/`:
```
Presentation  → HTTP Request Model (มี validation attributes) → Map ไปยัง Application Command/Query
Application   → รับ Command/Query → ประมวลผล UseCase → คืน Immutable DTO / ResultDto (ห้ามคืน Entity เด็ดขาด)
Domain        → Rich Domain Entity มี invariants และ business methods (ห้ามมี DTO หรือ Request/Response)
```

**กฎเหล็ก 5 ข้อของ DTOs และ Vertical Slice ใน Application Layer:**
1. **100% Immutable Positional Records:** ทุก DTO ต้องเป็น `public record ...` (ห้ามใช้ mutable class `{ get; set; }`)
2. **แยกแยะบทบาทชัดเจน (Command vs Query vs ResultDto):**
   - Mutation Inputs: ลงท้ายด้วย `*Command` อยู่ใน `Commands/<Action>/` (เช่น `GenerateDocumentCommand`, `CreateTemplateCommand`)
   - Read/Search Inputs: ลงท้ายด้วย `*Query` อยู่ใน `Queries/<Action>/` (เช่น `PreviewDocumentQuery`, `ValidateTemplatePayloadQuery`)
   - Outputs: ลงท้ายด้วย `*ResultDto` หรือ `*Dto` (เช่น `TemplateResultDto`, `DocumentVersionDto`, `GenerateDocumentResultDto`)
3. **ห้าม Domain Entity รั่วไหลออกนอก UseCase:**
   - ❌ `public async Task<ApiKey?> ValidateKeyAsync(...)`
   - ✅ `public async Task<ValidatedApiKeyDto?> ValidateKeyAsync(...)`
4. **ห้ามมี Presentation Attributes ใน Application DTOs:**
   - ❌ `using System.ComponentModel.DataAnnotations; [Required] string Html` (ย้ายไปไว้ที่ Presentation Layer ใน `SmkDoc.Api/Contracts/`)
5. **จัดโฟลเดอร์ตาม Single-Intent Vertical Slices:**
   - Colocate UseCase ไว้กับ Command/Query ในโฟลเดอร์เดียวกัน (เช่น `Commands/CreateTemplate/CreateTemplateUseCase.cs`, `Queries/PreviewDocument/PreviewDocumentUseCase.cs`)
   - Decoupled Request Models อยู่ใน `SmkDoc.Api/Contracts/<Module>/` แยกขาดจาก Application Layer

```csharp
// ✅ CORRECT — UseCase รับ Command และคืน Application DTO
public async Task<TemplateVersionDto> GetVersionAsync(Guid id, CancellationToken ct)
{
    var version = await _versionRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException(...);
    return new TemplateVersionDto(version.Id, version.TemplateId, version.Version, ...);
}

// ❌ WRONG — UseCase คืน Domain Entity ออกไปสู่ชั้น Controller หรือ Middleware
public async Task<TemplateVersion> GetVersionAsync(Guid id) // ห้าม
public async Task<ApiKey?> ValidateKeyAsync(string key)      // ห้าม (ต้องคืน ValidatedApiKeyDto)
```

### 1.7 Dual-Engine Automatic Validation Pipeline Pattern

ระบบแบ่งการตรวจสอบข้อมูลออกเป็น 2 กลไก (Static C# vs. Dynamic Document Schema):

1. **Static Command Validation (FluentValidation v11):**
   - เขียน Validator ใน `SmkDoc.Application/Validators/<Feature>/` สืบทอดจาก `AbstractValidator<TCommand>`
   - ลงทะเบียนอัตโนมัติด้วย `services.AddValidatorsFromAssemblyContaining<...>()`
   - ถูกดักจับและ execute อัตโนมัติในระดับ Presentation ด้วย `ValidateCommandFilter` ก่อนถึง Action
   - หากไม่ผ่าน โยน `ValidationException` (`SmkDoc.Domain.Exceptions`) และแปลงเป็น HTTP 400 Bad Request ผ่าน `GlobalExceptionFilter`
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
var preparedData = await _dataPreparationService.PrepareDataAsync(
    template, currentVersion, request.Data, request.SkipValidation, ct);

if (preparedData.ValidationResult is { IsValid: false })
{
    await _auditService.LogValidationFailureAsync(
        template.Id, currentVersion.Id, preparedData.DataJson, outputFormat, (int)sw.ElapsedMilliseconds, errorMessage, ct);
    throw new SchemaValidationException(slug, currentVersion.Version, preparedData.ValidationResult.Errors);
}
```

> **Performance Note:** `JsonSchemaValidationService` ใน Infrastructure มีการทำ Thread-safe In-Memory Caching (`ConcurrentDictionary`) เพื่อหลีกเลี่ยงการ parse Schema string ซ้ำซ้อน และใช้ `static readonly EvaluationOptions` เพื่อประสิทธิภาพสูงสุด


### 1.8 Strategy Pattern (Template Engines)

```csharp
// ✅ CORRECT — ใช้ IEnumerable<IRenderEngine> + LINQ
var engine = _engines.FirstOrDefault(e => e.EngineType == format.RenderEngineType)
    ?? throw new InvalidOperationException($"No engine registered for {format.RenderEngineType}");
await engine.RenderAsync(stream, payload, outputFormat, ct);

// ❌ WRONG — if/else หรือ switch บน engine type
if (format == "html") { new HtmlTemplateEngine()... }
else if (format == "docx") { new DocxTemplateEngine()... }
```

### 1.8 Unit of Work Pattern (Atomic Transactions)

```csharp
// ✅ CORRECT — หลาย operations ใน 1 transaction
await _templateRepo.AddAsync(template, ct);
await _unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 1 (generate ID)

await _versionRepo.AddAsync(version, ct);
await _unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 2

template.SetCurrentVersion(version.Id);
_templateRepo.Update(template);
await _unitOfWork.CommitAsync(ct); // Commit ครั้งที่ 3 (link version)
```

### 1.9 Thai Data Formatting (SSoT)

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

### 1.10 Test Architecture & Clean Testing Standards (SmkDoc.Tests)

โครงสร้างการทดสอบต้อง **Mirror เลเยอร์ของ Clean Architecture 1:1** (`Domain/`, `Application/`, `Infrastructure/`, `Api/`, `Integration/`, `Common/`)

#### 1. Mocking Interface Only
```csharp
// ✅ CORRECT — Mock interface ไม่ใช่ concrete class
private readonly Mock<IRepository<Template>> _mockTemplateRepo = new();
private readonly Mock<IStorageService> _mockStorage = new();

// Setup + Verify
_mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
    .ReturnsAsync(template);

_mockStorage.Verify(s => s.UploadAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
    It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
```

#### 2. FluentAssertions Single Source of Truth (SSoT)
**ห้ามใช้ xUnit `Assert.*` ในเทสต์ใหม่** ให้ใช้ FluentAssertions 100%:
```csharp
// ✅ CORRECT — FluentAssertions SSoT
result.Should().NotBeNull();
result.AccessToken.Should().Be("expected_token");
result.AccessibleProjects.Should().ContainSingle();
result.AccessibleProjects.Should().AllSatisfy(p => p.Role.Should().Be("Admin"));

// Exception Testing Pattern
var act = () => _useCase.ExecuteAsync(request);
await act.Should().ThrowAsync<UnauthorizedAccessException>();

// ❌ WRONG — Legacy Assertions
Assert.NotNull(result);
Assert.Equal("expected_token", result.AccessToken);
await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ...);
```

#### 3. Test Data Builder Pattern & Fixtures (`Common/Builders/`)
ใช้ Fluent Builders แทนการสร้าง Entity แบบ ad-hoc เพื่อลดความเปราะบางของ Constructor:
```csharp
// ✅ CORRECT — ใช้ TemplateBuilder & TemplateVersionBuilder
var template = new TemplateBuilder()
    .WithId(templateId)
    .WithName("Sale Contract")
    .WithSlug("sale-contract")
    .WithCurrentVersion(versionId)
    .Build();

var inactiveTemplate = new TemplateBuilder().AsInactive().WithSlug("inactive-tpl").Build();
```
สำหรับ Use Case ในโมดูล Rendering (เช่น `GenerateDocumentUseCase`) ให้ใช้ `GenerateDocumentTestFixture` ใน `Common/Fixtures/` ซึ่งประกอบด้วย Collaborator Services (`IDocumentDataPreparationService`, `IDocumentAuditService`, `IDocumentVersioningService`) ไว้เรียบร้อยแล้วเพื่อลด mock boilerplate

#### 4. Test Categorization ([Trait]) & Pure Tests
- **Pure Unit Tests:** ปราศจาก Side-Effects ไม่เขียนไฟล์ลง Git workspace (`docs/`)
- **Traits:**
  - `[Trait("Category", "Benchmark")]` สำหรับ Performance / Throughput Tests
  - `[Trait("Category", "Generator")]` สำหรับ Sample File Generators
- **CI/CD Command:**
  ```bash
  dotnet test --filter "Category!=Benchmark&Category!=Generator"
  ```

### 1.11 ApiKeyMiddleware Pattern

```
Request → ApiKeyMiddleware
  → Skip: /, /health, /swagger/*, /api/documents/preview
  → Check X-API-Key header → 401 RFC 7807 if missing
  → Check MASTER_API_KEY env → bypass (bootstrapping)
  → ApiKeyUseCase.ValidateKeyAsync(rawKey) → 401 RFC 7807 if invalid
  → Set ExecutionContext.CallerApp, ApiKeyId → next middleware
```

### 1.12 High-Performance Smart Enum & UUIDv7 Entity Pattern

#### Smart Enum with Zero Reflection Overhead
Smart Enums (`TemplateFormat`, `OutputFormat`, `RenderEngineType`, `RoleType`, `TemplateVersionStatus`, `GenerationStatus`) สืบทอดจาก `Enumeration`:
- ใช้ Generic Static Type Caching (`Cache<T>`) เพื่อ inspect reflection เพียงครั้งเดียวต่อ generic type
- Lookup ด้วย `FromValue<T>()` และ `FromDisplayName<T>()` เป็น **$O(1)$** ผ่าน Pre-computed Dictionaries
- มี `TryFromValue<T>()` และ `TryFromDisplayName<T>()` เพื่อหลีกเลี่ยงการ throw Exception เมื่อตรวจจับค่าไม่ถูกต้อง

#### UUIDv7 (Time-Ordered Primary Key)
ทุก Entity ที่สืบทอดจาก `BaseEntity` จะใช้ `Guid.CreateVersion7()` อัตโนมัติ (.NET 10):
- เรียงลำดับตาม Timestamp ทางกายภาพใน B-Tree Index ของ PostgreSQL
- กำจัดปัญหา B-Tree Page Splits และ Index Fragmentation เมื่อตาราง Audit Log / Document Version มีข้อมูลหลักล้านเรคคอร์ด
- **กฎเหล็ก:** ห้าม override ด้วย `Id = Guid.NewGuid()` ใน Use Cases หรือที่ใดๆ ในระบบ

### 1.13 Compiled Template Caching Pattern

เพื่อป้องกันการ Parse AST และ Compile Handlebars Delegate ซ้ำซ้อนในทุก HTTP Request:
- **Port (Application):** `ICompiledTemplateCache` นิยาม `Func<object, string> GetOrAdd(...)` แบบ Pure C# ไม่ผูกกับ Handlebars NuGet
- **Adapter (Infrastructure):** `MemoryCompiledTemplateCache` ใช้ `IMemoryCache` กำหนด Sliding Expiration 1 ชม.
- **Key Strategy:** คำนวณ SHA-256 Hash จาก Normalized Template Content ป้องกัน Invalidation bug เมื่อเนื้อหาเทมเพลตเปลี่ยน

```csharp
// ใน HtmlTemplateEngine:
string cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHtml)));
var evaluate = _templateCache.GetOrAdd(cacheKey, () =>
{
    var compiled = _handlebars.Compile(normalizedHtml);
    return data => compiled(data);
});
string rendered = evaluate(dataHierarchy);
```

### 1.14 Zero-LOH Direct Streaming Pipeline Pattern (Stream-over-RAM)

เพื่อรองรับการประมวลผลเอกสาร PDF/Office หลายพันหน้าพร้อมกันโดยไม่เกิด Out-Of-Memory (OOM) หรือ Large Object Heap (LOH) Fragmentation:
- **Hard Invariant:** ห้ามพักไฟล์ PDF / Docx / Excel ขนาดหลายสิบเมกะไบต์ลงใน Memory Buffer (`byte[]`)
- **Port:** `IRenderEngine.RenderStreamAsync(...)` และ `IPdfRenderer.RenderHtmlToPdfStreamAsync(...)` / `RenderOfficeToPdfStreamAsync(...)`
- **Adapter (Infrastructure):** `GotenbergPdfRenderer` ดึง Network Stream ตรงจาก `HttpResponseMessage.Content.ReadAsStreamAsync()` โดยไม่ buffer ลงแรม
- **Pipelined Storage Upload:** ใน `GenerateDocumentUseCase` สตรีมจาก Engine จะถูกส่งผ่านตรงเข้า `IStorageService.UploadAsync(..., Stream data, ...)` ซึ่ง MinIO client รองรับ dynamic stream upload (`length = -1` หรือ `stream.Length`)
- **Stateless Direct Stream Response:** ใน Preview และ Stateless Render (`PreviewDocumentUseCase`, `RenderStatelessDocumentUseCase`), `DocumentController` จะ return `File(pdfStream, "application/pdf")` สู่ HTTP response โดยตรงแบบ zero-copy
- **Backward Compatibility:** เมธอด legacy `RenderAsync` จะ delegate เข้าสู่ `RenderStreamAsync` พร้อม fallback อัตโนมัติ

```csharp
// ✅ CORRECT — Zero-LOH Stream Pipeline ใน GenerateDocumentUseCase
using var templateStream = await _storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
await using var outputStream = await engine.RenderStreamAsync(templateStream, preparedData.DataJson, outputFormat, ct);

long fileSizeBytes = outputStream.CanSeek ? outputStream.Length : 0;
await _storageService.UploadAsync(StorageBuckets.Outputs, outputKey, outputStream, contentType, ct);

// ❌ WRONG — Buffer multi-MB byte[] ลงใน LOH
byte[] outputBytes = await engine.RenderAsync(...);
using var outputStream = new MemoryStream(outputBytes);
```

### 1.15 Polly v8 Resilience Pipeline Pattern

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

### 1.16 OpenTelemetry & Real-time Metrics (APM) Pattern

เพื่อการมอนิเตอร์และวิเคราะห์สมรรถนะของเอนจินเรนเดอร์เอกสารแบบเรียลไทม์ตามมาตรฐาน Cloud-Native APM (OpenTelemetry, Prometheus, Datadog):
- **Vendor-Neutral Abstraction (`IDocumentMetrics`):**
  - ประกาศ Port ใน `SmkDoc.Application/Common/Interfaces/IDocumentMetrics.cs` ทำให้ Application Layer ไม่ผูกติดกับ SDK ของคลาวด์หรือแพ็กเกจ Monitoring ใดๆ
- **Zero-Dependency Native .NET 10 Observability:**
  - พัฒนาใน `SmkDoc.Infrastructure/Observability/DocumentMetrics.cs` โดยใช้คลาสแกน `System.Diagnostics.Metrics.Meter` (`"SmkDoc.DocumentEngine"`) และ `System.Diagnostics.ActivitySource`
  - มีคุณสมบัติ $O(1)$ Zero-Allocation เมื่อไม่มี APM Listener หรือ Metric Collector เชื่อมต่ออยู่
- **Core Standard Instruments & Tags:**
  - `document.render.duration` (`Histogram<double>`, seconds): วัดเวลาเรนเดอร์เอกสาร พร้อม Tag มิติ:
    - `engine_type` (Html, Word, Excel)
    - `output_format` (Pdf, Docx, Xlsx)
    - `status` (Success, Error)
  - `document.render.total` (`Counter<long>`): นับจำนวนงานเรนเดอร์เอกสารทั้งหมด
  - `document.render.bytes` (`Counter<long>`): นับผลรวมขนาดไบต์ของเอกสารที่ผลิตได้
  - `template.cache.requests` (`Counter<long>`): นับการเรียกใช้งาน Template Cache พร้อม Tag:
    - `cache_hit` (`true` หรือ `false`)
- **Pipeline Integration:**
  - ฝังใน `DocumentAuditService` บันทึก execution duration และ payload size
  - ฝังใน `MemoryCompiledTemplateCache` บันทึก cache hit/miss ratio

```csharp
// ใน DocumentAuditService.cs:
_metrics?.RecordRenderDuration(renderDurationSeconds, engineType, outputFormat, isSuccess);
_metrics?.RecordRenderTotal(engineType, outputFormat, isSuccess);
if (isSuccess && fileSizeBytes > 0)
{
    _metrics?.RecordRenderBytes(fileSizeBytes, engineType, outputFormat);
}

// ใน MemoryCompiledTemplateCache.cs:
_metrics?.RecordCacheRequest(cacheHit: true);
```

---



## 2. Frontend Patterns (Next.js 15)

| Pattern | ไฟล์ | หน้าที่ |
|---|---|---|
| **Design Tokens SSoT** | `frontend-v2/src/tokens/index.ts` | สี, ขนาด, ช่องว่าง — ห้าม Hardcode |
| **Zod Schemas** | `frontend-v2/src/schemas/` | Validate API Request/Response — ห้ามใช้ `any` |
| **API Adapters** | `frontend-v2/src/lib/api/` | fetch wrappers — Component ห้าม fetch ตรงๆ |
| **RFC 7807 Problem Details** | `frontend-v2/src/lib/api/client.ts` | ถอดรหัส `errorCode`, `detail`, `errors` เป็น strongly-typed `ApiError` |
| **Race-Condition-Free Preview** | `frontend-v2/src/hooks/useLivePreview.ts`, `useFormPreview.ts` | ยกเลิก HTTP request เก่าด้วย `AbortController` เมื่อพิมพ์เร็ว |
| **Custom Hooks** | `frontend-v2/src/hooks/` | ซ่อน Business Logic + Data fetching |
| **Atomic UI** | `frontend-v2/src/components/ui/` | Reusable building blocks ห้ามเขียนซ้ำ |

### 2.1 AbortController & Live Preview APM Telemetry Pattern

เพื่อป้องกันการเกิด Race Condition เมื่อผู้ใช้แก้ไขแม่แบบใน Monaco Editor หรือเปลี่ยนข้อมูลฟอร์มอย่างรวดเร็ว:
- **In-flight Request Abort:** ทุกครั้งที่เริ่มการเรนเดอร์ใหม่ จะสั่ง `abortControllerRef.current.abort()` ทันทีเพื่อยกเลิกคำขอเดิมที่ค้างอยู่ใน Network Layer
- **Silent Ignore on Abort:** ตรวจสอบ `if (err.name === 'AbortError' || controller.signal.aborted) return;` เพื่อไม่แสดง Error Banner หลอกตาผู้ใช้
- **Client-Side Telemetry Bench:** วัด Round-trip Latency ด้วย `performance.now()` และขนาด Payload (`blob.size`) ส่งต่อไปแสดงผลที่ Badge บน Preview Toolbar (เช่น `⚡ 182ms · 34.2 KB`)

### 2.2 RFC 7807 Client Diagnostic Pattern

เมื่อ Backend ส่ง Error ตอบกลับมาตามมาตรฐาน RFC 7807 (`application/problem+json`):
- `parseErrorPayload` ถอดรหัส `title`, `detail`, `errorCode`, และ dictionary รายการข้อผิดพลาด `errors`
- สกัดและจัดเก็บลงใน `ApiError.problemDetails`
- UI แสดงผล Error Code badge (เช่น `[TEMPLATE_SYNTAX_ERROR]`) และ Diagnostic Drawer ระบุบรรทัด/ฟิลด์ที่ผิดพลาดอย่างชัดเจนแทนข้อความ error ทั่วไป

---

## 3. UI/UX Patterns

- **Split-Screen over Modals:** ฟอร์มที่ซับซ้อน → Split-screen (ซ้ายฟอร์ม / ขวาพรีวิว) ไม่ใช่ Modal ขนาดใหญ่
- **Primary Action:** ปุ่มหลักอยู่มุมขวาล่างของ Card เสมอ
- **Overflow Menu:** `⋮` Dropdown อยู่มุมบนขวาของ Card (ไม่ล้นจอ)
- **Geometry:** Border radius 2–4px เท่านั้น (ไม่ใช้ `rounded-full` หรือ `rounded-2xl`)
