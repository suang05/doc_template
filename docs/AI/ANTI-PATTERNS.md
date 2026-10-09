# ANTI-PATTERNS.md — Prohibited Patterns & Pitfalls (The PR Review Shield)

> **Purpose:** This document is the definitive negative-constraint inventory of strictly prohibited code patterns, anti-patterns, and architectural regressions across the SMK Document Server (`backend-v2/` and `frontend-v2/`).
> **[AI_DIRECTIVE]:** AI Assistants (LLMs) and human code reviewers MUST treat these rules as non-negotiable blocking constraints. If any code matches a prohibited pattern in this document, it MUST be immediately rejected during PR review and refactored to the corresponding golden standard.
> **Single Source of Truth (SSoT):** To prevent documentation drift, detailed positive implementations are linked directly to [PATTERNS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md) and [CODING_CONVENTIONS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md).

<ai_directive>
CRITICAL ATTENTION ROUTING:
- For Backend (C# 13 / .NET 10), STRICTLY enforce Categories 1 through 6 (`<backend_scope>`).
- For Frontend (TypeScript 5.7 / Next.js 15.2), STRICTLY enforce Category 7 (`<frontend_scope>`).
- For Positive Architectural Archetypes, refer directly to `docs/AI/PATTERNS.md`.
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
| **AP-057** | Magic Numbers & Inlined Timeouts | `IOptions<T>` for operational configs, `*Constants` for technical invariants, `TimeProvider` |

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
*   **The Architectural Danger:** Violates Clean Architecture boundaries and turns Controllers into fat orchestrators, bypassing Application validation filters and security pipelines.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Controller directly depends on data persistence
public sealed class TemplateController(ITemplateRepository templateRepo) : ControllerBase { ... }

// ✅ CORRECT: Controller delegates to Single-Responsibility UseCase
public sealed class TemplateController(
    IUseCase<GetTemplateByIdQuery, TemplateResultDto> getTemplateByIdUseCase) : ControllerBase { ... }
```

---

### AP-002: Leaking Domain Entities Across Boundaries
*   **The Architectural Danger:** Exposing Domain Entities to outer layers leads to unintended state mutations, circular dependencies, and mass-assignment vulnerabilities.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 6: Action-Centric CQRS UseCase Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-6-action-centric-cqrs-usecase-pattern)
```csharp
// ❌ WRONG: Returning Domain Entity from UseCase
public async Task<Template> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) => template;

// ✅ CORRECT: Map to immutable Positional Record DTO before returning
public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) =>
    new(template.Id, template.ProjectId, template.Name.Value, template.Slug.Value, template.Category, template.CreatedAt);
```

---

### AP-005: Importing Infrastructure Details into Application Layer
*   **The Architectural Danger:** Direct dependencies on `AppDbContext` or EF Core in Application break Dependency Inversion and couple business workflows to relational persistence details.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 5: Atomic Unit of Work & Transaction Boundary)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-5-atomic-unit-of-work--transaction-boundary)
```csharp
// ❌ WRONG: Application layer directly references EF Core persistence
using SmkDoc.Infrastructure.Persistence;
public sealed class CreateTemplateUseCase(AppDbContext dbContext) { ... }

// ✅ CORRECT: Depend exclusively on Domain/Application abstractions
public sealed class CreateTemplateUseCase(ITemplateRepository templateRepo, IUnitOfWork unitOfWork) { ... }
```

---

### AP-007: Framework Types in UseCases
*   **The Architectural Danger:** Injecting `IFormFile`, `HttpContext`, or `HttpRequest` into UseCases couples Application workflows to ASP.NET Core, preventing reuse in background workers or CLI tools.
```csharp
// ❌ WRONG: Passing ASP.NET Core HTTP framework types into UseCase
public sealed record UploadTemplateCommand(IFormFile File, HttpContext Context);

// ✅ CORRECT: Pass pure C# primitives and streams
public sealed record UploadTemplateCommand(Stream FileStream, string FileName, string ContentType);
```

---

### AP-014: Modifying Legacy v1 Directories
*   **The Architectural Danger:** Editing legacy v1 folders (`src/`, `backend/`, `frontend/`) re-introduces deprecated architectural anti-patterns and creates merge conflicts.
```text
// ❌ WRONG: Modifying legacy codebase files
src/SmkDocServer/Services/TemplateService.cs

// ✅ CORRECT: Implement exclusively inside modern v2 architecture
backend-v2/src/SmkDoc.Application/Features/Templates/Commands/CreateTemplate/CreateTemplateUseCase.cs
```

---

### AP-018: Multi-Method Fat Use Case Classes
*   **The Architectural Danger:** Consolidating multiple actions into a single service class violates the Single Responsibility Principle and creates monolithic merge conflicts.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 6: Action-Centric CQRS UseCase Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-6-action-centric-cqrs-usecase-pattern)
```csharp
// ❌ WRONG: Fat service class with multiple operational intents
public class TemplateService { public Task Create(...) { } public Task Render(...) { } }

// ✅ CORRECT: 1 Business Intent = 1 Action-Centric UseCase Class
public sealed class CreateTemplateUseCase : IUseCase<CreateTemplateCommand, TemplateResultDto> { ... }
public sealed class RenderDocumentUseCase : IUseCase<RenderDocumentCommand, DocumentStreamResult> { ... }
```

---

### AP-020: Generic `IRepository<T>` when Aggregate Domain Repository Exists
*   **The Architectural Danger:** Using a generic repository abstraction bypasses domain-specific Aggregate Root invariants, specialized queries, and tenant-scoped security checks.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Bare generic repository bypasses Aggregate Root domain methods
public sealed class GetTemplateUseCase(IRepository<Template> genericRepo) { ... }

// ✅ CORRECT: Explicit domain repository enforcing tenant-scoped invariants
public sealed class GetTemplateUseCase(ITemplateRepository templateRepo) { ... }
```

---

### AP-026: Request DTO Inheriting from Command DTO / Shallow Subclassing
*   **The Architectural Danger:** Having Presentation Request records inherit from Application Command records couples transport schema to orchestration, hindering independent versioning.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Presentation Request inherits from Application Command
public sealed record CreateTemplateRequest(...) : CreateTemplateCommand(...);

// ✅ CORRECT: Fully decoupled records with explicit mapping in Controller
var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
```

---

### AP-033: Direct Infrastructure / External Service Injection in Controller
*   **The Architectural Danger:** Injecting infrastructure services (`IMinioStorageService`, `IGotenbergClient`) directly into Controllers bypasses application business rules and audit boundaries.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Controller directly calls external cloud storage
public sealed class DocumentController(IMinioStorageService storageService) : ControllerBase { ... }

// ✅ CORRECT: Controller delegates to Single-Responsibility UseCase
public sealed class DocumentController(
    IUseCase<UploadDocumentCommand, DocumentResultDto> uploadDocumentUseCase) : ControllerBase { ... }
```

---

## 💎 Category 2: Domain-Driven Design & Invariant Protection

### AP-008: Anemic Entities with Public Property Setters
*   **The Architectural Danger:** Public setters allow external callers to mutate entity properties into unverified states, bypassing domain invariants.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Anemic Entity with public setters and object initializer
public class Template { public string Name { get; set; } }
var template = new Template { Name = "Invoice" };

// ✅ CORRECT: Rich Domain Entity with private setters, Canonical Factory, and expressive verbs
public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
    new(null, projectId, name, slug, category, now);
```

---

### AP-009: String Comparisons for Smart Enums
*   **The Architectural Danger:** Comparing Smart Enums using string literals bypasses compiler type checking and introduces silent runtime defects from casing mismatches or typos.
```csharp
// ❌ WRONG: String comparison against Smart Enum name
if (template.Format.Name == "Html") { ... }

// ✅ CORRECT: Type-safe instance comparison using Smart Enum SSoT
if (template.Format == TemplateFormat.Html) { ... }
```

---

### AP-021: Manual Identity Override on UUIDv7 Entities
*   **The Architectural Danger:** Overriding entity primary keys manually defeats chronological UUIDv7 sorting in PostgreSQL B-Tree indexes, causing page splits and database fragmentation.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Overriding Id manually with random GUID
var template = new Template { Id = Guid.NewGuid() };

// ✅ CORRECT: BaseEntity automatically assigns Guid.CreateVersion7()
public abstract class BaseEntity { public Guid Id { get; protected set; } = Guid.CreateVersion7(); }
```

---

### AP-022: Public Mutable Collections on Entities
*   **The Architectural Danger:** Exposing mutable collections (`List<T>`) allows external code to add or remove children without enforcing Aggregate Root boundary validation.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Public mutable collection allows direct external modification
public class Project : BaseEntity { public List<ApiKey> ApiKeys { get; set; } = []; }

// ✅ CORRECT: Encapsulated backing list exposed as IReadOnlyCollection
private readonly List<ApiKey> apiKeys = [];
public IReadOnlyCollection<ApiKey> ApiKeys => apiKeys.AsReadOnly();
```

---

### AP-023: Weakening Domain Invariants to Make Tests or UseCases Pass
*   **The Architectural Danger:** Relaxing entity or Value Object validation rules to bypass test setup issues compromises data integrity across the system and allows corrupted data in production.
```csharp
// ❌ WRONG: Weakening domain invariant to appease failing test
if (string.IsNullOrEmpty(value)) return new TemplateSlug("default-slug"); // DANGEROUS HACK

// ✅ CORRECT: Enforce strict invariant; update test fixture to provide valid domain data
if (string.IsNullOrWhiteSpace(value) || !SlugRegex().IsMatch(value))
    throw new DomainValidationException("Slug must contain lowercase alphanumeric characters.");
```

---

### AP-024: Throwing Generic System Exceptions in Domain Layer
*   **The Architectural Danger:** Throwing `ArgumentException` or `InvalidOperationException` prevents exception middleware from categorizing errors, converting client validation bugs into 500 errors.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 2: RFC 9457 Global Exception Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline)
```csharp
// ❌ WRONG: Throwing generic runtime exceptions from Domain entities
if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty");

// ✅ CORRECT: Throw strongly-typed Domain Validation exceptions
if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Template name cannot be empty.");
```

---

### AP-039: Primitive Overloads & Optional Timestamp Fallbacks in Domain Factories
*   **The Architectural Danger:** Providing factory overloads taking raw primitives or defaulting `DateTimeOffset? now = null` to `DateTimeOffset.UtcNow` introduces non-determinism.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Factory overload with primitives and optional timestamp fallback
public static Template Create(string name, DateTimeOffset? now = null) => ...;

// ✅ CORRECT: Exactly 1 Canonical Factory with strongly-typed Value Objects + mandatory now
public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
    new(null, projectId, name, slug, category, now);
```

---

### AP-040: Test Backdoors in `SmkDoc.Domain.dll`
*   **The Architectural Danger:** Adding test-only methods or `#if DEBUG` public setters to production domain classes pollutes the domain binary and compromises encapsulation.
```csharp
// ❌ WRONG: Test backdoor inside production domain class
#if DEBUG public void SetIdForTesting(Guid id) => Id = id; #endif

// ✅ CORRECT: Internal constructor visible strictly to test assemblies via InternalsVisibleTo
internal Template(Guid? id, Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now)
    : base(id, now) { ... }
```

---

### AP-054: Primitive Obsession in Repository Signatures
*   **The Architectural Danger:** Accepting raw `string` parameters instead of strongly-typed Value Objects bypasses domain regex validation and frequently leads to missing tenant isolation (IDOR).
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 4: Pure Rich Domain Entity & Value Object Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-4-pure-rich-domain-entity--value-object-pattern)
```csharp
// ❌ WRONG: Raw string slug without tenant scope
Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

// ✅ CORRECT: Strongly-typed Value Object with tenant parameter first
Task<bool> ExistsBySlugAsync(Guid projectId, TemplateSlug slug, CancellationToken ct = default);
```

---

## 🌐 Category 3: Presentation, Routing, RFC 9457 & Idempotency

### AP-003: Anonymous Error Objects in API Responses
*   **The Architectural Danger:** Returning anonymous objects (`new { error = ... }`) violates RFC 9457 standards, breaks automated API client generation, and fragments error handling.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 2: RFC 9457 Global Exception Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline)
```csharp
// ❌ WRONG: Anonymous error object violates RFC 9457
return BadRequest(new { success = false, message = "Name is required" });

// ✅ CORRECT: Throw DomainException; GlobalExceptionHandler emits RFC 9457 ProblemDetails
throw new DomainValidationException("Template name is required.");
```

---

### AP-004: Catching Domain Exceptions Inside Controllers
*   **The Architectural Danger:** Embedding `try-catch` blocks in Controller actions produces boilerplate duplication and bypasses centralized logging and RFC 9457 formatting.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Catching exceptions manually in Controller
try { return Ok(await useCase.ExecuteAsync(command)); } catch (ConflictException ex) { return Conflict(...); }

// ✅ CORRECT: Thin controller; let exceptions bubble to IExceptionHandler
var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
var result = await createTemplateUseCase.ExecuteAsync(command, ct);
return CreatedAtAction(nameof(GetById), new { templateId = result.Id }, new ApiResponse<TemplateResultDto>(result));
```

---

### AP-015: Throwing Generic System Exceptions in Application / Presentation
*   **The Architectural Danger:** Throwing `System.Exception` or `KeyNotFoundException` prevents RFC 9457 `ProblemDetails` middleware from distinguishing client errors from unhandled crashes.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 2: RFC 9457 Global Exception Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline)
```csharp
// ❌ WRONG: Throwing generic system exceptions
throw new KeyNotFoundException($"Template {id} not found");

// ✅ CORRECT: Throw explicit Domain Exceptions mapped to RFC 9457 status codes
throw new NotFoundException($"Template '{id}' was not found.");
```

---

### AP-016: Mutable Classes or DataAnnotations in DTO Records
*   **The Architectural Danger:** Using mutable classes with `{ get; set; }` and DataAnnotations scatters validation logic away from FluentValidation SSoT.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 7: Dual-Engine Validation Pipeline Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-7-dual-engine-validation-pipeline-pattern)
```csharp
// ❌ WRONG: Mutable class with DataAnnotations attributes
public class CreateTemplateRequest { [Required] public string Name { get; set; } }

// ✅ CORRECT: Immutable positional record validated by FluentValidation
public sealed record CreateTemplateRequest(Guid ProjectId, string Name, string Slug, string? Category);
```

---

### AP-017: Inline DTOs Inside UseCase Files
*   **The Architectural Danger:** Declaring DTOs inside `CreateTemplateUseCase.cs` bloats the file, obscures usecase logic, and prevents clean reuse.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 6: Action-Centric CQRS UseCase Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-6-action-centric-cqrs-usecase-pattern)
```csharp
// ❌ WRONG: Declaring DTO records at the bottom of UseCase file
public sealed class CreateTemplateUseCase : IUseCase<...> { ... }
public sealed record TemplateResultDto(...); // Hidden in UseCase file

// ✅ CORRECT: Dedicated file in Features/{Feature}/DTOs/{DtoName}.cs
namespace SmkDoc.Application.Features.Templates.DTOs;
public sealed record TemplateResultDto(Guid Id, Guid ProjectId, string Name, string Slug, string? Category, DateTimeOffset CreatedAt);
```

---

### AP-019: Throwing System Exceptions in UseCase Workflows
*   **The Architectural Danger:** Throwing `UnauthorizedAccessException` or `InvalidOperationException` leaks low-level runtime types.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 2: RFC 9457 Global Exception Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline)
```csharp
// ❌ WRONG: Throwing runtime system exceptions for authorization
if (project.OwnerId != currentUserId) throw new UnauthorizedAccessException("Forbidden");

// ✅ CORRECT: Throw strongly-typed ForbiddenException
if (project.OwnerId != currentUserId)
    throw new ForbiddenException($"User '{currentUserId}' is not authorized to modify Project '{project.Id}'.");
```

---

### AP-027: Imperative Role Checks Inside Controller Actions
*   **The Architectural Danger:** Writing imperative `User.IsInRole()` checks inside action methods duplicates authorization checks and is vulnerable to missing checks during refactoring.
```csharp
// ❌ WRONG: Imperative role check inside action body
if (!User.IsInRole("Admin")) return Forbid();

// ✅ CORRECT: Declarative [Authorize] attribute on Controller or Action
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(...) { ... }
```

---

### AP-028: Raw Primitive Scalars in HTTP Request Bodies
*   **The Architectural Danger:** Binding `[FromBody] string status` causes JSON deserialization issues, prevents schema expansion without breaking changes, and complicates OpenAPI generation.
```csharp
// ❌ WRONG: Binding raw scalar string from request body
[HttpPatch("{id}/status")] public Task<IActionResult> SetStatus([FromBody] string status) { ... }

// ✅ CORRECT: Positional Record Request DTO with explicit schema
public sealed record SetTemplateStatusRequest(string Status);
[HttpPatch("{id}/status")] public Task<IActionResult> SetStatus([FromBody] SetTemplateStatusRequest request) { ... }
```

---

### AP-029: `CreatedAtAction` Pointing to Collection Endpoints
*   **The Architectural Danger:** Returning `CreatedAtAction(nameof(List), ...)` after entity creation violates REST standards. The `Location` header must point directly to the individual created resource URI.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: CreatedAtAction pointing to collection list endpoint
return CreatedAtAction(nameof(ListTemplates), new ApiResponse<TemplateResultDto>(result));

// ✅ CORRECT: CreatedAtAction points to GetById with entity route parameter
return CreatedAtAction(nameof(GetById), new { templateId = result.Id }, new ApiResponse<TemplateResultDto>(result));
```

---

### AP-030: Unscoped Tenant Mutations (IDOR Vulnerability)
*   **The Architectural Danger:** Mutating or reading entities by primary key alone (`templateId`) without scoping to tenant context (`projectId`) creates critical IDOR security holes allowing cross-tenant data tampering.
```csharp
// ❌ WRONG: Mutating entity without tenant scope validation
var template = await templateRepo.GetByIdAsync(command.TemplateId, ct);

// ✅ CORRECT: Enforce tenant ownership verification on all operations
var template = await templateRepo.GetByIdAsync(command.TemplateId, ct)
    ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");
if (template.ProjectId != command.ProjectId)
    throw new ForbiddenException($"Template does not belong to Project '{command.ProjectId}'.");
```

---

### AP-031: Unversioned or Duplicated Route Prefixes
*   **The Architectural Danger:** Using unversioned routes (e.g., `api/templates`) or token-based routes breaks API gateway routing and client SDK contracts.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Missing API versioning or using controller token
[Route("api/[controller]")]

// ✅ CORRECT: Strict canonical versioned route api/v1/{resource}
[Route("api/v1/templates")]
```

---

### AP-032: Incorrect HTTP Status Codes for REST Operations
*   **The Architectural Danger:** Returning `200 OK` on entity creation or `200 OK` with null on deletion violates HTTP semantics and causes confusion in client HTTP libraries.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 1: Thin Controller & API Envelope Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern)
```csharp
// ❌ WRONG: Returning 200 OK on creation or deletion
[HttpPost] public async Task<IActionResult> Create(...) => Ok(result);

// ✅ CORRECT: Explicit RESTful HTTP Status Codes
[HttpPost] public async Task<ActionResult<ApiResponse<T>>> Create(...) => CreatedAtAction(...);
[HttpDelete("{id}")] public async Task<IActionResult> Delete(...) => NoContent();
```

---

### AP-034: Dual Routing Attributes on Controller Classes
*   **The Architectural Danger:** Combining multiple routing attributes on a single controller creates ambiguous route matches and duplicates OpenAPI path operations.
```csharp
// ❌ WRONG: Multiple route attributes on single controller
[Route("api/v1/templates")]
[Route("api/templates")]

// ✅ CORRECT: Exactly one canonical route attribute
[Route("api/v1/templates")]
```

---

### AP-035: Ad-Hoc Error Payloads in Controller Actions
*   **The Architectural Danger:** Constructing custom error objects (`return StatusCode(500, new { err = "crash" })`) bypasses RFC 9457 `ProblemDetails` compliance and breaks frontend error adapters.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 2: RFC 9457 Global Exception Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline)
```csharp
// ❌ WRONG: Manually constructed error dictionary
return StatusCode(500, new { status = "error", error_message = "Rendering failed" });

// ✅ CORRECT: Throw domain exception; GlobalExceptionHandler maps to ProblemDetails
throw new RenderFailedException("Rendering engine failed to process template.");
```

---

### AP-036: Nested Inline Object Instantiation Inside Method Calls
*   **The Architectural Danger:** Nesting object instantiation inside execution calls violates Craftsmanship Rule 7 and impairs breakpoint debugging.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 7: Explicit Locals Over Nested Expressions)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-7-explicit-locals-over-nested-expressions)
```csharp
// ❌ WRONG: Inline object instantiation inside execution parameter
var result = await useCase.ExecuteAsync(new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category), ct);

// ✅ CORRECT: Instantiate explicit local variable first for clear debugging
var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug, request.Category);
var result = await useCase.ExecuteAsync(command, ct);
```

---

### AP-037: Inline Fully-Qualified Namespace Clutter
*   **The Architectural Danger:** Writing `System.Threading.CancellationToken` or `SmkDoc.Domain.Entities.Template` inside method signatures creates cognitive clutter and reduces scanning speed.
```csharp
// ❌ WRONG: Inline fully-qualified namespaces in method signatures
public async System.Threading.Tasks.Task<SmkDoc.Domain.Entities.Template> Execute(...)

// ✅ CORRECT: Clean top-level usings with clean unqualified type names
using SmkDoc.Domain.Entities;
public async Task<Template> ExecuteAsync(...)
```

---

### AP-038: Underscore Prefixes or Field Re-declarations in Primary Constructors
*   **The Architectural Danger:** Re-declaring `private readonly` fields defeats the purpose of C# 13 Primary Constructors, introducing unnecessary visual clutter and boilerplate.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 4.1: Primary Constructors & DI Standards)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#41-primary-constructors--di-standards)
```csharp
// ❌ WRONG: Illegal underscore prefix and redundant backing field
public sealed class TemplateController(ITemplateRepository _repo) : ControllerBase 
{
    private readonly ITemplateRepository repo = _repo; // PROHIBITED
}

// ✅ CORRECT: Pure camelCase parameter consumed directly (Zero _)
public sealed class TemplateController(
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase) : ControllerBase { ... }
```

---

### AP-053: Missing Idempotency Support on State-Mutating Endpoints
*   **The Architectural Danger:** State-mutating HTTP methods (`POST`, `PUT`, `DELETE`) without idempotency safety cause duplicate database records and duplicate billing when clients retry network timeouts.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 3: IETF Idempotency-Key Pipeline Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-3-ietf-idempotency-key-pipeline-pattern)
```csharp
// ❌ WRONG: State mutation without Idempotency-Key support
[HttpPost] public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request) { ... }

// ✅ CORRECT: Supports Idempotency-Key header and [Idempotent] filter
[HttpPost]
[Idempotent]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(
    [FromBody] CreateTemplateRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    CancellationToken ct) { ... }
```

---

## ⚡ Category 4: Infrastructure, Streaming & High-Throughput

### AP-006: Hardcoded `switch` or `if/else` on Render Engine Types
*   **The Architectural Danger:** Hardcoding engine types violates OCP. Adding a new engine (e.g., Markdown or Typst) requires modifying core UseCase classes.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 9: Polymorphic Render Strategy Engine Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-9-polymorphic-render-strategy-engine-pattern)
```csharp
// ❌ WRONG: Hardcoded switch/if-else ladders
IRenderEngine engine = format switch { "html" => new HtmlTemplateEngine(), _ => throw new NotSupportedException() };

// ✅ CORRECT: Polymorphic Strategy Pattern via DI
public sealed class RenderDocumentUseCase(IEnumerable<IRenderEngine> engines) { ... }
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
var publicUrl = internalUrl.Replace("http://minio:9000", "http://localhost:9000"); // 403 SignatureDoesNotMatch!

// ✅ CORRECT: Configure PublicEndpoint in MinioSettings for correct signature generation
var minioClient = new MinioClient().WithEndpoint(settings.PublicEndpoint)...Build();
```

---

### AP-013: Auto-Executing Destructive Docker Commands Autonomously
*   **The Architectural Danger:** Executing `docker run` or `docker compose down -v` via automated agent commands can destroy local development databases or crash container networks.
```bash
# ❌ WRONG: Agent executing destructive shell command autonomously
docker compose down -v

# ✅ CORRECT: Agent provides command snippet for human user review and execution
# Run manually: docker compose down
```

---

### AP-025: Repository Methods Returning `IQueryable` or Mutable `List<T>`
*   **The Architectural Danger:** Returning `IQueryable<T>` leaks EF Core query composition into Application (causing N+1 queries outside transaction boundaries); returning mutable `List<T>` allows callers to mutate internal state.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 5: Atomic Unit of Work & Transaction Boundary)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-5-atomic-unit-of-work--transaction-boundary)
```csharp
// ❌ WRONG: Leaking IQueryable outside Infrastructure layer
public interface ITemplateRepository { IQueryable<Template> GetQueryable(); }

// ✅ CORRECT: Return Domain Entity or IReadOnlyList<T> with tenant context
public interface ITemplateRepository { Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct); }
```

---

### AP-052: Memory Buffering in Document Rendering (LOH Fragmentation)
*   **The Architectural Danger:** Calling `MemoryStream.ToArray()` or holding multi-megabyte PDF byte arrays in memory allocates into the Large Object Heap (LOH), leading to severe garbage collection pauses and OOM crashes under concurrency.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 8: Zero-LOH Stream-over-RAM Direct Pipeline)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-8-zero-loh-stream-over-ram-direct-pipeline)
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
*   **The Architectural Danger:** Deeply nested `if`, `foreach`, and `try` blocks bury the "happy path" of execution and drastically increase cognitive load during debugging.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 3: Linear Storytelling & Guard Clauses)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-3-linear-storytelling--guard-clauses)
```csharp
// ❌ WRONG: Deeply nested indentation (Depth = 4)
if (payload != null) { if (payload.Items.Count > 0) { foreach (var item in payload.Items) { if (item.IsActive) { ... } } } }

// ✅ CORRECT: Guard Clauses keep happy path linear and left-aligned (Max depth = 2)
if (payload is null || payload.Items.Count == 0) return;
foreach (var item in payload.Items) { if (!item.IsActive) continue; await SaveItem(item); }
```

---

### AP-050: Echo / Robot Comments
*   **The Architectural Danger:** Comments that merely restate what code clearly expresses clutter source files, produce cognitive fatigue, and quickly rot when logic is updated.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 6: Zero Echo Comments)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-6-zero-echo-comments)
```csharp
// ❌ WRONG: Echo comments state the obvious
// Get the template by id
var template = await templateRepo.GetByIdAsync(templateId, ct);

// ✅ CORRECT: Code explains WHAT; comments explain WHY
// Chromium rasterizer requires 150ms stabilization delay for Thai complex font glyphs.
await Task.Delay(RenderEngineConstants.FontRasterizationDelayMs, ct);
```

---

### AP-051: Unbounded Fluent LINQ Chaining (> 3 Operations)
*   **The Architectural Danger:** Chaining multiple operations into a single uninterrupted statement impairs readability and prevents inspecting intermediate results in debuggers.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 7: Explicit Locals Over Nested Expressions)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-7-explicit-locals-over-nested-expressions)
```csharp
// ❌ WRONG: Unreadable long fluent chain
var activeNames = projects.SelectMany(p => p.Templates).Where(t => t.IsActive && t.Category == "Tax").OrderBy(t => t.Name.Value).Select(t => t.Name.Value).Distinct().ToList();

// ✅ CORRECT: Split into explicit intermediate variables with descriptive names
var allTemplates = projects.SelectMany(project => project.Templates);
var taxTemplates = allTemplates.Where(t => t.IsActive && t.Category == "Tax");
var activeNames = taxTemplates.Select(t => t.Name.Value).Distinct().Order().ToList();
```

---

### AP-055: Disordered File Anatomy (Violating the Stepdown Rule)
*   **The Architectural Danger:** Scattering private helper methods randomly above public entry points or dumping them at the very bottom forces readers to scroll chaotically.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 1: The Stepdown Rule / Newspaper Metaphor)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-1-the-stepdown-rule-newspaper-metaphor)
```csharp
// ❌ WRONG: Private helper above public entry point; chaotic order
public sealed class DocumentService { private byte[] Format(...) => ...; public async Task Render(...) => ...; }

// ✅ CORRECT: Public entry point at top; private helpers placed directly beneath the calling method
public sealed class DocumentService 
{
    public async Task<DocumentResult> Render(...) => await ExecuteRender(FormatHtml(...), ct);
    private string FormatHtml(...) => ...;
    private async Task<DocumentResult> ExecuteRender(...) => ...;
}
```

---

### AP-056: Semantic Synonym Drift (Violating Single Term per Concept)
*   **The Architectural Danger:** Alternating between `Get`, `Fetch`, `Retrieve`, `FindById`, and `Load` for the same operation fractures developer muscle memory.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 5: Single Term per Concept)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#rule-5-single-term-per-concept)
```csharp
// ❌ WRONG: Inventing arbitrary synonyms across classes
public Task<Template> FetchTemplate(Guid id);
public Task<Template> RetrieveById(Guid id);

// ✅ CORRECT: Standardized canonical verbs across all interfaces
public Task<Template?> GetByIdAsync(Guid id, CancellationToken ct);
public Task<Template?> FindAsync(Guid projectId, TemplateSlug slug, CancellationToken ct);
public Task<IReadOnlyList<Template>> ListByProjectAsync(Guid projectId, CancellationToken ct);
public Task<bool> ExistsBySlugAsync(Guid projectId, TemplateSlug slug, CancellationToken ct);
```

---

### AP-057: Magic Numbers & Inlined Timeouts instead of IOptions or Constants
*   **The Architectural Danger:** Hardcoding numeric thresholds or timeouts inline scatters operational configuration across the codebase, forcing code recompilation for simple DevOps tuning and causing non-deterministic test failures.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Rule 10: Zero Magic Numbers & Centralized Configuration)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#310-zero-magic-numbers--centralized-configuration-ioptions--constants)
```csharp
// ❌ WRONG: Inlined magic numbers, hardcoded delays, and clock calls
await Task.Delay(150);
var expiry = DateTimeOffset.UtcNow.AddMinutes(15);
if (file.Length > 10485760) throw new Exception("Too large");

// ✅ CORRECT: Strongly-typed IOptions<T>, Centralized Constants, and TimeProvider
await Task.Delay(RenderEngineConstants.FontRasterizationDelayMs, ct);
var expiry = timeProvider.GetUtcNow().Add(jwtSettings.Value.AccessTokenLifetime);
if (file.Length > uploadSettings.Value.MaxFileSizeBytes) throw new FileSizeExceededException(...);
```

---

## 🧪 Category 6: Unit & Integration Testing Standards

### AP-041: Hardcoding Volatile Execution Metrics in Documentation / Tests
*   **The Architectural Danger:** Writing exact volatile numbers (e.g., "assert 648 tests pass" or "we have 28 templates") in documentation or tests causes constant churn and false negatives.
```csharp
// ❌ WRONG: Asserting exact brittle counts in tests or docs
templates.Count.Should().Be(28); // Breaks the moment a 29th template is added!

// ✅ CORRECT: Assert invariant properties and non-empty bounds
templates.Should().NotBeEmpty();
templates.Should().AllSatisfy(t => t.ProjectId.Should().Be(projectId));
```

---

### AP-042: Monolithic Test Classes or Flat CQRS Placement
*   **The Architectural Danger:** Bundling tests for multiple commands and queries into a single 1000-line `TemplateTests.cs` violates CQRS folder parity and obscures test coverage.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 5: Unit Test Golden Archetypes)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#5-unit-test-golden-archetypes)
```text
// ❌ WRONG: Monolithic test dumping ground
tests/SmkDoc.Tests/Services/TemplateTests.cs (contains 150 tests covering all CRUD operations)

// ✅ CORRECT: 1:1 CQRS Folder Parity with single SUT per test class
tests/SmkDoc.Tests/Features/Templates/Commands/CreateTemplate/CreateTemplateUseCaseTests.cs
tests/SmkDoc.Tests/Features/Templates/Queries/GetTemplateById/GetTemplateByIdUseCaseTests.cs
```

---

### AP-043: Heavy I/O, Generators, or Benchmarks in Unit Tests
*   **The Architectural Danger:** Running disk I/O, real network calls, or OpenXml generation in `SmkDoc.Tests` slows down local test loops and CI pull-request validation pipelines.
```csharp
// ❌ WRONG: Disk file I/O inside SmkDoc.Tests
[Fact] public void GenerateExcel_WritesToDisk() => File.WriteAllBytes("C:/temp/test.xlsx", bytes);

// ✅ CORRECT: SmkDoc.Tests is 100% in-memory. Move heavy I/O to SmkDoc.IntegrationTests
// In tests/SmkDoc.IntegrationTests/Generators/ExcelGeneratorTests.cs
[Fact] public async Task GenerateExcel_Integration_PipesToStorage() => await fixture.UploadFileAsync(...);
```

---

### AP-044: Ad-Hoc Entity Instantiation with Nondeterministic `DateTimeOffset.UtcNow`
*   **The Architectural Danger:** Using `DateTimeOffset.UtcNow` directly in test assertions causes flakiness due to clock skew and milliseconds rounding differences.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 5: Unit Test Golden Archetypes)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#5-unit-test-golden-archetypes)
```csharp
// ❌ WRONG: Nondeterministic clock causes intermittent CI assertion failures
var now = DateTimeOffset.UtcNow; // Skew risk!
template.CreatedAt.Should().Be(DateTimeOffset.UtcNow); // Flaky!

// ✅ CORRECT: Use deterministic baseline timestamps from TestConstants
var baseline = TestConstants.BaselineTime;
var template = Template.Create(projectId, name, slug, "Invoice", baseline);
template.CreatedAt.Should().Be(baseline);
```

---

### AP-045: Artificial Dumping Grounds for Domain Invariant Tests
*   **The Architectural Danger:** Creating artificial test classes like `InvariantValidationTests.cs` separates invariant tests from their aggregate roots and clutters the test suite.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 5: Unit Test Golden Archetypes)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#5-unit-test-golden-archetypes)
```text
// ❌ WRONG: Artificial dumping ground for domain invariants
tests/SmkDoc.Tests/Domain/Common/AllEntityInvariantsTests.cs

// ✅ CORRECT: Domain invariants tested directly in Aggregate Root unit tests
tests/SmkDoc.Tests/Domain/Entities/TemplateTests.cs
tests/SmkDoc.Tests/Domain/Entities/ProjectTests.cs
```

---

### AP-046: Mocking Dependencies in Validator Tests
*   **The Architectural Danger:** Injecting mocks into Command Validator tests adds setup overhead and slows down validation suites. Validators are pure deterministic functions.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 7: Dual-Engine Validation Pipeline Pattern)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-7-dual-engine-validation-pipeline-pattern)
```csharp
// ❌ WRONG: Mocking repositories inside Validator tests
var repoMock = new Mock<ITemplateRepository>();
var validator = new CreateTemplateCommandValidator(repoMock.Object);

// ✅ CORRECT: Validators tested as pure functions with [Theory] + [InlineData]
public class CreateTemplateCommandValidatorTests 
{
    private readonly CreateTemplateCommandValidator sut = new();
    [Theory] [InlineData("")] [InlineData(" ")]
    public void Validate_WhenNameIsEmpty_HasValidationError(string name) => ...;
}
```

---

### AP-047: Calling `new UseCase(...)` in Every Individual Test Method
*   **The Architectural Danger:** Instantiating the SUT (`new CreateTemplateUseCase(...)`) in every test method creates massive maintenance overhead whenever constructor dependencies change.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 5: Unit Test Golden Archetypes)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#5-unit-test-golden-archetypes)
```csharp
// ❌ WRONG: Re-instantiating SUT manually across 20 test methods
[Fact] public async Task Test1() { var sut = new CreateTemplateUseCase(repoMock.Object, uowMock.Object); ... }

// ✅ CORRECT: Single Source of Truth CreateSut() helper method
private CreateTemplateUseCase CreateSut() => new(templateRepoMock.Object, unitOfWorkMock.Object, TimeProvider.System);
```

---

### AP-048: Shallow Exception Assertions Without Checking Negative Side-Effects
*   **The Architectural Danger:** Asserting only that an exception was thrown without verifying that database transactions were aborted risks undetected data corruption bugs.
*   **Canonical Standard:** 👉 [CODING_CONVENTIONS.md (Section 5: Unit Test Golden Archetypes)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#5-unit-test-golden-archetypes)
```csharp
// ❌ WRONG: Shallow assertion ignores database mutation verification
var act = () => CreateSut().ExecuteAsync(command);
await act.Should().ThrowAsync<ConflictException>(); // Missing Commit verification!

// ✅ CORRECT: Deep Semantic Assertion + Verify CommitAsync(Times.Never)
var act = () => CreateSut().ExecuteAsync(command);
await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{command.Slug}'*");
unitOfWorkMock.Verify(uow => uow.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
```

---

</backend_scope>

<frontend_scope>
## 🟡 Category 7: Frontend Standards (TypeScript 5.7 / Next.js 15.2)

### AP-F001: Inline Styles or Hardcoded Color Codes
*   **The Architectural Danger:** Hardcoding hex codes (`#3b82f6`) or inline styles bypasses Tailwind CSS token layers, breaks dark mode, and fragments theme customization.
```typescript
// ❌ WRONG: Hardcoded hex codes and inline styles
<div style={{ backgroundColor: "#1e293b", color: "#ffffff" }}>Preview</div>

// ✅ CORRECT: Standard Tailwind CSS semantic utility classes
<div className="bg-surface text-foreground dark:bg-surface-dark">Preview</div>
```

---

### AP-F002: Client-Side `useEffect` for Initial Data Fetching
*   **The Architectural Danger:** Fetching initial page data inside `useEffect` creates network waterfalls, causes cumulative layout shift (CLS), and breaks server-side rendering (SSR).
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 10: Server-First RSC Data Fetching & Leaf Client Component)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-10-server-first-rsc-data-fetching--leaf-client-component)
```typescript
// ❌ WRONG: Client-side useEffect fetching creates waterfall
"use client";
export default function TemplatesPage() { useEffect(() => { fetch(...).then(...); }, []); ... }

// ✅ CORRECT: React Server Component fetches directly on the server
export default async function TemplatesPage() {
  const templates = await getTemplates();
  return <TemplateList items={templates} />;
}
```

---

### AP-F003: Using `any` in TypeScript
*   **The Architectural Danger:** Disabling the TypeScript compiler via `any` causes runtime type exceptions and breaks compile-time schema validation guarantees.
```typescript
// ❌ WRONG: Bypassing type safety with 'any'
function handlePayload(data: any) { console.log(data.nonExistentField.toUpperCase()); }

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
{ "extends": ["next/core-web-vitals"] }

// ✅ CORRECT: Modern Flat Config eslint.config.mjs
const eslintConfig = [...compat.extends("next/core-web-vitals", "next/typescript")];
export default eslintConfig;
```

---

### AP-F005: Root-Level `"use client"` Pollution
*   **The Architectural Danger:** Marking `page.tsx` or `layout.tsx` with `"use client"` converts the entire component subtree into Client Components, disabling streaming SSR.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 10: Server-First RSC Data Fetching & Leaf Client Component)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-10-server-first-rsc-data-fetching--leaf-client-component)
```typescript
// ❌ WRONG: Entire page turned into Client Component
// app/templates/[id]/page.tsx
"use client";
export default function Page({ params }) { ... }

// ✅ CORRECT: Server Component page wrapping interactive client leaf component
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const initialData = await getTemplateById(id);
  return <TemplateEditorClient initialData={initialData} />;
}
```

---

### AP-F006: In-Flight Preview Race Conditions (Missing `AbortController`)
*   **The Architectural Danger:** Triggering HTTP preview generation requests on rapid keystrokes without canceling pending requests causes out-of-order response arrivals that overwrite newer state.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 11: Race-Condition-Free Live Preview)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-11-race-condition-free-live-preview)
```typescript
// ❌ WRONG: Rapid keystrokes trigger overlapping requests; slow request overwrites new preview
async function onCodeChange(newContent: string) { setPreviewUrl(URL.createObjectURL(await fetchPreview(newContent))); }

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
  }
}
```

---

### AP-F007: Manual Type Duplication (Bypassing Zod SSoT)
*   **The Architectural Danger:** Manually writing TypeScript interfaces when a Zod schema exists creates schema drift when validation rules are updated.
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 12: Type-Safe RFC 9457 Client Diagnostic Adapter)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-12-type-safe-rfc-9457-client-diagnostic-adapter)
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
*   **Canonical Standard:** 👉 [PATTERNS.md (Archetype 11: Race-Condition-Free Live Preview)](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-11-race-condition-free-live-preview)
```typescript
// ❌ WRONG: Showing error alerts on intentional aborts
try { await fetchPreview(content, signal); } catch (err) { showErrorToast("Failed to preview"); }

// ✅ CORRECT: Inspect error name and silently exit on AbortError
try {
  await fetchPreview(content, signal);
} catch (err: unknown) {
  if (err instanceof DOMException && err.name === "AbortError") return;
  showErrorToast("An unexpected preview generation error occurred.");
}
```

---

</frontend_scope>
