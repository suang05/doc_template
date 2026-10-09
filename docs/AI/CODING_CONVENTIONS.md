# CODING_CONVENTIONS.md — SMK Document Server

> **Purpose:** This document is the Ultimate Single Source of Truth (SSoT) for engineering standards, language idioms, code craftsmanship, and naming conventions across the SMK Document Server (`backend-v2/` and `frontend-v2/`).
> **[AI_DIRECTIVE]:** AI Assistants (LLMs) and human engineers MUST treat these guidelines as absolute laws. You must read the specific section relevant to your task to guarantee an uncompromised, clean, readable, SOLID, and deterministic codebase. Never deviate from these structures or invent alternative conventions.

<ai_directive>
CRITICAL ATTENTION ROUTING:
- For Backend (C# 13 / .NET 10), STRICTLY focus on `<backend_scope>`, `<craftsmanship_rules>`, and `<global_standards>`.
- For Frontend (TypeScript / Next.js 15), STRICTLY focus on `<frontend_scope>`, `<craftsmanship_rules>`, and `<global_standards>`.
- ALWAYS obey `<core_philosophy>` and `<tech_stack>`.
- For complete end-to-end architectural implementation templates (Golden Archetypes), refer directly to `docs/AI/PATTERNS.md`.
</ai_directive>

---

## 📑 Table of Contents
1. **[Core Philosophy: Clean Architecture & SOLID Design](#1-🏛️-core-philosophy-clean-architecture--solid-design)**
2. **[System Tech Stack (Ground Truth)](#2-🏗️-system-tech-stack-ground-truth)**
3. **[Readability, Simplicity & Code Craftsmanship (The 9 Golden Rules)](#3-💎-readability-simplicity--code-craftsmanship-the-9-golden-rules)**
   - 3.1 The Stepdown Rule (The Newspaper Metaphor)
   - 3.2 Vertical Proximity & Visual Formatting
   - 3.3 Linear Storytelling & Guard Clauses (Max Cyclomatic Depth = 2)
   - 3.4 Meaningful & Intention-Revealing Names (No Mental Mapping)
   - 3.5 Single Term per Concept (Cross-Layer Ubiquitous Consistency)
   - 3.6 Zero Echo/Robot Comments (Code explains WHAT, Comment explains WHY)
   - 3.7 Explicit Intermediate Variables & Breaking Long Chains
   - 3.8 Trust Nullable Reference Types (No Paranoid Defensive Guarding in Core)
   - 3.9 Expressive Domain Verbs over Anemic Property Dumping
4. **[Backend Standards: C# 13 / .NET 10 (`backend-v2/`)](#4-🔵-backend-standards-c-13--net-10-backend-v2)**
   - 4.1 Primary Constructors (camelCase Invariant, Zero Field Re-declaration)
   - 4.2 Immutability & Record Types ('record' vs 'class' Decision Matrix)
   - 4.3 Modern C# 13 Language Idioms (Collection Expressions, Pattern Matching, Locks)
   - 4.4 Deterministic Time (TimeProvider) & Async Cancellation (ct)
   - 4.5 Clean Architecture Layer Syntactic Rules
   - 4.6 Testing Craftsmanship (Strict AAA Anatomy & 3-Part Naming)
5. **[Frontend Standards: TypeScript 5.7 / Next.js 15.2 (`frontend-v2/`)](#5-🟡-frontend-standards-typescript-57--nextjs-152-frontend-v2)**
   - 5.1 Strict TypeScript & Zod SSoT
   - 5.2 Next.js 15 App Router Paradigms (Server Components by Default)
   - 5.3 Component Architecture & Category Isolation
6. **[System-Wide Naming Standards & The Anti-Hallucination Matrix](#6-🏷️-system-wide-naming-standards--the-anti-hallucination-matrix)**
   - 6.1 The 6 Pillars Naming Matrix
   - 6.2 The Canonical Verb Dictionary (One Word per Concept across Layers)
   - 6.3 The Anti-Hallucination Variable Dictionary (STRICT vs Banned Names)
   - 6.4 Canonical End-to-End Naming Flow Example

---

<core_philosophy>
## 1. 🏛️ Core Philosophy: Clean Architecture & SOLID Design

All code written for the SMK Document Server MUST strictly adhere to global software engineering best practices. We prioritize **Readability & Simplicity** above premature optimization.

1. **Clean Architecture & Strict Decoupling:** Inner layers (Domain, Application) know nothing about outer layers (Infrastructure, Presentation). Dependencies flow inwards only.
2. **SOLID Principles:**
   - **SRP (Single Responsibility):** Each class and method has exactly one reason to change.
   - **OCP (Open/Closed):** Open for extension via polymorphic dispatch, pipeline behaviors, or Strategy patterns; closed for modification.
   - **LSP (Liskov Substitution):** Derived types or interface implementations must be fully substitutable for their abstractions without unexpected side-effects.
   - **ISP (Interface Segregation):** Small, focused client-specific interfaces (e.g., `IUseCase<TCommand, TResult>`, `ITemplateRepository`) rather than bloated fat interfaces.
   - **DIP (Dependency Inversion):** High-level business modules depend on abstractions, never on low-level infrastructure details.
3. **Readability & Simplicity First:** Code must read like clean, unambiguous prose. Code is read ten times more often than it is written. If code requires mental gymnastics to understand, it is defective.
4. **KISS (Keep It Simple, Stupid):** The simplest solution that completely satisfies requirements and preserves architectural boundaries is always superior to an overly clever abstraction.
5. **DRY & Single Source of Truth (SSoT):** Every piece of knowledge, business logic, validation rule, or database mapping must have an authoritative, unambiguous representation within the system.
6. **Zero Dead Code:** Maintain pristine hygiene. Eliminate unused imports, commented-out logic, and obsolete scaffolding prior to verification.

---

</core_philosophy>

<tech_stack>
## 2. 🏗️ System Tech Stack (Ground Truth)

> **[AI_DIRECTIVE]** The following exact technologies and versions are actively configured in the workspace (`Directory.Build.props` and `package.json`). All generated code, dependencies, and syntax MUST align strictly with these versions.

| Layer / Concern | Technology | Version | Key Directives & Capabilities |
|---|---|---|---|
| **Backend Framework** | ASP.NET Core | .NET 10 (`net10.0`) | Modern Web API, Minimal Hosting, RFC 9457 ProblemDetails |
| **Language** | C# | 13.0 (`<LangVersion>13.0</LangVersion>`) | Primary Constructors, Collection Expressions `[]`, Positional Records |
| **Persistence / ORM** | Entity Framework Core + Npgsql | 10.x | Code-First, Fluent API Mapping, PostgreSQL Provider |
| **Database** | PostgreSQL | 15-alpine | Relational storage, UTF-8 Thai Collation, `smkdoc` database |
| **Document Rendering** | Gotenberg | 8.x | High-throughput Chromium (HTML to PDF) and LibreOffice engine |
| **Object Storage** | MinIO | S3-Compatible | Multi-bucket asset and output storage (`templates/`, `outputs/`) |
| **Frontend Framework** | Next.js App Router | 15.2.0 | React 19.0.0, Server Components by default, Leaf Client Components |
| **Frontend Language** | TypeScript | 5.7.3 | Strict Mode (`strict: true`), zero `any` allowance |
| **Validation (Frontend)**| Zod | 3.24.2 | Runtime schema validation and SSoT TypeScript type derivation (`z.infer`) |
| **UI Styling** | Tailwind CSS | 3.4.17 | CSS Custom Properties design tokens, `clsx` + `tailwind-merge` (`cn()`) |
| **Frontend Testing** | Vitest | 5.0.1 | Fast unit and component test runner |
| **Backend Testing** | xUnit + FluentAssertions + Moq | Latest stable | AAA anatomy, Testcontainers for real PostgreSQL/MinIO integration tests |

---

</tech_stack>

<craftsmanship_rules>
## 3. 💎 Readability, Simplicity & Code Craftsmanship (The 9 Golden Rules)

These 9 concrete rules convert abstract concepts like "Clean Code" into strictly verifiable constraints. Every pull request and LLM-generated code snippet must pass these criteria.

---

### 3.1 The Stepdown Rule (The Newspaper Metaphor)
A source file must read like a well-edited newspaper article. The highest-level, most important concepts appear first, followed by progressively lower-level details as the reader scrolls downward.

*   **Rule 1 (Public First):** The primary public entry point (e.g., `ExecuteAsync` in a UseCase, or HTTP action methods in a Controller) MUST be placed at the very top of the class body, directly following constructors.
*   **Rule 2 (Top-Down Sequence):** Private helper methods called by a public method MUST be placed **immediately beneath the caller method** in the exact order they are invoked.
*   **Rule 3 (No Random Dumps):** NEVER dump private methods into a random cluster at the very bottom of a 300-line class. The reader must never be forced to jump frantically across a file to trace the execution flow.

```csharp
// ❌ Bad: Disorganized method order forces chaotic scrolling
public sealed class GenerateDocumentUseCase(IRenderEngine engine)
{
    private byte[] ApplyWatermark(byte[] pdf) => ... // Helper at top
    
    public async Task<DocumentResult> ExecuteAsync(...) // Main entry point buried in the middle
    {
        var html = FormatHtml(...);
        var pdf = await engine.RenderAsync(html);
        return ApplyWatermark(pdf);
    }

    private string FormatHtml(...) => ... // Another helper at bottom
}

// ✅ Good: The Stepdown Rule (Reads naturally from top to bottom)
public sealed class GenerateDocumentUseCase(IRenderEngine engine)
{
    // 1. Primary entry point at the top
    public async Task<DocumentResult> ExecuteAsync(GenerateDocumentCommand command, CancellationToken ct)
    {
        var formattedHtml = PrepareHtmlPayload(command);
        var rawPdf = await engine.RenderAsync(formattedHtml, ct);
        return ApplyWatermark(rawPdf);
    }

    // 2. First helper placed directly beneath its invocation point
    private string PrepareHtmlPayload(GenerateDocumentCommand command)
    {
        return $"<html><body>{command.Content}</body></html>";
    }

    // 3. Second helper follows immediately
    private DocumentResult ApplyWatermark(byte[] rawPdf)
    {
        var finalizedBytes = WatermarkProcessor.Stamp(rawPdf);
        return new DocumentResult(finalizedBytes);
    }
}
```

---

### 3.2 Vertical Proximity & Visual Formatting
Concepts and variables that are closely related conceptually must be kept vertically close visually.

*   **Rule 1 (Declare at Point of Use):** Declare local variables immediately before their first usage. NEVER declare all variables at the top of a method if they are consumed 15 lines later.
*   **Rule 2 (Blank Lines as Paragraphs):** Use exactly one blank line to separate distinct logical operations (e.g., between fetching data, validating business state, mutating domain models, and persisting results). Never bunch 30 lines into an unformatted wall of code.

```csharp
// ❌ Bad: Variable declared far away from usage; no visual breathing room
public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
{
    var now = timeProvider.GetUtcNow(); // Declared at line 1
    var templateName = TemplateName.Create(command.Name); // Declared at line 2
    if (await templateRepo.SlugExistsAsync(command.Slug, ct)) throw new ConflictException("Slug exists");
    var existingProject = await projectRepo.GetByIdAsync(command.ProjectId, ct);
    if (existingProject is null) throw new NotFoundException("Project not found");
    var template = Template.Create(command.ProjectId, templateName, now); // Used at line 6
    await templateRepo.AddAsync(template, ct);
    await unitOfWork.CommitAsync(ct);
    return new TemplateResultDto(template.Id, template.Name.Value);
}

// ✅ Good: Vertical Proximity and paragraph-style blank lines
public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
{
    // Step 1: Pre-condition checks
    if (await templateRepo.SlugExistsAsync(command.Slug, ct))
    {
        throw new ConflictException("Slug already in use");
    }

    var project = await projectRepo.GetByIdAsync(command.ProjectId, ct);
    if (project is null)
    {
        throw new NotFoundException($"Project '{command.ProjectId}' does not exist");
    }

    // Step 2: Domain creation (variables declared directly at point of use)
    var now = timeProvider.GetUtcNow();
    var templateName = TemplateName.Create(command.Name);
    var template = Template.Create(command.ProjectId, templateName, now);

    // Step 3: Persistence
    await templateRepo.AddAsync(template, ct);
    await unitOfWork.CommitAsync(ct);

    return new TemplateResultDto(template.Id, template.Name.Value);
}
```

---

### 3.3 Linear Storytelling & Guard Clauses (Max Cyclomatic Depth = 2)
The "happy path" of execution must flow smoothly along the left margin of the editor.

*   **Rule 1 (Fail Fast via Guard Clauses):** Inspect arguments, preconditions, and failure scenarios at the top of the function. Exit immediately via `return` or throw a strongly-typed domain exception.
*   **Rule 2 (Maximum Indentation Depth = 2):** Cyclomatic nesting depth MUST NEVER exceed 2 levels (e.g., an `if` block inside a method is depth 1; a loop inside that `if` is depth 2). If a third level of nesting is needed, you MUST extract the nested logic into a descriptive private helper method or domain verb.

```csharp
// ❌ Bad: Arrow Anti-Pattern (Deep nesting, happy path buried inside brackets)
public async Task ProcessPayload(Payload payload)
{
    if (payload is not null)
    {
        if (payload.IsValid)
        {
            foreach (var item in payload.Items)
            {
                if (item.IsActive) // Depth 4! Unreadable and hard to test.
                {
                    await SaveItem(item);
                }
            }
        }
    }
}

// ✅ Good: Guard Clauses keep the happy path linear and left-aligned (Max depth = 2)
public async Task ProcessPayload(Payload payload)
{
    if (payload is null || !payload.IsValid)
    {
        throw new DomainValidationException("Payload is invalid");
    }

    foreach (var item in payload.Items)
    {
        if (!item.IsActive)
        {
            continue; // Guard inside loop maintains depth at 2
        }

        await SaveItem(item);
    }
}
```

---

### 3.4 Meaningful & Intention-Revealing Names (No Mental Mapping)
Names must answer three questions without inspecting comments: *Why does this exist? What does it do? How is it consumed?*

*   **Rule 1 (Zero Cryptic Abbreviations):** NEVER use single-letter or meaningless variable names like `t`, `p`, `temp`, `data`, `obj`, `item`, `val`.
*   **Rule 2 (Eliminate Mental Mapping):** The reader should never be forced to mentally translate `p` to `project` or `dt` to `documentTemplate`.
*   **Rule 3 (Searchable & Pronounceable):** Use names that can be searched globally across a codebase without yielding thousands of unrelated matches.

```csharp
// ❌ Bad: Requires mental mapping; single-letter variables
var t = await repo.Get(id);
var res = await s.Transform(t, p);

// ✅ Good: Intention-revealing, searchable, and explicit
var template = await templateRepository.GetByIdAsync(templateId, ct);
var transformedPayload = await templateTransformer.TransformAsync(template, projectContext, ct);
```

---

### 3.5 Single Term per Concept (Cross-Layer Ubiquitous Consistency)
Pick one word for one abstract concept and adhere to it strictly across all projects, layers, interfaces, and methods.

*   **Retrieving a single entity by ID:** ALWAYS use **`GetByIdAsync`** (NEVER mix with `Fetch`, `Retrieve`, `FindById`, `Load`).
*   **Conditional lookup returning nullable:** ALWAYS use **`FindAsync`** (returns `T?` if missing, does not throw).
*   **Retrieving multiple entities:** ALWAYS use **`ListAsync`** (NEVER mix with `GetAll`, `FetchMany`, `SelectAll`).
*   **Checking existence:** ALWAYS use **`ExistsAsync`** (NEVER mix with `CheckExists`, `HasEntity`, `IsPresent`).
*   **Parameter Name Invariance:** If an identifier represents a Template's ID, it MUST be named **`templateId`** at every boundary:
    - Route parameter: `/api/v1/templates/{templateId}`
    - Controller parameter: `Guid templateId`
    - Application Command: `Guid TemplateId`
    - Domain Method: `Template.Update(Guid templateId, ...)`
    - Repository Contract: `GetByIdAsync(Guid templateId, ...)`
    - *NEVER shorten to `id` in some layers and expand to `templateId` or `entityId` in others.*

---

### 3.6 Zero Echo/Robot Comments
Code must explain **WHAT** is happening; comments are strictly reserved for explaining **WHY**.

*   **Rule 1 (Banned Echo Comments):** NEVER write comments that merely rephrase the method call or variable assignment. They create visual noise and rot over time.
*   **Rule 2 (Legitimate Comments):** Comments are ONLY permitted to explain:
    - Non-obvious business domain rules or legal mandates (e.g., Thai Tax Revenue Department requirements).
    - Workarounds for known external SDK quirks or hardware limits.
    - Citations of external standards or specifications (e.g., `// Conforms to RFC 9457 Section 3.1`).

```csharp
// ❌ Bad: Useless echo comments pollute the code
// Check if template is null
if (template is null) 
{
    // Throw not found exception
    throw new NotFoundException("Not found");
}

// Add template to database
await templateRepo.AddAsync(template, ct);

// ✅ Good: Self-documenting code with comments explaining only the non-obvious "WHY"
if (template is null)
{
    throw new NotFoundException($"Template '{templateId}' was not found");
}

// Gotenberg Chromium engine requires a 150ms rendering grace period 
// to complete CSS webfont glyph rasterization before emitting PDF stream.
await Task.Delay(RenderEngineConstants.ChromiumFontRasterizationDelayMs, ct);

await templateRepo.AddAsync(template, ct);
```

---

### 3.7 Explicit Intermediate Variables & Breaking Long Chains
While fluent builders and LINQ are expressive, chaining 6 operations in a single uninterrupted statement degrades readability and obscures stack traces during exceptions.

*   **Rule 1 (Maximum Fluent Chain Length = 3):** If a transformation requires more than 3 operations, break it into intermediate variables with descriptive names.
*   **Rule 2 (Debuggability):** Breaking expressions into local variables allows developers and debuggers to inspect intermediate state at breakpoints and provides exact line numbers in stack traces.

```csharp
// ❌ Bad: Long chained expression hides intermediate errors and is hard to debug
var names = projects.SelectMany(p => p.Templates).Where(t => t.IsActive && t.Type == TemplateType.Html).OrderBy(t => t.Name).Select(t => t.Name.Value).ToList();

// ✅ Good: Explicit intermediate steps with intention-revealing names
var allTemplates = projects
    .SelectMany(project => project.Templates);

var activeHtmlTemplates = allTemplates
    .Where(template => template.IsActive && template.Type == TemplateType.Html);

var templateNames = activeHtmlTemplates
    .OrderBy(template => template.Name.Value)
    .Select(template => template.Name.Value)
    .ToList();
```

---

### 3.8 Trust Nullable Reference Types
With `<Nullable>enable</Nullable>` enforced across the solution, trust your boundary validation and language guarantees.

*   **Rule 1 (Validate at the Perimeter Only):** Validate inputs exhaustively at the Presentation and Application boundaries using FluentValidation and Zod.
*   **Rule 2 (No Paranoid Defensive Checks in Core):** Once an object passes through boundary validation and enters the protected Application/Domain core, do NOT scatter defensive `if (arg is null)` checks across every single internal method. Trust non-nullable type annotations.

---

### 3.9 Expressive Domain Verbs over Anemic Property Dumping
Domain Entities are living representations of business rules, not passive data bags.

*   **Rule 1 (No Public Property Setters):** Properties on Domain Entities must have `private set;` or `protected set;`.
*   **Rule 2 (Expressive Domain Verbs):** Mutate state exclusively through domain methods named with ubiquitous business verbs (e.g., `Publish`, `Archive`, `Deactivate`). NEVER assign properties individually from outside the entity.

```csharp
// ❌ Bad: Anemic property dumping outside the entity violates encapsulation
template.IsActive = false;
template.DeactivatedReason = "Superseded";
template.UpdatedAt = now;

// ✅ Good: Expressive domain verb encapsulates state invariants atomically
template.Deactivate(reason: "Superseded", now: now);
```

---

</craftsmanship_rules>

<backend_scope>
## 4. 🔵 Backend Standards: C# 13 / .NET 10 (`backend-v2/`)

**Core Target:** C# 13 (`<LangVersion>13.0</LangVersion>`), ASP.NET Core 10 (`net10.0`), Entity Framework Core 10.

---

### 4.1 Primary Constructors (camelCase Invariant, Zero Field Re-declaration)
Primary Constructors are mandatory across all injected services, UseCases, Repositories, Controllers, and Middleware.

*   **Mandatory Rule 1 (Pure `camelCase` Parameters):** Primary constructor parameters MUST use pure `camelCase` (e.g., `templateRepo`, `unitOfWork`, `timeProvider`). The underscore prefix (`_`) is **STRICTLY FORBIDDEN**.
*   **Mandatory Rule 2 (Zero Field Re-declaration):** NEVER declare backing fields (such as `private readonly ITemplateRepository repo = templateRepo;`). Consume the constructor parameter directly within class methods.
*   **Mandatory Rule 3 (Formatting Multi-Line Constructors):** When a class has 3 or more dependencies, format the primary constructor with each parameter on its own indented line.

```csharp
// ❌ Bad: Re-declaring fields defeats Primary Constructors; illegal underscore prefix
public sealed class TemplateController(ITemplateRepository _repo, IUnitOfWork _uow) : ControllerBase
{
    private readonly ITemplateRepository repo = _repo; // VIOLATION
    private readonly IUnitOfWork uow = _uow; // VIOLATION
}

// ✅ Good: Clean, idiomatic C# 13 Primary Constructor
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
    {
        // Use injected parameters directly
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(command.ProjectId, TemplateName.Create(command.Name), now);

        await templateRepo.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);

        return new TemplateResultDto(template.Id, template.Name.Value, template.CreatedAt);
    }
}
```

---

### 4.2 Immutability & Record Types ('record' vs 'class' Decision Matrix)
Understanding when to use `record` versus `class` is critical to preventing subtle bugs in Clean Architecture.

| Artifact Type | Recommended C# Construct | Rationale & Architectural Rule |
|---|---|---|
| **CQRS Commands** | `public sealed record` | Immutable data carrier. Structural equality guarantees safe hashing and caching. |
| **CQRS Queries** | `public sealed record` | Read-only input criteria. Value equality allows query deduplication. |
| **Output DTOs** | `public sealed record` | Data Transfer Objects should be immutable snapshots of domain state. |
| **Value Objects** | `public sealed record` or `readonly record struct` | Domain Value Objects are identified solely by their constituent values (e.g., `TemplateSlug`). |
| **Domain Entities** | `public sealed class` | **NEVER use records for EF Core Entities.** EF Core's Change Tracker relies on reference identity and in-place state mutation. |
| **DI Services / UseCases**| `public sealed class` | Stateful operations requiring behavior, encapsulation, and dependency injection. |

```csharp
// ✅ Positional records guarantee conciseness and immutability for contracts
public sealed record CreateTemplateCommand(Guid ProjectId, string Name, string Slug);
public sealed record TemplateResultDto(Guid Id, string Name, DateTimeOffset CreatedAt);

// ✅ Domain Value Object as a record
public sealed record TemplateSlug(string Value);
```

---

### 4.3 Modern C# 13 Language Idioms
Harness the full power of C# 13 to eliminate verbose legacy syntax.

*   **Collection Expressions `[]`:** Use `[]` for arrays, lists, spans, and empty collections. NEVER use `new List<T>()` or `new T[] { ... }`.
    ```csharp
    // ❌ Bad: Verbose instantiation
    List<string> tags = new List<string> { "invoice", "receipt" };
    string[] empty = new string[0];

    // ✅ Good: Modern C# 13 collection expressions
    List<string> tags = ["invoice", "receipt"];
    string[] empty = [];
    ```
*   **Pattern Matching Expressions:** Use `switch` expressions and pattern matching over legacy `if/else` ladders.
    ```csharp
    // ✅ Good: Clean pattern matching
    var mimeType = extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };
    ```
*   **C# 13 `System.Threading.Lock`:** Use the dedicated .NET 10 / C# 13 `Lock` object instead of arbitrary `object` instances for synchronization.
    ```csharp
    // ✅ Good: C# 13 lock object
    private readonly Lock syncLock = new();
    public void SafeOperation()
    {
        lock (syncLock) { ... }
    }
    ```

---

### 4.4 Deterministic Time (TimeProvider) & Async Cancellation (ct)
*   **Deterministic Time:** NEVER call `DateTime.UtcNow` or `DateTime.Now` directly in Application or Domain logic. Always inject .NET's standard `TimeProvider` and call `timeProvider.GetUtcNow()`. This allows unit tests to freeze or advance time deterministically.
*   **Strict Asynchronous Integrity:**
    - Every I/O-bound method MUST accept a `CancellationToken ct = default` as its final parameter.
    - Propagate `ct` to every async call (EF Core, HTTP clients, MinIO).
    - NEVER block threads using `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.

---

### 4.5 Clean Architecture Layer Syntactic Rules

#### 1. Domain Layer (`SmkDoc.Domain`)
*   **Persistence Ignorance:** Zero references to EF Core, Npgsql, or external packages. Pure POCOs.
*   **Encapsulation:** Constructors are `internal` or `private`. Expose exactly one canonical factory method (e.g., `Create`) as the SSoT for entity instantiation.
*   *Full Architecture Archetype:* 👉 **See Pattern 1 in `docs/AI/PATTERNS.md`**.

#### 2. Application Layer (`SmkDoc.Application`)
*   **Orchestration Only:** UseCases implement `IUseCase<TCommand, TResult>`.
*   **Dual Validation Boundary (DRY):**
    - Input structure/format validation belongs to `FluentValidation` (runs before UseCase).
    - Business rule/state invariant validation belongs inside the Domain Entity.
    - NEVER validate the same rule twice across both layers.
*   *Full Architecture Archetype:* 👉 **See Pattern 2 in `docs/AI/PATTERNS.md`**.

#### 3. Infrastructure Layer (`SmkDoc.Infrastructure`)
*   **Dependency Inversion:** Implement interfaces defined by Application or Domain.
*   **Fluent API Configuration:** Map EF Core entities exclusively using `IEntityTypeConfiguration<T>`. Never annotate Domain Entities with EF attributes (`[Table]`, `[Key]`).
*   *Full Architecture Archetype:* 👉 **See Pattern 4 in `docs/AI/PATTERNS.md`**.

#### 4. Presentation Layer (`SmkDoc.Api`)
*   **Thin Controllers:** Receive HTTP requests, map directly to Application Commands/Queries, and return standard `ApiResponse<T>`. No business logic.
*   **RFC 9457 ProblemDetails:** All HTTP error responses MUST conform to RFC 9457 via `IExceptionHandler` (`GlobalExceptionHandler`). Anonymous error objects (`new { error = ... }`) are STRICTLY BANNED.
*   *Full Architecture Archetype:* 👉 **See Pattern 3 in `docs/AI/PATTERNS.md`**.

---

### 4.6 Testing Craftsmanship (Strict AAA Anatomy & 3-Part Naming)

Every unit test must follow a rigorous, standardized anatomy to ensure maintainability:

*   **The 3-Part Naming Rule:** `MethodUnderTest_StateUnderTest_ExpectedBehavior` (e.g., `ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException`).
*   **Explicit AAA Separation:** Visually separate the test into three distinct sections using comments (`// Arrange`, `// Act`, `// Assert`) and blank lines.
*   **FluentAssertions Only:** Use `FluentAssertions` (e.g., `result.Should().NotBeNull()`). Avoid legacy xUnit `Assert.Equal()`.
*   **Zero I/O in Unit Tests:** Mock all Infrastructure dependencies (`IRepository`, `IStorageService`). Instantiate Domain Entities and DTOs directly.

```csharp
[Fact]
public async Task ExecuteAsync_WhenValidCommand_ShouldPersistAndReturnResultDto()
{
    // Arrange
    var projectId = Guid.NewGuid();
    var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice");
    
    fixture.TemplateRepo
        .Setup(repo => repo.SlugExistsAsync("tax-invoice", It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var sut = CreateSut();

    // Act
    var result = await sut.ExecuteAsync(command, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("Tax Invoice");

    fixture.TemplateRepo.Verify(
        repo => repo.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), 
        Times.Once);
        
    fixture.UnitOfWork.Verify(
        uow => uow.CommitAsync(It.IsAny<CancellationToken>()), 
        Times.Once);
}
```

---

</backend_scope>

<frontend_scope>
## 5. 🟡 Frontend Standards: TypeScript 5.7 / Next.js 15.2 (`frontend-v2/`)

---

### 5.1 Strict TypeScript & Zod SSoT
*   **Zero `any` Policy:** The `any` keyword is banned. Use `unknown` with type narrowing functions or Zod validation.
*   **Zod as Single Source of Truth:**
    - Place all validation schemas in `src/schemas/`.
    - Automatically infer TypeScript types using `export type CreateTemplateInput = z.infer<typeof createTemplateSchema>;`.
    - NEVER duplicate interfaces in `src/types/` if a Zod schema represents the same structure.

---

### 5.2 Next.js 15 App Router Paradigms (Server Components by Default)
*   **RSC by Default:** `page.tsx` and `layout.tsx` must remain Server Components (no `"use client"`). Fetch data directly on the server using async/await.
*   **Leaf Client Components:** Place `"use client"` exclusively at the interactive leaves of the DOM tree (e.g., a specific interactive modal or form). Never mark an entire page as a Client Component.
*   **Zero `useEffect` for Data Fetching:** NEVER use `useEffect` to fetch data on component mount. Use Server Components for initial data and React Query/SWR for client-side polling or mutation.

```tsx
// ❌ Bad: Entire page client-side with useEffect fetching
"use client";
import { useEffect, useState } from "react";

export default function TemplatePage({ params }: { params: { id: string } }) {
    const [template, setTemplate] = useState<Template | null>(null);
    useEffect(() => {
        fetch(`/api/v1/templates/${params.id}`).then(r => r.json()).then(setTemplate);
    }, [params.id]);

    if (!template) return <div>Loading...</div>;
    return <div>{template.name}</div>;
}

// ✅ Good: Next.js 15 Server Component with isolated interactive client leaf
import { notFound } from "next/navigation";
import { fetchTemplateById } from "@/lib/api/templates";
import { TemplateEditorClient } from "@/components/features/templates/TemplateEditorClient";

export default async function TemplatePage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const template = await fetchTemplateById(id);

    if (!template) {
        notFound();
    }

    return (
        <main className="container mx-auto py-6">
            <h1 className="text-2xl font-bold">{template.name}</h1>
            <TemplateEditorClient initialData={template} />
        </main>
    );
}
```

---

### 5.3 Component Architecture & Category Isolation
Structure frontend components into three strict folders to prevent circular dependencies:
```text
src/components/
├── ui/         # Primitives (Button, Modal, Input). Highly reusable, zero domain logic.
├── layout/     # Shells (Sidebar, Navbar, PageHeader). Dictates page layout.
└── features/   # Domain-specific components (e.g., /templates/TemplateCard.tsx).
                # May interact with API and maintain local feature state.
```

Always use `cn()` (`clsx` + `tailwind-merge`) when conditionally merging Tailwind classes:
```tsx
import { cn } from "@/lib/utils";

export function Button({ className, variant, ...props }: ButtonProps) {
    return (
        <button 
            className={cn("px-4 py-2 rounded-md font-medium transition-colors", className)} 
            {...props} 
        />
    );
}
```

---

</frontend_scope>

<global_standards>
## 6. 🏷️ System-Wide Naming Standards & The Anti-Hallucination Matrix

To ensure seamless coordination across human developers, code reviewers, and AI agents, all symbols, variables, and parameters MUST adhere to the following deterministic matrices.

---

### 6.1 The 6 Pillars Naming Matrix

| Element / Artifact | Layer / Scope | Convention | Canonical Example |
|---|---|---|---|
| **Domain Entities** | Domain | Singular `PascalCase` | `Template`, `DocumentLog` |
| **Value Objects** | Domain | Descriptive `PascalCase` | `TemplateSlug`, `Sha256Checksum` |
| **Domain Factory** | Domain | Canonical Verb | `Create()`, `BuildDefault()` |
| **Domain Mutations** | Domain | Ubiquitous Business Verb | `Publish()`, `Archive()`, `Deactivate()` |
| **Primary Ctor Parameters**| All C# Classes | Pure `camelCase` (Zero `_`) | `templateRepo`, `unitOfWork`, `timeProvider` |
| **CQRS Commands** | Application | `[Action][Entity]Command` | `CreateTemplateCommand`, `PublishTemplateCommand` |
| **CQRS Queries** | Application | `[Get/List][Entity]Query` | `GetTemplateByIdQuery`, `ListTemplatesQuery` |
| **Output DTOs** | Application | `[Context]ResultDto` | `TemplateResultDto`, `DocumentGenerationResultDto` |
| **HTTP Request Payloads** | Presentation | `[Action][Entity]Request` | `CreateTemplateRequest`, `RenderPreviewRequest` |
| **Controllers** | Presentation | `[Entity]Controller` | `TemplateController`, `DocumentController` |
| **Test Classes** | Test Projects | `[TargetClass]Tests` | `CreateTemplateUseCaseTests`, `TemplateTests` |
| **Zod Schemas** | Frontend | `[action][Entity]Schema` | `createTemplateSchema`, `updateConfigSchema` |
| **React Components** | Frontend | `PascalCase.tsx` | `TemplateCard.tsx`, `DocumentPreview.tsx` |
| **Custom Hooks** | Frontend | `use[Feature].ts` | `useTemplateEditor.ts`, `useDocumentStream.ts` |
| **REST Route URLs** | Presentation | `kebab-case` plural | `/api/v1/templates`, `/api/v1/projects/{projectId}` |
| **Database Tables** | Infrastructure | `plural_snake_case` | `templates`, `document_generation_logs` |
| **Database Columns** | Infrastructure | `snake_case` / `is_*` | `project_id`, `created_at`, `is_active` |

---

### 6.2 The Canonical Verb Dictionary (One Word per Concept across Layers)
Engineers and LLMs frequently invent synonyms that fracture system consistency. You MUST use the exact canonical verbs defined below:

| Operation Intent | STRICT Canonical Verb | Forbidden Synonyms | Scope |
|---|---|---|---|
| Retrieve single entity by ID | **`GetByIdAsync`** | `Fetch`, `Retrieve`, `FindById`, `Load`, `GetSingle` | Repositories, Queries |
| Query entity by condition (nullable) | **`FindAsync`** | `Search`, `Lookup`, `Detect`, `Check` | Repositories, UseCases |
| Retrieve collection of entities | **`ListAsync`** | `GetAll`, `FetchMany`, `GetMultiple`, `SelectAll` | Repositories, Queries |
| Verify existence of record | **`ExistsAsync`** | `CheckExists`, `IsExisting`, `HasEntity`, `Contains` | Repositories, UseCases |
| Persist transaction changes | **`CommitAsync`** | `Save`, `SaveChanges`, `Flush`, `Complete` | UnitOfWork |
| Instantiate domain entity | **`Create`** | `Build`, `New`, `Init`, `Make`, `Construct` | Domain Factory |
| Soft delete / retire record | **`Archive`** | `Delete`, `Remove`, `Kill`, `Trash` | Domain Entities |

---

### 6.3 The Anti-Hallucination Variable Dictionary (STRICT vs Banned Names)
When instantiating local variables or defining method parameters, you MUST use the exact identifier specified below for its corresponding type.

| Type / Context | STRICT Variable Name | Forbidden / Banned Names | Rationale |
|---|---|---|---|
| **CQRS Command** (`*Command`) | `command` | `cmd`, `req`, `request`, `payload`, `c` | Disambiguates Application Commands from HTTP Requests. |
| **CQRS Query** (`*Query`) | `query` | `qry`, `req`, `request`, `q` | Explicitly identifies read operations. |
| **HTTP Request** (`*Request`) | `request` | `req`, `body`, `payload`, `dto`, `r` | Standardizes Controller API signatures. |
| **Domain Entity** (`Template`) | The entity name (`template`) | `entity`, `model`, `data`, `obj`, `t` | Always use the ubiquitous domain noun. |
| **UseCase Output** (`*ResultDto`) | `result` | `res`, `response`, `dto`, `output` | Standard output container of an Application UseCase. |
| **Cancellation Token** | `ct` | `cancellationToken`, `cancelToken`, `token` | Ubiquitous modern .NET convention. |
| **Deterministic Time** (`DateTimeOffset`)| `now` | `date`, `dt`, `currentTime`, `timestamp` | Clean, universal stamp for auditing and invariant logic. |
| **Entity Primary Key** (`Guid`) | `id` or `{entity}Id` | `guid`, `uuid`, `key`, `identifier` | Clear, standard key naming. |

---

### 6.4 Canonical End-to-End Naming Flow Example
Trace how an operation maintains absolute naming consistency from the HTTP boundary down to persistence:

```csharp
// =========================================================================
// 1. PRESENTATION LAYER: TemplateController.cs
// =========================================================================
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(
        [FromBody] CreateTemplateRequest request, 
        CancellationToken ct)
    {
        // Parameter is 'request' -> Mapped to 'command'
        var command = new CreateTemplateCommand(request.ProjectId, request.Name, request.Slug);
        
        // Output from UseCase is 'result'
        var result = await createTemplateUseCase.ExecuteAsync(command, ct);
        
        return CreatedAtAction(
            nameof(GetById), 
            new { templateId = result.Id }, 
            new ApiResponse<TemplateResultDto>(result));
    }
}

// =========================================================================
// 2. APPLICATION LAYER: CreateTemplateUseCase.cs
// =========================================================================
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
    {
        // 1. Guard check using canonical verb 'ExistsAsync'
        if (await templateRepo.SlugExistsAsync(command.Slug, ct))
        {
            throw new ConflictException($"Template with slug '{command.Slug}' already exists");
        }

        // 2. Obtain time as 'now'
        var now = timeProvider.GetUtcNow();

        // 3. Create domain entity named 'template' via canonical factory 'Create'
        var template = Template.Create(
            command.ProjectId, 
            TemplateName.Create(command.Name), 
            TemplateSlug.Create(command.Slug), 
            now);

        // 4. Persistence using canonical verbs 'AddAsync' and 'CommitAsync'
        await templateRepo.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);

        // 5. Return mapped Result DTO
        return new TemplateResultDto(template.Id, template.Name.Value, template.CreatedAt);
    }
}
```

---

</global_standards>
