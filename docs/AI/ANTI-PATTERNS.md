# ANTI-PATTERNS.md — Prohibited Patterns & Pitfalls

> **Purpose:** Explicit inventory of prohibited code patterns in `backend-v2/` and `frontend-v2/` with correct alternatives.  
> **Related Docs:** [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Syntax & Standards), [PATTERNS.md](PATTERNS.md) (Canonical Patterns).

### ⚡ Quick-Lookup: Prohibited Patterns (What NOT to Do)

| Code | Prohibited Pattern | Correct Alternative |
|---|---|---|
| **AP-001** | `IRepository<T>` in Controller | Inject specific Single-Responsibility UseCase into Controller |
| **AP-002** | Return Domain Entity from UseCase | Map to Application DTO record |
| **AP-003** | Anonymous objects in API Response | Wrap in `ApiResponse<T>` / `ApiPagedResponse<T>` |
| **AP-004** | `try-catch` blocks in Controllers | Throw DomainException; let `GlobalExceptionFilter` map to RFC 7807 |
| **AP-005** | Import `AppDbContext` in Application | Depend strictly on `IRepository<T>` and `IUnitOfWork` |
| **AP-006** | `switch` or `if/else` on `RenderEngineType` | Strategy Pattern via DI (`IEnumerable<IRenderEngine>`) |
| **AP-007** | `IFormFile` or `HttpContext` in UseCase | Pass pure C# primitives (`Stream`, `string`, `Guid`) |
| **AP-008** | Object initializers `{ get; set; }` for Entities | Parameterized constructor + business methods (Rich Domain Model) |
| **AP-009** | String comparison for Smart Enums | Compare typed Smart Enum instances (e.g., `TemplateFormat.Html`) |
| **AP-010** | Hardcoded `{{}}` regex | `PlaceholderHelper.Pattern` (SSoT) |
| **AP-011** | DrawingML Id = 0 | Use positive integer `(uint)counter + 1` |
| **AP-012** | String-replace on MinIO Presigned URLs | Set `PublicEndpoint` in `MinioSettings` |
| **AP-013** | Auto-execute Docker commands | Print command snippet for user to execute |
| **AP-014** | Modify legacy v1 directories | Edit exclusively in `backend-v2/` and `frontend-v2/` |
| **AP-015** | Throw `KeyNotFoundException` or generic `Exception` | Throw strongly-typed `NotFoundException`, `ConflictException`, etc. |
| **AP-016** | Mutable classes or DataAnnotations in DTOs | 100% immutable positional `record` types |
| **AP-017** | Inline DTOs inside UseCase files | Place DTOs in `SmkDoc.Application/DTOs/<Feature>/` |
| **AP-018** | Multi-Method Fat Use Case class | 1 Intent = 1 Class implementing `IUseCase<TRequest, TResponse>` |
| **AP-019** | Throwing System Exceptions in UseCase | Throw strongly-typed Domain Exceptions (`UnauthorizedException`, etc.) |
| **AP-020** | Generic `IRepository<T>` when Aggregate Repository exists | Use explicit domain repository (`IUserRepository`, `ITemplateRepository`) |
| **AP-021** | Override identity `new X(...) { Id = ... }` | Let `BaseEntity` generate UUIDv7; pass Id via ctor/factory only if domain requires |
| **AP-022** | Public mutable collections `ICollection<T>` on entities | `private readonly List<T>` + `IReadOnlyCollection<T>` + Aggregate Root methods |
| **AP-023** | Weakening an invariant to make tests/UseCases pass | Fix the caller (UseCase/test builder); invariants are non-negotiable |
| **AP-024** | `ArgumentException`/`InvalidOperationException` in Domain | `DomainValidationException` / `BusinessRuleViolationException` |
| **AP-025** | Repository returning `IQueryable`, DTO, or mutable `List<T>`; unscoped tenant queries | Return Entity / `IReadOnlyList<T>`; require `projectId` on tenant-owned lookups |
| **AP-026** | Request inherits from Command / Shallow empty DTO subclasses | Decouple Presentation Request records; explicit mapping in Controller; return Application DTOs directly |
| **AP-027** | Imperative Role check in Controller Action (`User.RequireAdmin()`) | Declarative `[Authorize(Roles = "Admin")]` on Controller/Action (returns 403 Forbidden properly) |
| **AP-028** | Raw primitive scalar in Request Body (`[FromBody] bool isActive`) | Positional Record Request DTO (`[FromBody] SetUserStatusRequest req`) |
| **AP-029** | `CreatedAtAction` pointing to collection endpoint (`ListProjects`) | Point to single-item `GetById` with entity route param, or return `StatusCode(201, ...)` |
| **AP-030** | Unscoped Tenant Mutation (IDOR Vulnerability) | Always pass and validate tenant context (`projectId`) along with entity ID |
| **AP-031** | Unversioned or duplicated routes | Strict canonical routes `api/v1/{resource}` or `api/v1/management/projects/{projectId}/{resource}` |
| **AP-032** | Incorrect HTTP status codes | 201 Created for creation, 204 NoContent for delete/empty mutation, 200 OK for reads |
| **AP-033** | Direct service injection in Controller | Inject single-responsibility UseCases only |
| **AP-034** | Dual routing attributes on Controller | Single canonical route prefix per controller |
| **AP-035** | Ad-hoc error payloads in Controller | Throw domain exceptions; RFC 7807 via `GlobalExceptionFilter` |
| **AP-036** | Nested inline instantiation in `ExecuteAsync` | Declare explicit local variable (`var command = ...`) before calling `ExecuteAsync` |
| **AP-037** | Inline fully-qualified namespaces | Clean top-level usings only (Zero inline namespaces) |
| **AP-038** | Underscore prefix or generic names in Primary Ctor | Standardized `camelCase` 1:1 mirroring dependency class/interface |
| **AP-039** | Primitive overloads & optional timestamp fallback in Domain | Single Canonical Factory taking strongly-typed Value Objects + mandatory deterministic `DateTimeOffset now` |
| **AP-040** | `CreateForTest` or test backdoors in `SmkDoc.Domain.dll` | Factory/Builder strictly in `SmkDoc.Tests/Common/Factories/` via `internal` constructor |
| **AP-041** | Hardcoding high-churn execution metrics in docs (e.g. "614 tests", "17 controllers") | Use invariant-driven quality gates (100% pass rate, 0 failures, Bounded Contexts) |
| **AP-042** | Monolithic UseCase test classes or flat CQRS placement | 1:1 CQRS Folder Parity (`Commands/{Command}/` & `Queries/{Query}/`), single SUT per file |
| **AP-043** | Heavy I/O, Generators, or Benchmarks in Unit Tests (`SmkDoc.Tests`) | Pure in-memory unit tests in `SmkDoc.Tests`; I/O and benchmarks in `SmkDoc.IntegrationTests` |
| **AP-044** | Ad-hoc entity instantiation or nondeterministic `UtcNow` in tests | Use `*Builder` / `*TestFactory` with `TestConstants.BaselineTime` |
| **AP-045** | Artificial dumping grounds for invariant tests (`DomainInvariantTests`) | Test invariants directly in Aggregate Root unit tests (`{Aggregate}Tests.cs`) |
| **AP-046** | Mocking dependencies in Validator Tests | Test validators as pure functions with `[Theory]` + `[InlineData]` |
| **AP-F001** | Hardcoded colors/styles in components | Use system design tokens (`bg-surface`, `text-textPrimary`) |
| **AP-F002** | Raw `fetch()` in components | Custom hooks wrapping `apiClient<T>` |
| **AP-F003** | `any` types in TypeScript | Zod schema validation + inferred types |
| **AP-F004** | Legacy `.eslintrc.json` | Modern Flat Config `eslint.config.mjs` |

---

## 🔴 Backend Anti-Patterns

### AP-001: ห้าม Inject `IRepository<T>` ลงใน Controller โดยตรง
```csharp
// ❌ WRONG
public class TemplateController(IRepository<Template> repo) : ControllerBase { }

// ✅ CORRECT — Inject Single-Responsibility UseCase
public class TemplateController(CreateTemplateUseCase createUseCase, GetTemplateByIdUseCase getUseCase) : ControllerBase { }
```

### AP-002: ห้าม return Domain Entity จาก UseCase สู่ Controller หรือ Middleware
```csharp
// ❌ WRONG — คืน Entity ออกไปสู่ชั้น Presentation
public async Task<Template> GetAsync(Guid id) { ... return template; }
public async Task<ApiKey?> ValidateKeyAsync(string key) { ... return apiKey; } // Middleware ห้ามจับ Entity
public IActionResult Get() { return Ok(template); }

// ✅ CORRECT — Map เป็น Application DTO ก่อนคืนเสมอ
public async Task<TemplateDto> GetAsync(Guid id) { ... return new TemplateDto(...); }
public async Task<ValidatedApiKeyDto?> ValidateKeyAsync(string key) { ... return new ValidatedApiKeyDto(key.Id, key.CallerApp, key.ProjectId); }
public IActionResult Get() { return Ok(new ApiResponse<TemplateDto>(dto)); }
```

### AP-003: ห้ามใช้ Anonymous Objects ใน API Response
```csharp
// ❌ WRONG
return Ok(new { success = true, data = template });
return Ok(new { error = "Not found" });

// ✅ CORRECT
return Ok(new ApiResponse<TemplateDto>(dto));
return NoContent();
// (Exceptions ให้ GlobalExceptionFilter จัดการ)
```

### AP-004: ห้ามใช้ try-catch ใน Controllers
```csharp
// ❌ WRONG
try { var dto = await _useCase.GetAsync(id, ct); return Ok(...); }
catch (NotFoundException) { return NotFound(); }

// ✅ CORRECT — throw Domain Exception ใน UseCase แล้วให้ GlobalExceptionFilter จัดการ
var dto = await _useCase.GetAsync(id, ct); // UseCase throws NotFoundException internally
return Ok(new ApiResponse<TemplateDto>(dto));
```

### AP-005: ห้าม import `AppDbContext` ใน Application Layer
```csharp
// ❌ WRONG — Application layer พึ่งพา Infrastructure (Dependency Inversion violation)
using SmkDoc.Infrastructure.Persistence;
public class MyUseCase(AppDbContext db) { }

// ✅ CORRECT — พึ่งพา Interface เท่านั้น
public class MyUseCase(IRepository<Template> templateRepo, IUnitOfWork unitOfWork) { }
```

### AP-006: ห้ามใช้ if-else / switch บน Engine Type
```csharp
// ❌ WRONG — Strategy Pattern violation
IRenderEngine engine;
if (format == "html") engine = new HtmlTemplateEngine(...);
else if (format == "docx") engine = new DocxTemplateEngine(...);

// ✅ CORRECT — Strategy Pattern via DI
var engine = _engines.First(e => e.EngineType == format.RenderEngineType);
```

### AP-007: ห้าม pass `IFormFile` หรือ `HttpContext` เข้า UseCase
```csharp
// ❌ WRONG — Web framework leaks into Application layer
public class TemplateManagementUseCase(IHttpContextAccessor httpContext) { }
public async Task CreateAsync(IFormFile file) { }

// ✅ CORRECT — pass pure types
public async Task CreateAsync(Stream fileStream, string fileName, CancellationToken ct) { }
```

### AP-008: ห้ามใช้ Object Initializer สร้าง Domain Entity
```csharp
// ❌ WRONG — Anemic Domain Model, bypasses business rules
var version = new TemplateVersion { TemplateId = id, Status = "Published" };

// ✅ CORRECT — Parameterized Constructor + Business Method
var version = new TemplateVersion(templateId, 1, storageKey, TemplateFormat.Html, "author", "Initial");
version.Publish();
```

### AP-009: ห้ามใช้ string สำหรับ Smart Enum comparison
```csharp
// ❌ WRONG
if (version.FileFormat == "html") { }
if (version.Status == "Published") { }

// ✅ CORRECT
if (version.FileFormat == TemplateFormat.Html) { }
if (version.Status == TemplateVersionStatus.Published) { }
```

### AP-010: ห้ามใช้ Magic String สำหรับ Placeholder Regex
```csharp
// ❌ WRONG — Duplicate regex, DRY violation
var matches = Regex.Matches(content, @"\{\{(\w+)\}\}");

// ✅ CORRECT — SSoT
var matches = PlaceholderHelper.Pattern.Matches(content);
```

### AP-011: ห้ามตั้งค่า DrawingML Id = 0
```csharp
// ❌ WRONG — Microsoft Word Desktop จะปฏิเสธ node นี้
new Pic.NonVisualDrawingProperties { Id = 0, ... }

// ✅ CORRECT — Id ต้องเป็น positive integer เสมอ
new Pic.NonVisualDrawingProperties { Id = (uint)imageCounter + 1, ... }
```

### AP-012: ห้ามแก้ไขหรือ string-replace Presigned URLs
```csharp
// ❌ WRONG — Signature ของ MinIO จะ invalid ทันที
var url = presignedUrl.Replace("minio:9000", "localhost:9000");

// ✅ CORRECT — ใช้ PublicEndpoint สำหรับ Signing (config ใน MinioSettings)
_options.PublicEndpoint = "http://localhost:9000";
```

### AP-013: ห้ามรัน Docker Commands โดยอัตโนมัติ
Docker operations ทั้งหมด (เช่น `docker compose up`) ต้องแสดง command แล้วให้ User รันเอง ห้าม AI รันแทน

### AP-014: ห้ามแตะไฟล์ใน `frontend/` หรือ `backend/` (V1 Legacy)
การพัฒนาทั้งหมดต้องอยู่ใน `frontend-v2/` และ `backend-v2/` เท่านั้น

### AP-015: ห้าม throw `KeyNotFoundException` หรือ Generic Exception ใน Use Cases
```csharp
// ❌ WRONG — KeyNotFoundException ไม่ใช่ DomainException จะทำให้หลุดไปเป็น HTTP 500
?? throw new KeyNotFoundException($"Template '{id}' not found.");
throw new InvalidOperationException("Template slug is already in use.");

// ✅ CORRECT — throw Domain Exception ที่มี StatusCode & ErrorCode ตรงตัว
?? throw new NotFoundException($"Template '{id}' not found.");
throw ConflictException.DuplicateSlug(request.Slug);
```

### AP-012: ห้ามใช้ Mutable Class หรือ DataAnnotations ใน Application DTOs
```csharp
// ❌ WRONG — ใช้ mutable class และใส่ DataAnnotations ของ HTTP ในชั้น Application
using System.ComponentModel.DataAnnotations;
public class HtmlToPdfRequest 
{
    [Required] public string Html { get; set; }
}

// ✅ CORRECT — 100% Immutable record, validation จัดการใน Presentation หรือ Domain
public record HtmlToPdfCommand(string Html, string? HeaderHtml = null, string? FooterHtml = null);
```

### AP-013: ห้ามประกาศ DTOs ฝังอยู่ในไฟล์ UseCase (Inline Declarations)
```csharp
// ❌ WRONG — ประกาศ DTO ในไฟล์ UseCase ทำให้กระจัดกระจายและเกิด namespace clashing
public record ApiKeyDto(...);
public class ApiKeyUseCase { ... }

// ✅ CORRECT — จัดกลุ่มไว้ใน SmkDoc.Application.DTOs.<Feature>/
// เช่น DTOs/Security/SecurityDtos.cs, DTOs/Documents/DocumentDtos.cs
```

### AP-018: ห้ามสร้าง Multi-Method Use Case (Fat Service Class)
```csharp
// ❌ WRONG — รวม Create, List, Revoke ไว้ในคลาสเดียว ละเมิด SRP
public class ApiKeyUseCase 
{
    public Task<ApiKeyDto> CreateAsync(...) { ... }
    public Task<List<ApiKeyDto>> ListAsync(...) { ... }
    public Task RevokeAsync(...) { ... }
}

// ✅ CORRECT — แยก 1 Action = 1 Class implementing IUseCase<TRequest, TResponse>
public sealed class CreateApiKeyUseCase(
    IRepository<ApiKey> apiKeyRepo,
    IUnitOfWork uow) : IUseCase<CreateApiKeyCommand, CreateApiKeyResult>
{
    public async Task<CreateApiKeyResult> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct) { ... }
}

public sealed class ListApiKeysUseCase(
    IRepository<ApiKey> apiKeyRepo) : IUseCase<ListApiKeysQuery, List<ApiKeyDto>>
{
    public async Task<List<ApiKeyDto>> ExecuteAsync(ListApiKeysQuery query, CancellationToken ct) { ... }
}
```

### AP-019: ห้ามโยน System Exceptions ใน Application Layer
```csharp
// ❌ WRONG — โยน System Exception ทำให้ Presentation แปลงเป็น RFC 7807 ไม่ตรงมาตรฐาน
throw new UnauthorizedAccessException("Invalid password");
throw new InvalidOperationException("Project slug already exists");

// ✅ CORRECT — โยน Strongly-Typed Domain Exception
throw new UnauthorizedException("Invalid password");
throw new ConflictException("Project slug already exists");
```

### AP-020: ห้ามใช้ Generic IRepository<T> ข้าม Aggregates ที่มี Domain Repository เฉพาะทาง
```csharp
// ❌ WRONG — พึ่งพา Generic Repository ทำให้ Business Query กระจัดกระจาย
public class LoginUseCase(
    IRepository<User> userRepo, 
    IRepository<UserProjectRole> roleRepo) { }

// ✅ CORRECT — พึ่งพา Aggregate Domain Repository ใน SmkDoc.Domain.Interfaces
public class LoginUseCase(
    IUserRepository userRepo, 
    IUserProjectRoleRepository roleRepo) { }
```

### AP-021 – AP-025: Domain Layer Integrity
```csharp
// ❌ AP-021 — เขียนทับ identity จากภายนอก
var template = new Template(projectId, name, slug) { Id = templateId };

// ❌ AP-022 — ข้าม Aggregate Root
template.Versions.Add(new TemplateVersion(...));
// ✅
template.AddVersion(storageKey, format, createdBy);

// ❌ AP-023 — ผ่อน invariant เพราะ UseCase ส่ง Guid.Empty มา
// if (projectId == Guid.Empty) throw ...   <- ลบทิ้ง
// ✅ แก้ที่ UseCase: บังคับ ProjectId จาก command/execution context

// ❌ AP-024
throw new ArgumentException("Hash must be 64 hex chars.");
// ✅
throw new DomainValidationException("Hash must be 64 hex chars.");

// ❌ AP-025
Task<List<Template>> ListAsync();                       // mutable + ไม่ scope tenant
// ✅
Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct);
```

### AP-026: ห้ามให้ HTTP Request สืบทอด (Inherit) จาก Application Command หรือสร้าง Shallow DTO Subclass กลวงเปล่า
```csharp
// ❌ WRONG — Request ผูกติดกับ UseCase Command ข้าม Layer (Leaky Abstraction & Cascading Breaking Changes)
public record CreateProjectRequest(string Name, string Slug) : CreateProjectCommand(Name, Slug);
public record LoginRequest(string Email, string Password) : LoginCommand(Email, Password);

// ❌ WRONG — Empty Subclass ที่ไม่ได้เพิ่ม field หรือ behavior ใดๆ (Fake Abstraction)
public record ProjectListItemDto(Guid Id, string Name, string Slug, bool IsActive, DateTimeOffset CreatedAt) 
    : ProjectResultDto(Id, Name, Slug, IsActive, CreatedAt);

// ✅ CORRECT — Decoupled Request Record ใน Presentation Layer (SmkDoc.Api.Contracts)
namespace SmkDoc.Api.Contracts.IdentityAccess.Projects;
public record CreateProjectRequest(string Name, string Slug);

// ✅ Controller ทำหน้าที่เป็น Translation Adapter (Explicit Mapping)
var userId = User.GetUserId();
var command = new CreateProjectCommand(userId, req.Name, req.Slug);
var project = await createProjectUseCase.ExecuteAsync(command, ct);

// ✅ Return Application DTO เข้า ApiResponse<T> โดยตรง ไม่ต้องสร้าง wrapper DTO ซ้ำซ้อน
return Ok(new ApiResponse<ProjectResultDto>(project));
```

### AP-027: ห้ามใช้ Imperative Role Check (`User.RequireAdmin()`) ใน Controller Action
```csharp
// ❌ WRONG — Imperative check โยน 401 Unauthorized (แทนที่จะเป็น 403 Forbidden) และเสี่ยงลืมเช็คใน Action ใหม่
[HttpPost]
public async Task<IActionResult> CreateKey(...)
{
    User.RequireAdmin(); // ❌ ขัดแย้งกับ ASP.NET Core Authorization Pipeline
    ...
}

// ✅ CORRECT — ใช้ Declarative Role/Policy Authorization ที่ระดับ Controller หรือ Action
[HttpPost]
[Authorize(Roles = "Admin")] // คืน 403 Forbidden อัตโนมัติเมื่อ User ล็อกอินแล้วแต่ไม่มีสิทธิ์
public async Task<IActionResult> CreateKey(...) { ... }
```

### AP-028: ห้ามรับ Raw Primitive Scalar ใน `[FromBody]`
```csharp
// ❌ WRONG — ส่ง payload ดิบๆ เป็น `true` หรือ `false` (ขาด JSON Schema Object, แตกหักง่ายกับ Client SDK)
[HttpPatch("{userId:guid}/status")]
public async Task<IActionResult> SetStatus([FromBody] bool isActive) { ... }

// ✅ CORRECT — ห่อด้วย Positional Record เสมอ
public record SetUserStatusRequest(bool IsActive);

[HttpPatch("{userId:guid}/status")]
public async Task<IActionResult> SetStatus([FromBody] SetUserStatusRequest req) { ... }
```

### AP-029: ห้ามใช้ `CreatedAtAction` ชี้ไปยัง Collection/List Endpoint
```csharp
// ❌ WRONG — Location Header กลายเป็น `/projects?id=...` ซึ่งชี้ไปที่ List แทน Single Resource
return CreatedAtAction(nameof(ListProjects), new { id = project.Id }, new ApiResponse<ProjectDto>(dto));

// ✅ CORRECT — ชี้ไปยัง GetById ของ Resource นั้น หรือตอบ StatusCode 201 หากไม่มี Single GET Endpoint
return CreatedAtAction(nameof(GetProjectById), new { projectId = project.Id }, new ApiResponse<ProjectDto>(dto));
// หรือ (กรณี API Key ซึ่งไม่มี GetById เพื่อความปลอดภัย):
return StatusCode(StatusCodes.Status201Created, new ApiResponse<ApiKeyResponseDto>(dto));
```

### AP-030: ห้ามแก้ไขหรือลบ Resource ใน Tenant Scope โดยไม่ระบุ `projectId` (IDOR Risk)
```csharp
// ❌ WRONG — ส่งแค่ keyId แต่ไม่ตรวจสอบ projectId (เสี่ยงโดนยิงลบข้าม Tenant)
public async Task<IActionResult> RevokeKey([FromRoute] Guid projectId, [FromRoute] Guid keyId)
{
    await _useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId));
}

// ✅ CORRECT — ส่งทั้ง projectId และ entity ID เข้า Command เสมอ
public async Task<IActionResult> RevokeKey([FromRoute] Guid projectId, [FromRoute] Guid keyId)
{
    await _useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId, projectId));
}
```

### AP-031: ห้ามคืน Anonymous Objects หรือ Un-enveloped JSON จาก Controller (`new { success = true }`, `new { id }`)
```csharp
// ❌ WRONG — ทำลาย Contract schema ของ Client, Swagger ไม่รู้ type, ขาด Envelope มาตรฐาน
return Ok(new { success = true });
return Ok(new { keys });
return Ok(new ApiResponse<object>(new { id = created.Id }));

// ✅ CORRECT — ใช้ strongly-typed DTO ห่อด้วย ApiResponse<T> หรือตอบ 204 NoContent
return Ok(new ApiResponse<TemplateResultDto>(result));
// หรือหากไม่มี body ตอบกลับ:
return NoContent();
```

### AP-032: ห้ามใช้ HTTP Status Code ผิดความหมาย (`200 OK` ในการสร้างหรือลบ Resource)
```csharp
// ❌ WRONG — POST สร้าง entity หรือ DELETE ลบ entity แต่ตอบ 200 OK
[HttpPost]
public async Task<IActionResult> Create(...) { return Ok(result); }

[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(...) { return Ok(new { success = true }); }

// ✅ CORRECT — ยึดตาม RFC 7231 REST Semantics
[HttpPost]
public async Task<IActionResult> Create(...)
{
    var result = await createUseCase.ExecuteAsync(command, ct);
    return StatusCode(StatusCodes.Status201Created, new ApiResponse<TemplateResponseDto>(result));
    // หรือ CreatedAtAction(...)
}

[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(...)
{
    await deleteUseCase.ExecuteAsync(command, ct);
    return NoContent(); // 204 NoContent
}
```

### AP-033: ห้ามฉีด Domain/Infrastructure Services เข้า Controller โดยตรง (Bypassing UseCases)
```csharp
// ❌ WRONG — Controller ทำงานข้าม Layer ไปเรียก Service ตรงๆ ฝ่าฝืน Clean Architecture DIP
public class TemplateScanController(ITemplateScannerService scanner) : ControllerBase
{
    [HttpPost("scan-fields")]
    public async Task<IActionResult> Scan(IFormFile file, CancellationToken ct)
    {
        var result = await scanner.ScanPlaceholdersAsync(stream, ext, ct);
    }
}

// ✅ CORRECT — Controller ต้องคุยผ่าน UseCase เท่านั้น (1 Use Case = 1 Action)
public class TemplateScanController(ScanUploadedTemplateUseCase scanUseCase) : ControllerBase
{
    [HttpPost("scan-fields")]
    public async Task<IActionResult> Scan(IFormFile file, CancellationToken ct)
    {
        var result = await scanUseCase.ExecuteAsync(new ScanUploadedTemplateCommand(stream, ext), ct);
        return Ok(new ApiResponse<ScanFieldsResponseDto>(result));
    }
}
```

### AP-034: ห้ามทำ Dual-Routing หรือ Route ที่ไม่มี Version บน Canonical Controller
```csharp
// ❌ WRONG — ติด attribute 2 เส้นทางบน Controller เดียวกัน ทำให้เกิด ambiguity และยากต่อการทำ API Governance
[ApiController]
[Route("api/v1/templates")]
[Route("api/templates")] // ❌ Route เก่าปะปนกับ Route ใหม่
public class TemplateController : ControllerBase { ... }

// ✅ CORRECT — ใช้ Route มาตรฐานเวอร์ชันเดียวชัดเจน
[ApiController]
[Route("api/v1/templates")]
public class TemplateController : ControllerBase { ... }
```

### AP-035: ห้ามสร้าง Ad-hoc Error Payloads ใน Controller (`BadRequest(new { error = ... })`)
```csharp
// ❌ WRONG — Controller ผลิต error schema เอง ทำให้ caller ได้ format ไม่ตรงกับ GlobalExceptionFilter
if (file is not { Length: > 0 })
    return BadRequest(new ApiResponse<object>(new { error = "File is required." }));

// ✅ CORRECT — โยน Domain Exception หรือใช้ FluentValidation / Model Validation ปล่อยให้ GlobalExceptionFilter จัดการเป็น RFC 7807 Problem Details
if (file is not { Length: > 0 })
    throw new DomainValidationException("File must not be null or empty."); // errorCode มีค่า default เป็น "DOMAIN_VALIDATION_ERROR"
// หรือหากต้องการระบุ error code อิสระ:
// throw new BusinessRuleViolationException("File must not be null or empty.", "FILE_REQUIRED");
```

### AP-036: ห้ามสร้าง Command/Query Object ซ้อนข้างใน `ExecuteAsync` โดยตรง (Nested Inline Instantiation)
```csharp
// ❌ WRONG — ประกาศ new Command/Query ซ้อนข้างใน ExecuteAsync(...) ทำให้อ่านยาก และ Debug ตรวจสอบค่าก่อนยิงได้ยาก
await removeUserUseCase.ExecuteAsync(new RemoveUserCommand(projectId, userId, currentUserId), ct);
var result = await listUsersUseCase.ExecuteAsync(new ListProjectUsersQuery(projectId), ct);

// ✅ CORRECT — แยกตัวแปร local variable (var command = ... หรือ var query = ...) ก่อนส่งเข้า ExecuteAsync เสมอ
var query = new ListProjectUsersQuery(projectId);
var result = await listUsersUseCase.ExecuteAsync(query, ct);

var command = new RemoveUserCommand(projectId, userId, currentUserId);
await removeUserUseCase.ExecuteAsync(command, ct);
```

### AP-037: ห้ามเขียน Inline Fully-Qualified Namespace ในโค้ด (ฝ่าฝืน Clean Usings)
```csharp
// ❌ WRONG — เขียน inline namespace รกใน method body / signature ขัดต่อ Clean Architecture และเสี่ยงซ่อน Layer Leaks
public async Task<IActionResult> Parse(IFormFile file)
{
    if (file == null)
        throw new SmkDoc.Domain.Exceptions.DomainValidationException("File required.");
        
    var command = new SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft.ParseTemplateDraftCommand(...);
}

// ✅ CORRECT — ประกาศ using ที่ระดับหัวไฟล์ 100% แล้วเรียกใช้เฉพาะชื่อ Type สั้นๆ
using SmkDoc.Domain.Exceptions;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public async Task<IActionResult> Parse(IFormFile file)
{
    if (file == null)
        throw new DomainValidationException("File required.");
        
    var command = new ParseTemplateDraftCommand(...);
}
```

### AP-038: ห้ามใช้ Underscore Prefix (`_`) หรือชื่อ Generic คลุมเครือใน Primary Constructor
```csharp
// ❌ WRONG — Primary constructor parameter ไม่ใช่ private field และห้ามใช้ชื่อคลุมเครือ (service, repo)
public class DocumentController(
    GenerateDocumentUseCase _generateUseCase,
    ITemplateRepository repo,
    IStorageService service) : ControllerBase

// ✅ CORRECT — ใช้ camelCase 1:1 ตรงตามชื่อ Class หรือ Interface เสมอ
public class DocumentController(
    GenerateDocumentUseCase generateUseCase,
    ITemplateRepository templateRepo,
    IStorageService storageService) : ControllerBase
```

### AP-039: ห้ามสร้าง Primitive Convenience Overloads หรือ Default Timestamp Fallback ใน Domain Entities
```csharp
// ❌ WRONG — Entity สร้าง overload รับ string เพื่อความสะดวกของ caller หรือ default now เป็น null ทำให้ domain ไม่ deterministic
public static Template Create(Guid projectId, string name, string slug, DateTimeOffset? now = null)
{
    var timestamp = now ?? DateTimeOffset.UtcNow; // ❌ ซ่อน Side-effect ภายใน Domain
    return new Template(projectId, name, slug, timestamp);
}

// ✅ CORRECT — Exactly 1 Canonical Factory รับเฉพาะ Value Objects และบังคับ deterministic now จาก Application UseCase
public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
    new(null, projectId, name, slug, category, now);
```

### AP-040: ห้ามสร้าง `CreateForTest` หรือ Test Backdoors ใน Production Assembly `SmkDoc.Domain.dll`
```csharp
// ❌ WRONG — มี Test Method หรือ backdoor ใน Domain Entity ของ Production Code
public sealed class Template : BaseEntity
{
    public static Template CreateForTest(Guid id, string name, ...) // ❌ ปนเปื้อน production DLL
}

// ✅ CORRECT — Constructor พิเศษสำหรับ Test ต้องเป็น internal และตัวสร้างทั้งหมดต้องอยู่ใน SmkDoc.Tests
// ใน SmkDoc.Domain:
internal Template(Guid? id, Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now)
    : base(id, createdAt: now) { ... }

// ใน SmkDoc.Tests/Common/Factories/TemplateTestFactory.cs:
public static class TemplateTestFactory
{
    public static Template Create(Guid? id = null, ...) => new Template(...);
}
```

### AP-041: ห้าม Hardcode ตัวเลขผันผวน (High-Churn Execution Metrics) ลงในเอกสารคู่มือระบบและ Agent Guidelines
```markdown
// ❌ WRONG — เอกสารระบุตัวเลขผันผวนแบบ Snapshot ทำให้ล้าสมัยทันทีที่มีการเพิ่มโค้ด และสร้าง Cognitive Bias / ความสับสนให้ LLM
- Test Suite: 614 tests passing / 244 tests passing
- API Controllers: 17 controllers
- Data Entities: 14 domain entities

// ✅ CORRECT — ใช้ Invariant-Driven Quality Gates และเกณฑ์เชิงสถาปัตยกรรมที่ไม่ขึ้นกับกาลเวลา
- Test Suite: Unit & Integration test suite — 100% Passing (0 errors, 0 failures, zero tolerated regressions)
- API Controllers: Thin HTTP Controllers organized by Bounded Context (Tenant, Authoring, Rendering, Integration)
- Data Entities: Sealed Rich Domain Models enforcing SSoT Canonical Factory & Zero Test Backdoors
```

> **หมายเหตุข้อแตกต่างที่สำคัญ:** ค่าคงที่ทางธุรกิจและสถาปัตยกรรม (Domain Invariant Constants) เช่น *1 Canonical Factory method ต่อ Entity*, *SHA-256 = 64 ตัวอักษร*, *MaxChangeNoteLength = 500* **ต้องคงตัวเลขที่แน่นอนไว้เสมอ** เพราะเป็นกฎทางธุรกิจที่ไม่ใช่ตัวเลขผันผวนจากการรันโค้ด

### AP-042: ห้ามสร้าง Monolithic UseCase Test Class หรือวางไฟล์เทสแบนราบ (Flat) นอก CQRS Folders
```csharp
// ❌ WRONG — รวม Use Case หลายตัวไว้ในคลาสเดียว หรือวางไฟล์แบนราบ
// SmkDoc.Tests/Application/Modules/Tenant/Projects/ProjectUseCaseTests.cs
public class ProjectUseCaseTests {
    [Fact] public async Task CreateProject_Works() { ... }
    [Fact] public async Task UpdateProject_Works() { ... }
    [Fact] public async Task DeleteProject_Works() { ... }
}

// ✅ CORRECT — 1:1 CQRS Folder Parity แยก 1 Use Case ต่อ 1 โฟลเดอร์และ 1 ไฟล์ SUT
// SmkDoc.Tests/Application/Modules/Tenant/Projects/Commands/CreateProject/CreateProjectUseCaseTests.cs
public class CreateProjectUseCaseTests { ... }

// SmkDoc.Tests/Application/Modules/Tenant/Projects/Commands/UpdateProject/UpdateProjectUseCaseTests.cs
public class UpdateProjectUseCaseTests { ... }
```

### AP-043: ห้ามใส่ Heavy I/O, Generators หรือ Benchmarks ใน Unit Test Project (`SmkDoc.Tests`)
```csharp
// ❌ WRONG — รัน OpenXml/ClosedXML disk writers หรือ benchmark วัด performance ใน SmkDoc.Tests
// ทำให้ PR unit test ช้า มี I/O artifact ค้างใน disk และไม่เป็น pure in-memory
[Fact]
public async Task GenerateLargeExcel_WriteToDisk_Benchmark() {
    using var fs = File.Create("test.xlsx");
    ...
}

// ✅ CORRECT — แยก solution-level ชัดเจน: ย้ายไป SmkDoc.IntegrationTests
// SmkDoc.Tests = 100% Pure in-memory (0 disk, 0 network, 0 DB) รันจบในระดับวินาที
// SmkDoc.IntegrationTests = Benchmarks, Generators, Container Fixtures
```

### AP-044: ห้าม New Entity สดๆ แบบ Ad-hoc หรือใช้ `DateTimeOffset.UtcNow` ใน Unit Test
```csharp
// ❌ WRONG — bypass builders, ค่า mock ไม่สม่ำเสมอ และเวลาเลื่อนไหล (flaky test)
var template = new Template(Guid.NewGuid(), "Name", "slug", null, DateTimeOffset.UtcNow);

// ✅ CORRECT — ใช้ *Builder / *TestFactory ร่วมกับ TestConstants.BaselineTime
var template = new TemplateBuilder()
    .WithName("Invoice")
    .WithSlug("invoice-01")
    .Build(); // ภายในใช้ TestConstants.BaselineTime เป็น SSoT
```

### AP-045: ห้ามสร้างไฟล์รวมกลางทดสอบ Invariant ข้าม Aggregate (`DomainInvariantTests`)
```csharp
// ❌ WRONG — นำ invariant ของหลาย Aggregate มารวมในไฟล์เดียว กลายเป็น Dumping Ground
// SmkDoc.Tests/Domain/DomainInvariantTests.cs
// SmkDoc.Tests/Domain/EntityEncapsulationTests.cs

// ✅ CORRECT — รวม Invariant และ Encapsulation เข้ากับ Aggregate Root Unit Test โดยตรง (SSoT)
// SmkDoc.Tests/Domain/Entities/TemplateTests.cs
// SmkDoc.Tests/Domain/Entities/UserTests.cs
```

### AP-046: ห้าม Mock Dependencies ใน Command/Query Validator Tests
```csharp
// ❌ WRONG — นำ Mock<IRepository> เข้าไปใน Validator test ทำให้หนักและช้า
var repoMock = new Mock<ITemplateRepository>();
var validator = new CreateTemplateCommandValidator(repoMock.Object);

// ✅ CORRECT — Validator เป็น Pure Function; ทดสอบ constraints ด้วย [Theory] + [InlineData]
public class CreateTemplateCommandValidatorTests
{
    private readonly CreateTemplateCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenNameIsEmpty_HasValidationError(string name)
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), name, "slug");
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
```

### AP-047: ห้ามเรียก `new UseCase(...)` สดๆ หรือประกาศตัวแปร `var sut = ...` ภายใน Test Method
```csharp
// ❌ WRONG — New UseCase เองในทุกเทสต์ ทำให้เกิด Constructor Coupling และมีตัวแปร sut ซ้ำซ้อน
[Fact]
public async Task ExecuteAsync_HappyPath()
{
    var sut = new CreateApiKeyUseCase(_apiKeyRepoMock.Object, _projectRepoMock.Object, _uowMock.Object, _validator);
    var result = await sut.ExecuteAsync(command);
    ...
}

// ✅ CORRECT — SSoT SUT Factory (`CreateSut`) + Direct 3-A Invocation
private CreateApiKeyUseCase CreateSut(IValidator<CreateApiKeyCommand>? validator = null) =>
    new(_apiKeyRepoMock.Object, _projectRepoMock.Object, _uowMock.Object, validator ?? _validator);

[Fact]
public async Task ExecuteAsync_WhenValidCommand_ReturnsSuccessResult()
{
    var result = await CreateSut().ExecuteAsync(command);
    ...
}
```

### AP-048: ห้าม Shallow Exception Assertions และห้ามละเลยการ Verify `Times.Never` บน Mutation Repositories / Unit of Work
```csharp
// ❌ WRONG — เช็คแค่ประเภท Exception (เสี่ยง False Positive) และไม่ตรวจสอบ Side-effect (เสี่ยง Bug แอบเซฟข้อมูล)
[Fact]
public async Task ExecuteAsync_WhenConflict_Throws()
{
    var act = () => CreateSut().ExecuteAsync(command);
    await act.Should().ThrowAsync<ConflictException>(); // ขาดการตรวจ Identifier
    // ขาดการ Verify ว่าไม่ได้ Commit ข้อมูลลง DB
}

// ✅ CORRECT — Deep Semantic Assertions ด้วย Wildcard Keyword + ตรวจสอบ Negative Side-effects ครบถ้วน
[Fact]
public async Task ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException()
{
    var act = () => CreateSut().ExecuteAsync(command);
    await act.Should().ThrowAsync<ConflictException>()
        .WithMessage($"*'{command.Slug}'*");

    _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    _apiKeyRepoMock.Verify(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

---

### 🚦 Unit Testing Anti-Patterns Quick Matrix

| Anti-Pattern | ❌ Do NOT Do This | ✅ Always Do This Instead |
|---|---|---|
| **AP-042** | รวมหลาย UseCase ในไฟล์เดียว | 1 UseCase = 1 Test Class ในโฟลเดอร์ CQRS เฉพาะ |
| **AP-043** | รัน OpenXml / Disk I/O ใน `SmkDoc.Tests` | ย้าย I/O หนักไปที่ `SmkDoc.IntegrationTests` |
| **AP-044** | `new Entity(...)` สดๆ / `DateTimeOffset.UtcNow` | ใช้ `*Builder` / `*TestFactory` + `TestConstants.BaselineTime` |
| **AP-045** | สร้างไฟล์รวม invariant `DomainInvariantTests` | ยึด SSoT ใน `{Aggregate}Tests.cs` โดยตรง |
| **AP-046** | Mock dependencies ใน Validator Tests | ใช้ `[Theory]` + `[InlineData]` เพียวๆ 0 Mocks |
| **AP-047** | `var sut = new ...` ในทุก test method | เรียกผ่าน `CreateSut().ExecuteAsync(...)` เสมอ |
| **AP-048** | Assert แค่ Exception Type โดยไม่เช็ค Commit | ใช้ Semantic Wildcard + `_uowMock.Verify(Times.Never)` |

---

## 🟡 Frontend Anti-Patterns

### AP-F001: ห้าม Hardcode สีหรือขนาดใน Component
```tsx
// ❌ WRONG
<div style={{ color: '#1a73e8', borderRadius: '8px' }}>

// ✅ CORRECT
<div style={{ color: 'var(--color-primary)', borderRadius: 'var(--radius-md)' }}>
```

### AP-F002: ห้าม fetch ข้อมูลตรงๆ ใน Component
```tsx
// ❌ WRONG
const [templates, setTemplates] = useState([]);
useEffect(() => { fetch('/api/templates').then(...) }, []);

// ✅ CORRECT — ผ่าน Hook
const { templates, isLoading } = useTemplates();
```

### AP-F003: ห้ามใช้ `any` type ใน TypeScript
```tsx
// ❌ WRONG
const data: any = await fetchTemplates();

// ✅ CORRECT — ใช้ Zod schema + inferred types
const data = TemplateSchema.array().parse(rawData);
```

### AP-F004: ห้ามสร้าง `eslintrc.json` (ใช้ ESLint 9 Flat Config)
```
// ❌ WRONG — legacy format
.eslintrc.json

// ✅ CORRECT — Flat Config
eslint.config.mjs
```
