# ANTI-PATTERNS.md — Prohibited Patterns & Pitfalls (The PR Review Shield)

> **Purpose:** This document is the definitive negative-constraint inventory of strictly prohibited code patterns, anti-patterns, and architectural regressions across the SMK Document Server (`backend-v2/` and `frontend-v2/`).
> **[AI_DIRECTIVE]:** AI Assistants (LLMs) and human code reviewers MUST treat these rules as non-negotiable blocking constraints. If any code matches a prohibited pattern in this document, it MUST be immediately rejected during PR review and refactored to the corresponding golden standard.

<ai_directive>
CRITICAL ATTENTION ROUTING:
- For Backend (C# 13 / .NET 10), STRICTLY enforce Categories 1 through 6 (`<backend_scope>`).
- For Frontend (TypeScript 5.7 / Next.js 15.2), STRICTLY enforce Category 7 (`<frontend_scope>`).
- For Golden Architectural Archetypes, refer directly to `docs/AI/PATTERNS.md`.
- For Style, Formatting, and Craftsmanship Rules, refer to `docs/AI/CODING_CONVENTIONS.md`.
</ai_directive>

---

## 📑 Master Quick-Lookup Index

### 🔵 Backend Prohibited Patterns (C# 13 / .NET 10)

| Code | Prohibited Anti-Pattern | Correct Golden Standard |
|---|---|---|
| **AP-001** | `IRepository<T>` in Controller | Inject specific Single-Responsibility UseCase (`IUseCase<TCommand, TResult>`) |
| **AP-002** | Return Domain Entity from UseCase | Map to Application DTO record (`*ResultDto`) |
| **AP-003** | Anonymous objects in API Response | Wrap in strongly-typed `ApiResponse<T>` or `PagedApiResponse<T>` |
| **AP-004** | `try-catch` in Controllers / Legacy `IExceptionFilter` | Throw DomainException; let `IExceptionHandler` map to RFC 9457 |
| **AP-005** | Import `AppDbContext` in Application | Depend strictly on `IRepository` abstraction and `IUnitOfWork` |
| **AP-006** | `switch` or `if/else` on `RenderEngineType` | Strategy Pattern via DI (`IEnumerable<IRenderEngine>`) |
| **AP-007** | `IFormFile` or `HttpContext` in UseCase | Pass pure C# primitives/abstractions (`Stream`, `string`, `Guid`) |
| **AP-008** | Object initializers `{ get; set; }` for Entities | Encapsulated constructor + business verbs (Rich Domain Model) |
| **AP-009** | String comparison for Smart Enums | Compare typed Smart Enum instances (e.g., `TemplateFormat.Html`) |
| **AP-010** | Hardcoded `{{}}` regex | `PlaceholderHelper.Pattern` (SSoT) |
| **AP-011** | DrawingML Id = 0 | Use positive integer `(uint)counter + 1` |
| **AP-012** | String-replace on MinIO Presigned URLs | Set `PublicEndpoint` in `MinioSettings` |
| **AP-013** | Auto-execute Docker commands | Print command snippet for user to execute |
| **AP-014** | Modify legacy v1 directories | Edit exclusively in `backend-v2/` and `frontend-v2/` |
| **AP-015** | Throw generic `Exception` or `KeyNotFoundException` | Throw strongly-typed `NotFoundException`, `ConflictException`, etc. |
| **AP-016** | Mutable classes or DataAnnotations in DTOs | 100% immutable positional `record` types |
| **AP-017** | Inline DTOs inside UseCase files | Place DTOs in dedicated `DTOs/` folders |
| **AP-018** | Multi-Method Fat Use Case class | 1 Intent = 1 Class implementing `IUseCase<TCommand, TResult>` |
| **AP-019** | Throwing System Exceptions in UseCase | Throw strongly-typed Domain Exceptions (`UnauthorizedException`, etc.) |
| **AP-020** | Generic `IRepository<T>` when Aggregate Repository exists | Use explicit domain repository (`ITemplateRepository`, `IProjectRepository`) |
| **AP-021** | Override identity `new X(...) { Id = ... }` | Let `BaseEntity` generate UUIDv7; pass Id via factory only if required |
| **AP-022** | Public mutable collections `ICollection<T>` on entities | `private readonly List<T>` + `IReadOnlyCollection<T>` + Aggregate Root methods |
| **AP-023** | Weakening an invariant to make tests/UseCases pass | Fix the caller; domain invariants are strictly non-negotiable |
| **AP-024** | `ArgumentException`/`InvalidOperationException` in Domain | Throw `DomainValidationException` / `BusinessRuleViolationException` |
| **AP-025** | Repository returning `IQueryable` or mutable `List<T>` | Return Entity or `IReadOnlyList<T>`; require tenant ID on queries |
| **AP-026** | Request inherits from Command / Shallow DTO subclasses | Decouple Presentation Request records; explicit mapping in Controller |
| **AP-027** | Imperative Role check in Controller Action | Declarative `[Authorize(Roles = "Admin")]` on Controller/Action |
| **AP-028** | Raw primitive scalar in Request Body | Positional Record Request DTO (`[FromBody] SetUserStatusRequest request`) |
| **AP-029** | `CreatedAtAction` pointing to collection endpoint | Point to single-item `GetById` with entity route parameter |
| **AP-030** | Unscoped Tenant Mutation (IDOR Vulnerability) | Always pass and validate tenant context (`projectId`) alongside entity ID |
| **AP-031** | Unversioned or duplicated routes | Strict canonical routes `api/v1/{resource}` |
| **AP-032** | Incorrect HTTP status codes | 201 Created for creation, 204 NoContent for delete, 200 OK for reads |
| **AP-033** | Direct service injection in Controller | Inject single-responsibility UseCases only |
| **AP-034** | Dual routing attributes on Controller | Single canonical route prefix per controller |
| **AP-035** | Ad-hoc error payloads in Controller | Throw domain exceptions; RFC 9457 via `IExceptionHandler` |
| **AP-036** | Nested inline instantiation in `ExecuteAsync` | Declare explicit local variable (`var command = ...`) before invocation |
| **AP-037** | Inline fully-qualified namespaces | Clean top-level usings only (Zero inline namespaces) |
| **AP-038** | Underscore prefix or field re-declaration in Primary Ctor | Standardized `camelCase` parameters consumed directly (Zero `_`) |
| **AP-039** | Primitive overloads & optional timestamp fallback in Domain | Single Canonical Factory taking strongly-typed Value Objects + mandatory `now` |
| **AP-040** | Test backdoors in `SmkDoc.Domain.dll` | Factory/Builder strictly in `SmkDoc.Tests` via `internal` constructor |
| **AP-041** | Hardcoding high-churn execution metrics in docs | Use invariant-driven quality gates (100% pass rate, 0 failures) |
| **AP-042** | Monolithic UseCase test classes or flat CQRS placement | 1:1 CQRS Folder Parity (`Commands/{Command}/` & `Queries/{Query}/`), single SUT |
| **AP-043** | Heavy I/O, Generators, or Benchmarks in Unit Tests | Pure in-memory unit tests in `SmkDoc.Tests`; I/O in `SmkDoc.IntegrationTests` |
| **AP-044** | Ad-hoc entity instantiation or nondeterministic `UtcNow` | Use `*Builder` / `*TestFactory` with `TestConstants.BaselineTime` |
| **AP-045** | Artificial dumping grounds for invariant tests | Test invariants directly in Aggregate Root unit tests (`{Aggregate}Tests.cs`) |
| **AP-046** | Mocking dependencies in Validator Tests | Test validators as pure functions with `[Theory]` + `[InlineData]` |
| **AP-047** | `var sut = new ...` in every test method | Call via SSoT `CreateSut().ExecuteAsync(...)` |
| **AP-048** | Asserting only Exception Type without checking DB side-effects | Use Semantic Wildcard + `unitOfWorkMock.Verify(Times.Never)` |
| **AP-049** | Arrow Anti-Pattern & Cyclomatic Indentation > 2 | Flatten execution using Guard Clauses / Early Exits |
| **AP-050** | Echo / Robot Comments | Comments explain WHY (business rule, RFC); code explains WHAT |
| **AP-051** | Unbounded Fluent LINQ / Chaining (> 3 operations) | Split into intermediate, descriptively named local variables |
| **AP-052** | Memory Buffering in Document Rendering (LOH Fragmentation) | 100% Stream-over-RAM piping directly from Gotenberg to MinIO/HTTP |
| **AP-053** | Missing or Un-scoped Idempotency on State Mutations | Support `Idempotency-Key` header with 24h cache and 409 in-flight check |
| **AP-054** | Primitive Obsession in Repository Signatures | Strongly-typed Value Objects with tenant parameter first (`ExistsBySlugAsync`) |
| **AP-055** | Disordered File Anatomy (Violating the Stepdown Rule) | Public methods at top; private helpers placed directly beneath caller |
| **AP-056** | Semantic Synonym Drift (Violating Single Term per Concept) | Standardize strictly on canonical verbs (`GetByIdAsync`, `ListAsync`, `CommitAsync`) |

---

### 🟡 Frontend Prohibited Patterns (TypeScript 5.7 / Next.js 15.2)

| Code | Prohibited Anti-Pattern | Correct Golden Standard |
|---|---|---|
| **AP-F001** | Inline styles or hardcoded colors | Strictly use Tailwind utility classes (`text-primary`, `bg-surface`) |
| **AP-F002** | Client-side `useEffect` for initial data fetching | Next.js Server Components (`await fetch()`) for initial data load |
| **AP-F003** | `any` types in TypeScript | Zod schema validation + inferred types (`z.infer`) |
| **AP-F004** | Legacy `.eslintrc.json` config | Modern Flat Config `eslint.config.mjs` |
| **AP-F005** | Root-Level `"use client"` Pollution | Server Component by default; isolate `"use client"` to interactive leaf controls |
| **AP-F006** | In-Flight Preview Race Conditions | Abort pending requests via `AbortController.abort()` on rapid keystrokes |
| **AP-F007** | Manual Type Duplication (Bypassing Zod SSoT) | Derive TypeScript types automatically via `z.infer<typeof schema>` |
| **AP-F008** | Swallowing or Toasting Intentional `AbortError` | Inspect `error.name === "AbortError"` and exit silently |

---

<backend_scope>
## 🏛️ Category 1: Clean Architecture & Layer Boundaries

### AP-001: Direct Repository Injection in Controllers
*   **The Architectural Danger:** Violates Clean Architecture boundaries by turning Controllers into fat orchestrators and bypassing Application business workflows, validation filters, and security pipelines.
```csharp
// ❌ WRONG: Controller directly depends on data persistence
public sealed class TemplateController(ITemplateRepository templateRepo) : ControllerBase 
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id) => Ok(await templateRepo.GetByIdAsync(id));
}

// ✅ CORRECT: Controller delegates to Single-Responsibility UseCase
public sealed class TemplateController(
    IUseCase<GetTemplateByIdQuery, TemplateResultDto> getTemplateByIdUseCase) : ControllerBase 
{
    [HttpGet("{templateId:guid}")]
    public async Task<ActionResult<ApiResponse<TemplateResultDto>>> GetById(Guid templateId, CancellationToken ct)
    {
        var query = new GetTemplateByIdQuery(templateId);
        var result = await getTemplateByIdUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<TemplateResultDto>(result));
    }
}
```

---

### AP-002: Leaking Domain Entities Across Boundaries
*   **The Architectural Danger:** Exposing Domain Entities to outer layers (Presentation or Middleware) leads to unintended state mutations, circular dependencies, and mass-assignment vulnerabilities.
```csharp
// ❌ WRONG: Returning Domain Entity to Presentation or Middleware
public async Task<Template> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
{
    var template = Template.Create(command.ProjectId, command.Name, command.Slug, command.Category, timeProvider.GetUtcNow());
    await templateRepo.AddAsync(template, ct);
    return template; // LEAK! Domain Entity exposed to outer layers
}

// ✅ CORRECT: Map to immutable Positional Record DTO before returning
public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
{
    var template = Template.Create(command.ProjectId, command.Name, command.Slug, command.Category, timeProvider.GetUtcNow());
    await templateRepo.AddAsync(template, ct);
    await unitOfWork.CommitAsync(ct);
    return new TemplateResultDto(template.Id, template.ProjectId, template.Name.Value, template.Slug.Value, template.Category, template.CreatedAt);
}
```

---

### AP-005: Importing Infrastructure Details into Application Layer
*   **The Architectural Danger:** Direct dependencies on `AppDbContext`, Npgsql, or MinIO SDK in the Application layer break Dependency Inversion and couple business orchestration to relational database implementations.
```csharp
// ❌ WRONG: Application layer references Infrastructure persistence directly
using SmkDoc.Infrastructure.Persistence;

public sealed class CreateTemplateUseCase(AppDbContext dbContext) 
{
    public async Task ExecuteAsync(...) => await dbContext.Templates.AddAsync(...);
}

// ✅ CORRECT: Depend exclusively on Domain/Application abstractions
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo, 
    IUnitOfWork unitOfWork) : IUseCase<CreateTemplateCommand, TemplateResultDto> 
{
    // Implementation uses pure interfaces
}
```

---

### AP-007: Framework Types in UseCases
*   **The Architectural Danger:** Injecting `IFormFile`, `HttpContext`, or `HttpRequest` into UseCases couples the Application layer to ASP.NET Core hosting environments, preventing usage in background workers, CLI tools, or event consumers.
```csharp
// ❌ WRONG: Passing ASP.NET Core HTTP types into UseCase
public sealed record UploadTemplateCommand(IFormFile File, HttpContext Context);

// ✅ CORRECT: Pass pure C# primitives and streams
public sealed record UploadTemplateCommand(Stream FileStream, string FileName, string ContentType);
```

---

### AP-014: Modifying Legacy v1 Directories
*   **The Architectural Danger:** Editing legacy v1 folders (`src/`, `backend/`, `frontend/`) re-introduces deprecated architectural anti-patterns and creates merge conflicts. Active development is strictly isolated to `backend-v2/` and `frontend-v2/`.
```text
// ❌ WRONG: Modifying legacy codebase files
src/SmkDocServer/Services/TemplateService.cs
backend/SmkDoc.Api/Controllers/OldController.cs

// ✅ CORRECT: Implement exclusively inside modern v2 architecture
backend-v2/src/SmkDoc.Application/Features/Templates/Commands/CreateTemplate/CreateTemplateUseCase.cs
frontend-v2/src/components/templates/TemplateEditor.tsx
```

---

### AP-018: Multi-Method Fat Use Case Classes
*   **The Architectural Danger:** Consolidating multiple actions into a single service class (e.g., `TemplateService`) violates the Single Responsibility Principle and creates monolithic merge conflicts.
```csharp
// ❌ WRONG: Service with multiple operational intents
public class TemplateService 
{
    public Task Create(...) { }
    public Task Update(...) { }
    public Task Delete(...) { }
    public Task Render(...) { }
}

// ✅ CORRECT: 1 Business Intent = 1 Action-Centric UseCase Class
public sealed class CreateTemplateUseCase : IUseCase<CreateTemplateCommand, TemplateResultDto> { ... }
public sealed class UpdateTemplateDetailsUseCase : IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> { ... }
public sealed class RenderDocumentUseCase : IUseCase<RenderDocumentCommand, DocumentStreamResult> { ... }
```

---

### AP-020: Generic `IRepository<T>` when Aggregate Domain Repository Exists
*   **The Architectural Danger:** Using a generic repository abstraction (`IRepository<Template>`) bypasses domain-specific Aggregate Root invariants, specialized queries, and tenant-scoped security checks.
```csharp
// ❌ WRONG: Bare generic repository bypasses Aggregate Root domain methods
public sealed class GetTemplateUseCase(IRepository<Template> genericRepo) { ... }

// ✅ CORRECT: Explicit domain repository enforcing tenant-scoped invariants
public sealed class GetTemplateUseCase(ITemplateRepository templateRepo) { ... }
```

---

### AP-026: Request DTO Inheriting from Command DTO / Shallow Subclassing
*   **The Architectural Danger:** Having Presentation Request records inherit from Application Command records couples the HTTP transport schema to internal application pipelines, preventing independent versioning.
```csharp
// ❌ WRONG: Presentation Request inherits from Application Command
public sealed record CreateTemplateRequest(Guid ProjectId, string Name, string Slug, string? Category) 
    : CreateTemplateCommand(ProjectId, Name, Slug, Category);

// ✅ CORRECT: Fully decoupled records with explicit mapping in Controller
public sealed record CreateTemplateRequest(Guid ProjectId, string Name, string Slug, string? Category);
public sealed record CreateTemplateCommand(Guid ProjectId, string Name, string Slug, string? Category);

// In Controller:
var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
```

---

### AP-033: Direct Infrastructure / External Service Injection in Controller
*   **The Architectural Danger:** Injecting infrastructure services (`IMinioStorageService`, `IGotenbergClient`) directly into Controllers bypasses application business rules, transaction boundaries, and audit logging.
```csharp
// ❌ WRONG: Controller directly calls external cloud storage
public sealed class DocumentController(IMinioStorageService storageService) : ControllerBase 
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file) 
    {
        await storageService.UploadAsync("bucket", file.FileName, file.OpenReadStream());
        return Ok();
    }
}

// ✅ CORRECT: Controller delegates to Single-Responsibility UseCase
public sealed class DocumentController(
    IUseCase<UploadDocumentCommand, DocumentResultDto> uploadDocumentUseCase) : ControllerBase 
{
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<DocumentResultDto>>> Upload(IFormFile file, CancellationToken ct) 
    {
        await using var stream = file.OpenReadStream();
        var command = new UploadDocumentCommand(stream, file.FileName, file.ContentType);
        var result = await uploadDocumentUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<DocumentResultDto>(result));
    }
}
```

---

## 💎 Category 2: Domain-Driven Design & Invariant Protection

### AP-008: Anemic Entities with Public Property Setters
*   **The Architectural Danger:** Public setters allow external callers to mutate entity properties into invalid, unverified states, bypassing domain invariants.
```csharp
// ❌ WRONG: Anemic Entity with public setters and object initializer
public class Template 
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
var template = new Template { Name = "Invoice", IsActive = true };

// ✅ CORRECT: Rich Domain Entity with private setters and Canonical Factory
public sealed class Template : BaseEntity 
{
    public TemplateName Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private Template() { } // EF Core only

    public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
        new(null, projectId, name, slug, category, now);

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        SetUpdated(now);
    }
}
```

---

### AP-009: String Comparisons for Smart Enums
*   **The Architectural Danger:** Comparing Smart Enums using string literals bypasses compiler type checking and introduces silent runtime defects from case mismatches or typos.
```csharp
// ❌ WRONG: String comparison against Smart Enum name
if (template.Format.Name == "Html") { ... }
if (engineType == "gotenberg") { ... }

// ✅ CORRECT: Type-safe instance comparison using Smart Enum SSoT
if (template.Format == TemplateFormat.Html) { ... }
if (engineType == RenderEngineType.Gotenberg) { ... }
```

---

### AP-021: Manual Identity Override on UUIDv7 Entities
*   **The Architectural Danger:** Overriding entity primary keys manually defeats chronological UUIDv7 sorting in PostgreSQL B-Tree indexes, causing page splits and database fragmentation.
```csharp
// ❌ WRONG: Overriding Id manually with random GUID
var template = new Template { Id = Guid.NewGuid() };

// ✅ CORRECT: BaseEntity automatically assigns Guid.CreateVersion7()
public abstract class BaseEntity 
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    protected BaseEntity(Guid? id, DateTimeOffset createdAt) 
    {
        Id = id ?? Guid.CreateVersion7();
        CreatedAt = createdAt;
    }
}
```

---

### AP-022: Public Mutable Collections on Entities
*   **The Architectural Danger:** Exposing mutable collections (`List<T>`, `ICollection<T>`) allows external code to add or remove children without enforcing Aggregate Root boundary validation.
```csharp
// ❌ WRONG: Public mutable collection allows direct modification
public class Project : BaseEntity 
{
    public List<ApiKey> ApiKeys { get; set; } = []; // External callers can invoke .Clear() or .Add()
}

// ✅ CORRECT: Encapsulated backing list exposed as IReadOnlyCollection
public sealed class Project : BaseEntity 
{
    private readonly List<ApiKey> apiKeys = [];
    public IReadOnlyCollection<ApiKey> ApiKeys => apiKeys.AsReadOnly();

    public ApiKey GenerateApiKey(string name, DateTimeOffset now) 
    {
        var key = ApiKey.Create(Id, name, now);
        apiKeys.Add(key);
        SetUpdated(now);
        return key;
    }
}
```

---

### AP-023: Weakening Domain Invariants to Make Tests or UseCases Pass
*   **The Architectural Danger:** Relaxing entity or Value Object validation rules to bypass test setup issues compromises data integrity across the entire application and allows corrupted state in production.
```csharp
// ❌ WRONG: Weakening domain invariant rule to appease failing test
public sealed class TemplateSlug : ValueObject 
{
    public static TemplateSlug Create(string value) 
    {
        // Weakened rule: Allowing empty strings or spaces just to satisfy a test
        if (string.IsNullOrEmpty(value)) return new TemplateSlug("default-slug"); 
        ...
    }
}

// ✅ CORRECT: Enforce strict invariant; update test fixture to provide valid data
public sealed class TemplateSlug : ValueObject 
{
    public static TemplateSlug Create(string value) 
    {
        if (string.IsNullOrWhiteSpace(value) || !SlugRegex().IsMatch(value))
        {
            throw new DomainValidationException("Slug must contain lowercase alphanumeric characters and hyphens.");
        }
        return new TemplateSlug(value);
    }
}
```

---

### AP-024: Throwing Generic System Exceptions in Domain Layer
*   **The Architectural Danger:** Throwing `ArgumentException` or `InvalidOperationException` prevents centralized exception middleware from categorizing errors, converting client validation bugs into 500 Internal Server Errors.
```csharp
// ❌ WRONG: Throwing generic runtime exceptions from Domain entities
public void UpdateName(string name) 
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty");
}

// ✅ CORRECT: Throw strongly-typed Domain Validation or Business Rule exceptions
public void UpdateName(TemplateName name, DateTimeOffset now) 
{
    ArgumentNullException.ThrowIfNull(name);
    Name = name;
    SetUpdated(now);
}
```

---

### AP-039: Primitive Overloads & Optional Timestamp Fallbacks in Domain Factories
*   **The Architectural Danger:** Providing factory overloads taking raw primitives or defaulting `DateTimeOffset? now = null` to `DateTimeOffset.UtcNow` introduces non-determinism, bypasses Value Objects, and complicates time-sensitive testing.
```csharp
// ❌ WRONG: Factory overload with primitives and optional timestamp fallback
public static Template Create(string name, string slug, DateTimeOffset? now = null) 
{
    var timestamp = now ?? DateTimeOffset.UtcNow; // NON-DETERMINISTIC
    return new Template(Guid.NewGuid(), name, slug, timestamp);
}

// ✅ CORRECT: Exactly 1 Canonical Factory with strongly-typed Value Objects + mandatory now
public static Template Create(
    Guid projectId, 
    TemplateName name, 
    TemplateSlug slug, 
    string? category, 
    DateTimeOffset now) 
{
    return new Template(null, projectId, name, slug, category, now);
}
```

---

### AP-040: Test Backdoors in `SmkDoc.Domain.dll`
*   **The Architectural Danger:** Adding test-only methods, `#if DEBUG` public setters, or bypass parameters to production domain classes pollutes the domain binary and compromises encapsulation.
```csharp
// ❌ WRONG: Test backdoor inside production domain class
public sealed class Template : BaseEntity 
{
    #if DEBUG
    public void SetIdForTesting(Guid id) => Id = id; // PROHIBITED
    #endif
}

// ✅ CORRECT: Internal constructor visible strictly to test assemblies
public sealed class Template : BaseEntity 
{
    // InternalsVisibleTo("SmkDoc.Tests")
    internal Template(Guid? id, Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now)
        : base(id, now) { ... }
}
```

---

### AP-054: Primitive Obsession in Repository Signatures
*   **The Architectural Danger:** Accepting raw `string` parameters instead of strongly-typed Value Objects bypasses domain regex validation and frequently leads to missing tenant isolation (IDOR vulnerabilities).
```csharp
// ❌ WRONG: Raw string slug without tenant scope
public interface ITemplateRepository 
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
}

// ✅ CORRECT: Strongly-typed Value Object with tenant parameter first
public interface ITemplateRepository 
{
    Task<bool> ExistsBySlugAsync(Guid projectId, TemplateSlug slug, CancellationToken ct = default);
}
```

---

## 🌐 Category 3: Presentation, Routing, RFC 9457 & Idempotency

### AP-003: Anonymous Error Objects in API Responses
*   **The Architectural Danger:** Returning anonymous objects (`new { error = ... }`) violates RFC 9457 standards, breaks automated API client generation, and fragments error handling.
```csharp
// ❌ WRONG: Anonymous error object violates RFC 9457
return BadRequest(new { success = false, message = "Name is required" });

// ✅ CORRECT: Throw DomainException; GlobalExceptionHandler emits RFC 9457 ProblemDetails
throw new DomainValidationException("Template name is required.");
```

---

### AP-004: Catching Domain Exceptions Inside Controllers
*   **The Architectural Danger:** Embedding `try-catch` blocks in Controller actions produces boilerplate duplication and bypasses centralized logging and RFC 9457 formatting.
```csharp
// ❌ WRONG: Catching exceptions manually in Controller
[HttpPost]
public async Task<IActionResult> Create(CreateTemplateRequest request) 
{
    try {
        var result = await useCase.ExecuteAsync(command);
        return Ok(result);
    } catch (ConflictException ex) {
        return Conflict(new { error = ex.Message });
    }
}

// ✅ CORRECT: Let exceptions bubble up to GlobalExceptionHandler (IExceptionHandler)
[HttpPost]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(
    [FromBody] CreateTemplateRequest request, 
    CancellationToken ct) 
{
    var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
    var result = await createTemplateUseCase.ExecuteAsync(command, ct);
    return CreatedAtAction(nameof(GetById), new { templateId = result.Id }, new ApiResponse<TemplateResultDto>(result));
}
```

---

### AP-015: Throwing Generic System Exceptions in Application / Presentation
*   **The Architectural Danger:** Throwing `System.Exception` or `KeyNotFoundException` instead of strongly-typed Domain Exceptions prevents RFC 9457 `ProblemDetails` middleware from distinguishing client errors from unhandled infrastructure crashes.
```csharp
// ❌ WRONG: Throwing generic system exceptions
throw new KeyNotFoundException($"Template {id} not found");
throw new Exception("Something failed during generation");

// ✅ CORRECT: Throw explicit Domain Exceptions mapped to RFC 9457 status codes
throw new NotFoundException($"Template '{id}' was not found.");
throw new RenderFailedException("Chromium render timed out after 30 seconds.");
```

---

### AP-016: Mutable Classes or DataAnnotations in DTO Records
*   **The Architectural Danger:** Using mutable classes with `{ get; set; }` or decorating DTOs with `[Required]`, `[MaxLength]` scatters validation logic between controllers and validators, violating FluentValidation SSoT.
```csharp
// ❌ WRONG: Mutable class with DataAnnotations attributes
public class CreateTemplateRequest 
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

// ✅ CORRECT: Immutable positional record validated by FluentValidation
public sealed record CreateTemplateRequest(Guid ProjectId, string Name, string Slug, string? Category);
```

---

### AP-017: Inline DTOs Inside UseCase Files
*   **The Architectural Danger:** Declaring DTOs inside `CreateTemplateUseCase.cs` bloats the file, obscures usecase logic, and prevents clean reuse across queries, commands, and tests.
```csharp
// ❌ WRONG: Declaring DTOs at the bottom of UseCase file
public sealed class CreateTemplateUseCase : IUseCase<...> { ... }
public sealed record TemplateResultDto(Guid Id, string Name); // Hidden in UseCase file

// ✅ CORRECT: Dedicated file in Feature DTOs folder
// File: src/SmkDoc.Application/Features/Templates/DTOs/TemplateResultDto.cs
namespace SmkDoc.Application.Features.Templates.DTOs;

public sealed record TemplateResultDto(Guid Id, Guid ProjectId, string Name, string Slug, string? Category, DateTimeOffset CreatedAt);
```

---

### AP-019: Throwing System Exceptions in UseCase Workflows
*   **The Architectural Danger:** Throwing `UnauthorizedAccessException` or `InvalidOperationException` leaks low-level runtime types; domain workflows must throw expressive domain exceptions (`ForbiddenException`, `ConflictException`, `NotFoundException`).
```csharp
// ❌ WRONG: Throwing runtime system exceptions for authorization
if (project.OwnerId != currentUserId)
    throw new UnauthorizedAccessException("Forbidden access");

// ✅ CORRECT: Throw strongly-typed ForbiddenException
if (project.OwnerId != currentUserId)
    throw new ForbiddenException($"User '{currentUserId}' is not authorized to modify Project '{project.Id}'.");
```

---

### AP-027: Imperative Role Checks Inside Controller Actions
*   **The Architectural Danger:** Writing imperative `User.IsInRole("Admin")` checks inside action methods duplicates authorization checks and is vulnerable to missing checks during refactoring.
```csharp
// ❌ WRONG: Imperative role check inside action body
[HttpPost]
public async Task<IActionResult> CreateTemplate(...) 
{
    if (!User.IsInRole("Admin")) return Forbid();
    ...
}

// ✅ CORRECT: Declarative [Authorize] attribute on Controller or Action
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> CreateTemplate(...) { ... }
```

---

### AP-028: Raw Primitive Scalars in HTTP Request Bodies
*   **The Architectural Danger:** Binding `[FromBody] string status` or `[FromBody] int version` causes JSON deserialization issues, prevents schema expansion without breaking changes, and complicates OpenAPI contract generation.
```csharp
// ❌ WRONG: Binding raw scalar string from request body
[HttpPatch("{templateId:guid}/status")]
public async Task<IActionResult> SetStatus(Guid templateId, [FromBody] string status) { ... }

// ✅ CORRECT: Positional Record Request DTO with explicit schema
public sealed record SetTemplateStatusRequest(string Status);

[HttpPatch("{templateId:guid}/status")]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> SetStatus(
    Guid templateId, 
    [FromBody] SetTemplateStatusRequest request, 
    CancellationToken ct) { ... }
```

---

### AP-029: `CreatedAtAction` Pointing to Collection Endpoints
*   **The Architectural Danger:** Returning `CreatedAtAction(nameof(List), ...)` after entity creation violates RFC 9110 / REST standards. The `Location` header must point directly to the individual created resource URI.
```csharp
// ❌ WRONG: CreatedAtAction pointing to list endpoint
return CreatedAtAction(nameof(ListTemplates), new ApiResponse<TemplateResultDto>(result));

// ✅ CORRECT: CreatedAtAction points to GetById with entity route parameter
return CreatedAtAction(
    nameof(GetById), 
    new { templateId = result.Id }, 
    new ApiResponse<TemplateResultDto>(result));
```

---

### AP-030: Unscoped Tenant Mutations (IDOR Vulnerability)
*   **The Architectural Danger:** Mutating or reading entities by primary key alone (`templateId`) without scoping to tenant context (`projectId`) creates critical IDOR security holes allowing cross-tenant data tampering.
```csharp
// ❌ WRONG: Mutating entity without tenant scope validation
var template = await templateRepo.GetByIdAsync(command.TemplateId, ct);
template.UpdateDetails(...); // Vulnerable to IDOR if template belongs to another project!

// ✅ CORRECT: Enforce tenant ownership verification on all operations
var template = await templateRepo.GetByIdAsync(command.TemplateId, ct)
    ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

if (template.ProjectId != command.ProjectId)
{
    throw new ForbiddenException($"Template '{command.TemplateId}' does not belong to Project '{command.ProjectId}'.");
}
```

---

### AP-031: Unversioned or Duplicated Route Prefixes
*   **The Architectural Danger:** Using unversioned routes (e.g., `api/templates`) or inconsistent casing (`api/v1/Template`) breaks API gateway routing and client SDK contracts.
```csharp
// ❌ WRONG: Missing API versioning or using controller token
[Route("api/[controller]")]
public sealed class TemplatesController : ControllerBase { ... }

// ✅ CORRECT: Strict canonical versioned route api/v1/{resource}
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase { ... }
```

---

### AP-032: Incorrect HTTP Status Codes for REST Operations
*   **The Architectural Danger:** Returning `200 OK` on entity creation or `200 OK` with null on missing items violates HTTP semantics and causes confusion in client HTTP libraries.
```csharp
// ❌ WRONG: Returning 200 OK on creation or deletion
[HttpPost]
public async Task<IActionResult> Create(...) => Ok(result); // Should be 201 Created

[HttpDelete("{id}")]
public async Task<IActionResult> Delete(...) => Ok(); // Should be 204 NoContent

// ✅ CORRECT: Explicit RESTful HTTP Status Codes
[HttpPost]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(...) =>
    CreatedAtAction(nameof(GetById), new { templateId = result.Id }, new ApiResponse<TemplateResultDto>(result));

[HttpDelete("{id}")]
public async Task<IActionResult> Delete(...) 
{
    await deleteUseCase.ExecuteAsync(command, ct);
    return NoContent();
}
```

---

### AP-034: Dual Routing Attributes on Controller Classes
*   **The Architectural Danger:** Combining multiple routing attributes on a single controller creates ambiguous route matches and duplicates OpenAPI path operations.
```csharp
// ❌ WRONG: Multiple route attributes on single controller
[Route("api/v1/templates")]
[Route("api/templates")]
public sealed class TemplatesController : ControllerBase { ... }

// ✅ CORRECT: Exactly one canonical route attribute
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase { ... }
```

---

### AP-035: Ad-Hoc Error Payloads in Controller Actions
*   **The Architectural Danger:** Constructing custom error objects (`return StatusCode(500, new { err = "crash" })`) bypasses RFC 9457 `ProblemDetails` compliance and breaks frontend error adapters.
```csharp
// ❌ WRONG: Manually constructed error dictionary
return StatusCode(500, new { status = "error", error_message = "Rendering failed" });

// ✅ CORRECT: Throw domain exception; GlobalExceptionHandler maps to ProblemDetails
throw new RenderFailedException("Rendering engine failed to process template.");
```

---

### AP-036: Nested Inline Object Instantiation Inside Method Calls
*   **The Architectural Danger:** Writing `await useCase.ExecuteAsync(new CreateTemplateCommand(...))` nests instantiation inside execution calls, violating Craftsmanship Rule 7 and impairing breakpoint debugging.
```csharp
// ❌ WRONG: Inline object instantiation inside execution parameter
var result = await createTemplateUseCase.ExecuteAsync(
    new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category), ct);

// ✅ CORRECT: Instantiate explicit local variable first for clear debugging
var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
var result = await createTemplateUseCase.ExecuteAsync(command, ct);
```

---

### AP-037: Inline Fully-Qualified Namespace Clutter
*   **The Architectural Danger:** Writing `System.Threading.CancellationToken` or `SmkDoc.Domain.Entities.Template` inside method signatures creates cognitive clutter and reduces scanning speed.
```csharp
// ❌ WRONG: Inline fully-qualified namespaces in method signatures
public async System.Threading.Tasks.Task<SmkDoc.Application.Features.Templates.DTOs.TemplateResultDto> Execute(
    SmkDoc.Application.Features.Templates.Commands.CreateTemplateCommand command,
    System.Threading.CancellationToken ct) { ... }

// ✅ CORRECT: Clean top-level usings with clean unqualified type names
using SmkDoc.Application.Features.Templates.Commands;
using SmkDoc.Application.Features.Templates.DTOs;

public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) { ... }
```

---

### AP-038: Underscore Prefixes or Field Re-declarations in Primary Constructors
*   **The Architectural Danger:** Re-declaring `private readonly` fields defeats the entire purpose of C# 13 Primary Constructors, introducing unnecessary visual clutter and boilerplate.
```csharp
// ❌ WRONG: Illegal underscore prefix and redundant backing field
public sealed class TemplateController(ITemplateRepository _repo) : ControllerBase 
{
    private readonly ITemplateRepository repo = _repo; // PROHIBITED
}

// ✅ CORRECT: Pure camelCase parameter consumed directly
public sealed class TemplateController(
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase) : ControllerBase 
{
    [HttpPost]
    public async Task<IActionResult> Create(...) => await createTemplateUseCase.ExecuteAsync(...);
}
```

---

### AP-053: Missing Idempotency Support on State-Mutating Endpoints
*   **The Architectural Danger:** State-mutating HTTP methods (`POST`, `PUT`, `DELETE`) without idempotency safety cause duplicate database records and duplicate billing when clients retry network timeouts.
```csharp
// ❌ WRONG: State mutation without Idempotency-Key support
[HttpPost]
public async Task<IActionResult> ChargeOrMutate([FromBody] CreateInvoiceRequest request) { ... }

// ✅ CORRECT: Supports Idempotency-Key header and [Idempotent] filter
[HttpPost]
[Idempotent]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(
    [FromBody] CreateTemplateRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    CancellationToken ct) 
{
    var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
    var result = await createTemplateUseCase.ExecuteAsync(command, ct);
    return CreatedAtAction(nameof(GetById), new { templateId = result.Id }, new ApiResponse<TemplateResultDto>(result));
}
```

---

## ⚡ Category 4: Infrastructure, Streaming & High-Throughput

### AP-006: Hardcoded `switch` or `if/else` on Render Engine Types
*   **The Architectural Danger:** Hardcoding engine types violates the Open/Closed Principle. Adding a new engine (e.g., Markdown or Typst) requires modifying core UseCase classes.
```csharp
// ❌ WRONG: Hardcoded switch/if-else ladders
IRenderEngine engine = format switch 
{
    "html" => new HtmlTemplateEngine(),
    "docx" => new DocxTemplateEngine(),
    _ => throw new NotSupportedException()
};

// ✅ CORRECT: Polymorphic Strategy Pattern via DI
public sealed class RenderDocumentUseCase(
    IEnumerable<IRenderEngine> engines) : IUseCase<RenderDocumentCommand, DocumentStreamResult> 
{
    public async Task<DocumentStreamResult> ExecuteAsync(RenderDocumentCommand command, CancellationToken ct) 
    {
        var engine = engines.FirstOrDefault(e => e.EngineType == command.EngineType)
            ?? throw new InvalidOperationException($"No engine registered for {command.EngineType}");
        var stream = await engine.RenderStreamAsync(...);
        return new DocumentStreamResult(stream, command.OutputFormat.MimeType);
    }
}
```

---

### AP-010: Hardcoded `{{}}` Regex in Parsing Engines
*   **The Architectural Danger:** Hardcoding ad-hoc regex expressions across template engines causes inconsistent placeholder parsing and edge-case rendering bugs.
```csharp
// ❌ WRONG: Ad-hoc regex declared inside local methods
var matches = Regex.Matches(templateText, @"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}");

// ✅ CORRECT: Use centralized compiled SSoT regex
using SmkDoc.Domain.Common;

var matches = PlaceholderHelper.Pattern.Matches(templateText);
```

---

### AP-011: DrawingML Image Non-Visual Drawing Properties Id = 0
*   **The Architectural Danger:** Generating DrawingML non-visual properties (`cNvPr`) with ID 0 in OpenXml Word/Excel documents causes Microsoft Office / Word to flag the document as corrupt upon opening.
```csharp
// ❌ WRONG: Using ID = 0 for DrawingML properties corrupts Word/Excel documents
var docPr = new NonVisualDrawingProperties { Id = 0, Name = "Picture 1" };

// ✅ CORRECT: Use positive integer (1-based counter) for OpenXml DrawingML IDs
var docPr = new NonVisualDrawingProperties { Id = (uint)imageIndex + 1, Name = $"Picture_{imageIndex + 1}" };
```

---

### AP-012: String-Replacement on MinIO Presigned URLs
*   **The Architectural Danger:** Using `.Replace("http://minio:9000", "http://localhost:9000")` on S3 presigned URLs invalidates the cryptographic HMAC signature, producing 403 Access Denied.
```csharp
// ❌ WRONG: String replacement breaks AWS SigV4 HMAC signature
var internalUrl = await minioClient.PresignedGetObjectAsync(args);
var publicUrl = internalUrl.Replace("http://minio:9000", "http://localhost:9000"); // 403 SignatureDoesNotMatch!

// ✅ CORRECT: Configure PublicEndpoint in MinioSettings for correct signature generation
var minioClient = new MinioClient()
    .WithEndpoint(settings.PublicEndpoint)
    .WithCredentials(settings.AccessKey, settings.SecretKey)
    .Build();
var presignedUrl = await minioClient.PresignedGetObjectAsync(args);
```

---

### AP-013: Auto-Executing Destructive Docker Commands Autonomously
*   **The Architectural Danger:** Executing `docker run` or `docker compose down -v` via automated agent commands can destroy local development databases or crash container networks.
```bash
# ❌ WRONG: Agent executing destructive shell command autonomously
docker compose down -v

# ✅ CORRECT: Agent provides command snippet for human user review and execution
# "To reset local development containers, please run:"
# docker compose down
```

---

### AP-025: Repository Methods Returning `IQueryable` or Mutable `List<T>`
*   **The Architectural Danger:** Returning `IQueryable<T>` leaks EF Core query composition into the Application layer (causing N+1 queries outside transaction boundaries); returning mutable `List<T>` allows callers to mutate the internal repository cache.
```csharp
// ❌ WRONG: Leaking IQueryable outside Infrastructure layer
public interface ITemplateRepository 
{
    IQueryable<Template> GetQueryable(); // LEAK! Application can compose arbitrary SQL
}

// ✅ CORRECT: Return Domain Entity or IReadOnlyList<T> with tenant context
public interface ITemplateRepository 
{
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct);
}
```

---

### AP-052: Memory Buffering in Document Rendering (LOH Fragmentation)
*   **The Architectural Danger:** Calling `MemoryStream.ToArray()` or holding multi-megabyte PDF byte arrays in memory allocates into the Large Object Heap (LOH), leading to severe garbage collection pauses and Out-Of-Memory (OOM) crashes under concurrency.
```csharp
// ❌ WRONG: Buffering multi-MB document into RAM
byte[] pdfBytes = await engine.RenderAsync(template, data);
using var memoryStream = new MemoryStream(pdfBytes);
await storageService.UploadAsync("outputs", key, memoryStream, "application/pdf", ct);

// ✅ CORRECT: Zero-LOH Stream-over-RAM piping directly from network to storage
await using var pdfStream = await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
await storageService.UploadAsync("outputs", key, pdfStream, "application/pdf", ct);
```

---

## 💎 Category 5: Code Craftsmanship, Readability & Simplicity

### AP-049: The Arrow Anti-Pattern & Cyclomatic Indentation > 2
*   **The Architectural Danger:** Deeply nested `if`, `foreach`, and `try` blocks bury the "happy path" of execution and drastically increase cognitive load during debugging and code review.
```csharp
// ❌ WRONG: Deeply nested indentation (Depth = 4)
public async Task ProcessItems(BatchPayload payload) 
{
    if (payload != null) 
    {
        if (payload.Items.Count > 0) 
        {
            foreach (var item in payload.Items) 
            {
                if (item.IsActive) 
                {
                    await SaveItem(item);
                }
            }
        }
    }
}

// ✅ CORRECT: Guard Clauses keep happy path linear and left-aligned (Max depth = 2)
public async Task ProcessItems(BatchPayload payload) 
{
    if (payload is null || payload.Items.Count == 0) return;

    foreach (var item in payload.Items) 
    {
        if (!item.IsActive) continue;
        await SaveItem(item);
    }
}
```

---

### AP-050: Echo / Robot Comments
*   **The Architectural Danger:** Comments that merely restate what the code clearly expresses clutter the source file, produce cognitive fatigue, and quickly rot when logic is updated.
```csharp
// ❌ WRONG: Echo comments state the obvious
// Get the template by id
var template = await templateRepo.GetByIdAsync(templateId, ct);
// If template is null, throw exception
if (template is null) throw new NotFoundException("Not found");
// Commit changes to database
await unitOfWork.CommitAsync(ct);

// ✅ CORRECT: Code explains WHAT; comments explain WHY
var template = await templateRepo.GetByIdAsync(templateId, ct)
    ?? throw new NotFoundException($"Template '{templateId}' was not found.");

// Chromium rasterizer requires a 150ms font stabilization delay
// to avoid missing glyphs in Thai complex font rendering.
await Task.Delay(RenderEngineConstants.FontRasterizationDelayMs, ct);

await unitOfWork.CommitAsync(ct);
```

---

### AP-051: Unbounded Fluent LINQ Chaining (> 3 Operations)
*   **The Architectural Danger:** Chaining multiple operations into a single uninterrupted statement impairs readability and prevents developers from inspecting intermediate results in debuggers.
```csharp
// ❌ WRONG: Unreadable long fluent chain
var activeNames = projects.SelectMany(p => p.Templates).Where(t => t.IsActive && t.Category == "Tax").OrderBy(t => t.Name.Value).Select(t => t.Name.Value).Distinct().ToList();

// ✅ CORRECT: Split into explicit intermediate variables with descriptive names
var allTemplates = projects.SelectMany(project => project.Templates);
var taxTemplates = allTemplates.Where(t => t.IsActive && t.Category == "Tax");

var activeNames = taxTemplates
    .Select(t => t.Name.Value)
    .Distinct()
    .Order()
    .ToList();
```

---

### AP-055: Disordered File Anatomy (Violating the Stepdown Rule)
*   **The Architectural Danger:** Scattering private helper methods randomly above public entry points or dumping them at the very bottom of a 400-line class forces readers to scroll chaotically.
```csharp
// ❌ WRONG: Private helper above public entry point; chaotic order
public sealed class DocumentService 
{
    private byte[] FormatHtml(...) => ...; // Helper at top
    public async Task<DocumentResult> Render(...) => FormatHtml(...); // Entry point below
}

// ✅ CORRECT: The Stepdown Rule (Public entry point first; helpers directly below)
public sealed class DocumentService 
{
    // 1. Primary entry point at top
    public async Task<DocumentResult> Render(RenderCommand command, CancellationToken ct) 
    {
        var html = FormatHtml(command);
        return await ExecuteRender(html, ct);
    }

    // 2. Private helper placed immediately below its invocation
    private string FormatHtml(RenderCommand command) => ...;

    // 3. Second helper placed directly below
    private async Task<DocumentResult> ExecuteRender(string html, CancellationToken ct) => ...;
}
```

---

### AP-056: Semantic Synonym Drift (Violating Single Term per Concept)
*   **The Architectural Danger:** Alternating between `Get`, `Fetch`, `Retrieve`, `FindById`, and `Load` for the same operation fractures developer muscle memory and leads to accidental duplicate helper methods.
```csharp
// ❌ WRONG: Inventing arbitrary synonyms across classes
public Task<Template> FetchTemplate(Guid id);
public Task<Template> RetrieveById(Guid id);
public Task<Template> LoadSingle(Guid id);

// ✅ CORRECT: Canonical Verbs across all interfaces
public Task<Template?> GetByIdAsync(Guid id, CancellationToken ct);
public Task<Template?> FindAsync(Guid projectId, TemplateSlug slug, CancellationToken ct);
public Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct);
public Task<bool> ExistsBySlugAsync(Guid projectId, TemplateSlug slug, CancellationToken ct);
```

---

## 🧪 Category 6: Unit & Integration Testing Standards

### AP-041: Hardcoding Volatile Execution Metrics in Documentation / Tests
*   **The Architectural Danger:** Writing exact volatile numbers (e.g., "assert 648 tests pass" or "we have 28 templates") in documentation or tests causes constant churn, false negatives, and doc drift.
```csharp
// ❌ WRONG: Asserting exact brittle counts in tests or docs
templates.Count.Should().Be(28); // Breaks the moment a 29th template is added!

// ✅ CORRECT: Assert invariant properties and non-empty bounds
templates.Should().NotBeEmpty();
templates.Should().AllSatisfy(t => t.ProjectId.Should().Be(projectId));
```

---

### AP-042: Monolithic Test Classes or Flat CQRS Placement
*   **The Architectural Danger:** Bundling tests for multiple commands and queries into a single 1000-line `TemplateTests.cs` violates CQRS folder parity and obscures which test covers which use case.
```text
// ❌ WRONG: Monolithic test dumping ground
tests/SmkDoc.Tests/Services/TemplateTests.cs (contains 150 tests covering all CRUD operations)

// ✅ CORRECT: 1:1 CQRS Folder Parity with single SUT per test class
tests/SmkDoc.Tests/Features/Templates/Commands/CreateTemplate/CreateTemplateUseCaseTests.cs
tests/SmkDoc.Tests/Features/Templates/Commands/UpdateTemplate/UpdateTemplateUseCaseTests.cs
tests/SmkDoc.Tests/Features/Templates/Queries/GetTemplateById/GetTemplateByIdUseCaseTests.cs
```

---

### AP-043: Heavy I/O, Generators, or Benchmarks in Unit Tests
*   **The Architectural Danger:** Running disk I/O, real network calls, or OpenXml generation in `SmkDoc.Tests` slows down local test loops and CI pull-request validation pipelines.
```csharp
// ❌ WRONG: Disk file I/O inside SmkDoc.Tests
[Fact]
public void GenerateExcel_WritesToDisk() 
{
    File.WriteAllBytes("C:/temp/test.xlsx", bytes); // PROHIBITED in Unit Tests
}

// ✅ CORRECT: SmkDoc.Tests is 100% In-Memory. Move heavy I/O to SmkDoc.IntegrationTests
// In tests/SmkDoc.IntegrationTests/Generators/ExcelGeneratorTests.cs
[Fact]
public async Task GenerateExcel_Integration_PipesToStorage() 
{
    await testContainerFixture.UploadFileAsync(...);
}
```

---

### AP-044: Ad-Hoc Entity Instantiation with Nondeterministic `DateTimeOffset.UtcNow`
*   **The Architectural Danger:** Using `DateTimeOffset.UtcNow` directly in test assertions causes flakiness due to clock skew and milliseconds rounding differences.
```csharp
// ❌ WRONG: Nondeterministic clock causes intermittent CI assertion failures
var now = DateTimeOffset.UtcNow; // Skew risk!
var template = Template.Create(projectId, name, slug, "Invoice", now);
template.CreatedAt.Should().Be(DateTimeOffset.UtcNow); // Flaky!

// ✅ CORRECT: Use deterministic baseline timestamps from TestConstants
var baseline = TestConstants.BaselineTime;
var template = Template.Create(projectId, name, slug, "Invoice", baseline);
template.CreatedAt.Should().Be(baseline);
```

---

### AP-045: Artificial Dumping Grounds for Domain Invariant Tests
*   **The Architectural Danger:** Creating artificial test classes like `InvariantValidationTests.cs` separates invariant tests from their aggregate roots and clutters the test suite.
```text
// ❌ WRONG: Artificial dumping ground for domain invariants
tests/SmkDoc.Tests/Domain/Common/AllEntityInvariantsTests.cs

// ✅ CORRECT: Domain invariants tested directly in Aggregate Root unit tests
tests/SmkDoc.Tests/Domain/Entities/TemplateTests.cs
tests/SmkDoc.Tests/Domain/Entities/ProjectTests.cs
tests/SmkDoc.Tests/Domain/Entities/ApiKeyTests.cs
```

---

### AP-046: Mocking Dependencies in Validator Tests
*   **The Architectural Danger:** Injecting mocks into Command Validator tests adds setup overhead and slows down validation suites. Validators are pure deterministic functions.
```csharp
// ❌ WRONG: Mocking repositories inside Validator tests
var repoMock = new Mock<ITemplateRepository>();
var validator = new CreateTemplateCommandValidator(repoMock.Object);

// ✅ CORRECT: Validators tested as pure functions with [Theory] + [InlineData]
public class CreateTemplateCommandValidatorTests 
{
    private readonly CreateTemplateCommandValidator sut = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenNameIsEmpty_HasValidationError(string name) 
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), name, "tax-invoice", "Finance");
        var result = sut.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
```

---

### AP-047: Calling `new UseCase(...)` in Every Individual Test Method
*   **The Architectural Danger:** Instantiating the SUT (`new CreateTemplateUseCase(...)`) in every test method creates massive test maintenance overhead whenever constructor dependencies change.
```csharp
// ❌ WRONG: Re-instantiating SUT manually across 20 test methods
[Fact]
public async Task Test1() 
{
    var sut = new CreateTemplateUseCase(repoMock.Object, uowMock.Object, timeProviderMock.Object);
    ...
}

// ✅ CORRECT: Single Source of Truth CreateSut() helper method
public class CreateTemplateUseCaseTests 
{
    private readonly Mock<ITemplateRepository> templateRepoMock = new();
    private readonly Mock<IUnitOfWork> unitOfWorkMock = new();

    private CreateTemplateUseCase CreateSut() =>
        new(templateRepoMock.Object, unitOfWorkMock.Object, TimeProvider.System);
}
```

---

### AP-048: Shallow Exception Assertions Without Checking Negative Side-Effects
*   **The Architectural Danger:** Asserting only that an exception was thrown without verifying that database transactions were aborted risks undetected data corruption bugs.
```csharp
// ❌ WRONG: Shallow assertion ignores database mutation verification
[Fact]
public async Task ExecuteAsync_WhenConflict_Throws() 
{
    var act = () => CreateSut().ExecuteAsync(command);
    await act.Should().ThrowAsync<ConflictException>();
    // Missing verification that Commit was NEVER invoked!
}

// ✅ CORRECT: Deep Semantic Assertion + Verify CommitAsync(Times.Never)
[Fact]
public async Task ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException() 
{
    var act = () => CreateSut().ExecuteAsync(command);
    await act.Should().ThrowAsync<ConflictException>()
        .WithMessage($"*'{command.Slug}'*");

    unitOfWorkMock.Verify(uow => uow.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    templateRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

---

</backend_scope>

<frontend_scope>
## 🟡 Category 7: Frontend Standards (TypeScript 5.7 / Next.js 15.2)

### AP-F001: Inline Styles or Hardcoded Color Codes
*   **The Architectural Danger:** Hardcoding hex codes (`#3b82f6`) or inline CSS styles bypasses Tailwind CSS token layers, breaks dark mode, and fragments theme customization.
```typescript
// ❌ WRONG: Hardcoded hex codes and inline styles
<div style={{ backgroundColor: "#1e293b", color: "#ffffff" }}>Preview</div>

// ✅ CORRECT: Standard Tailwind CSS semantic utility classes
<div className="bg-surface text-foreground dark:bg-surface-dark">Preview</div>
```

---

### AP-F002: Client-Side `useEffect` for Initial Data Fetching
*   **The Architectural Danger:** Fetching initial page data inside `useEffect` creates network waterfalls, causes cumulative layout shift (CLS), and breaks server-side rendering (SSR).
```typescript
// ❌ WRONG: Client-side useEffect fetching creates waterfall
"use client";
export default function TemplatesPage() {
  const [data, setData] = useState([]);
  useEffect(() => {
    fetch("/api/v1/templates").then(res => res.json()).then(setData);
  }, []);
  return <TemplateList items={data} />;
}

// ✅ CORRECT: React Server Component fetches on the server directly
// app/templates/page.tsx (Server Component by default)
export default async function TemplatesPage() {
  const templates = await getTemplates(); // Direct async fetch on server
  return <TemplateList items={templates} />;
}
```

---

### AP-F003: Using `any` in TypeScript
*   **The Architectural Danger:** Disabling the TypeScript compiler via `any` causes runtime type exceptions and breaks compile-time schema validation guarantees.
```typescript
// ❌ WRONG: Bypassing type safety with 'any'
function handlePayload(data: any) {
  console.log(data.nonExistentField.toUpperCase()); // Runtime TypeError!
}

// ✅ CORRECT: Strict schema parsing with Zod and unknown
function handlePayload(rawData: unknown) {
  const parsed = templatePayloadSchema.parse(rawData);
  console.log(parsed.name.toUpperCase());
}
```

---

### AP-F004: Legacy `.eslintrc.json` Configuration Files
*   **The Architectural Danger:** Using deprecated `.eslintrc.json` or `.eslintrc.js` in Next.js 15 projects breaks ESLint 9 Flat Config resolution and creates plugin conflicts.
```javascript
// ❌ WRONG: Deprecated legacy config .eslintrc.json
{
  "extends": ["next/core-web-vitals"]
}

// ✅ CORRECT: Modern Flat Config eslint.config.mjs
import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({ baseDirectory: __dirname });
const eslintConfig = [...compat.extends("next/core-web-vitals", "next/typescript")];

export default eslintConfig;
```

---

### AP-F005: Root-Level `"use client"` Pollution
*   **The Architectural Danger:** Marking `page.tsx` or `layout.tsx` with `"use client"` converts the entire component subtree into Client Components, disabling streaming SSR and bundling unnecessary JavaScript.
```typescript
// ❌ WRONG: Entire page turned into Client Component
// app/templates/[id]/page.tsx
"use client";
export default function Page({ params }: { params: { id: string } }) { ... }

// ✅ CORRECT: Server Component page wrapping interactive client leaf component
// app/templates/[id]/page.tsx (Server Component)
import { TemplateEditorClient } from "@/components/templates/TemplateEditorClient";

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const initialData = await getTemplateById(id);
  return <TemplateEditorClient initialData={initialData} />;
}
```

---

### AP-F006: In-Flight Preview Race Conditions (Missing `AbortController`)
*   **The Architectural Danger:** Triggering HTTP preview generation requests on rapid keystrokes without canceling pending requests causes out-of-order response arrivals that overwrite newer state.
```typescript
// ❌ WRONG: Rapid keystrokes trigger overlapping requests; slow request overwrites new preview
async function onCodeChange(newContent: string) {
  const blob = await fetchPreview(newContent);
  setPreviewUrl(URL.createObjectURL(blob));
}

// ✅ CORRECT: AbortController cancels pending request on each keystroke
let activeController: AbortController | null = null;

async function onCodeChange(content: string) {
  activeController?.abort();
  activeController = new AbortController();

  try {
    const blob = await fetchPreview(content, activeController.signal);
    setPreviewUrl(URL.createObjectURL(blob));
  } catch (err: unknown) {
    if (err instanceof DOMException && err.name === "AbortError") return;
    console.error(err);
  }
}
```

---

### AP-F007: Manual Type Duplication (Bypassing Zod SSoT)
*   **The Architectural Danger:** Manually writing TypeScript interfaces when a Zod schema exists creates schema drift when validation rules are updated.
```typescript
// ❌ WRONG: Redundant manual interface diverges from schema
export const createTemplateSchema = z.object({ name: z.string().min(2) });
export interface CreateTemplateInput { name: string; } // DRIFT RISK!

// ✅ CORRECT: Derive types directly from Zod Schema as SSoT
export const createTemplateSchema = z.object({ name: z.string().min(2) });
export type CreateTemplateInput = z.infer<typeof createTemplateSchema>;
```

---

### AP-F008: Toasting or Swallowing Intentional `AbortError` Exceptions
*   **The Architectural Danger:** Treating intentional request cancellations as server errors displays confusing error banners to users when they simply typed quickly.
```typescript
// ❌ WRONG: Showing error alerts on intentional aborts
try {
  await fetchPreview(content, signal);
} catch (err) {
  showErrorToast("Failed to generate preview"); // Annoying false alarm on typing!
}

// ✅ CORRECT: Inspect error name and silently exit on AbortError
try {
  await fetchPreview(content, signal);
} catch (err: unknown) {
  if (err instanceof DOMException && err.name === "AbortError") {
    return; // Silent intentional cancellation
  }
  showErrorToast("An unexpected preview generation error occurred.");
}
```

---

</frontend_scope>
