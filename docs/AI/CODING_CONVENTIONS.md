# CODING_CONVENTIONS.md — SMK Document Server

> **Purpose:** This document is the Ultimate Single Source of Truth (SSoT) for engineering standards, architectural patterns, and prohibited anti-patterns across the SMK Document Server (`backend-v2/` and `frontend-v2/`). 
> **[AI_DIRECTIVE]:** AI Assistants (LLMs) and developers MUST treat these guidelines as absolute laws. You must read the specific section relevant to your task to guarantee a clean, SOLID, and predictable codebase. Never deviate from these structures.

<ai_directive>
CRITICAL ATTENTION ROUTING: 
- For Backend (C# / .NET), STRICTLY focus on `<backend_scope>` and `<global_standards>`.
- For Frontend (Next.js), STRICTLY focus on `<frontend_scope>` and `<global_standards>`.
- ALWAYS obey `<core_philosophy>` and `<tech_stack>`.
</ai_directive>

---

## 📑 Table of Contents
1. **[Core Philosophy: Clean Architecture & SOLID Design](#1-🏛️-core-philosophy-clean-architecture--solid-design)**
2. **[System Tech Stack](#2-🏗️-system-tech-stack)**
3. **[Backend Standards: C# 12 / .NET 10](#3-🔵-backend-standards-c-12--net-10-backend-v2)**
   - 3.1 The 4-Layer Clean Architecture Responsibilities
   - 3.2 CQRS Data Flow & Object Mapping
   - 3.3 Code Hygiene & Instantiation
   - 3.4 Domain Layer Standards (`SmkDoc.Domain`)
   - 3.5 Application Layer Standards (`SmkDoc.Application`)
   - 3.6 Infrastructure Layer Standards (`SmkDoc.Infrastructure`)
   - 3.7 Presentation Layer & Error Handling (`SmkDoc.Api`)
   - 3.8 Unit Testing Standards
4. **[Frontend Standards: TypeScript / Next.js 15](#4-🟡-frontend-standards-typescript--nextjs-15-frontend-v2)**
5. **[System-Wide Naming Standards & Matrix](#5-🧠-system-wide-naming-standards--matrix-the-6-pillars)**

---

<core_philosophy>
## 1. 🏛️ Core Philosophy: Clean Architecture & SOLID Design

All code written for the SMK Document Server MUST strictly adhere to global software engineering best practices. We prioritize **Readability & Simplicity** over premature optimization.

1. **Clean Architecture & Clean Design:** The system is strictly decoupled. Inner layers (Domain/Application) know nothing about outer layers (Infrastructure/Presentation).
2. **SOLID Principles:** Every class and method must have a single responsibility (SRP). The system is closed for modification but open for extension (OCP) via polymorphic dispatch or pipeline behaviors.
3. **Readability First:** Code must read like clean prose. Choose clear, intention-revealing names over cryptic abbreviations. Code is read far more often than it is written.
4. **KISS (Keep It Simple, Stupid):** Simple and explicit beats clever and convoluted. Introduce abstractions or patterns only when business requirements or scalability genuinely demand them.
5. **DRY & Single Source of Truth (SSoT):** Consolidate duplicated logic, regex patterns, and types into authoritative modules (e.g., `PlaceholderHelper`, `Zod schemas`).
6. **Zero Dead Code:** Maintain pristine hygiene. Remove unused imports, commented-out code blocks, and debug artifacts before committing.

---

</core_philosophy>

<tech_stack>
## 2. 🏗️ System Tech Stack

> **[AI_DIRECTIVE]** The following exact versions MUST be strictly enforced during all code generation and package installation. Do not use newer or older versions unless explicitly commanded.

| Layer | Technology | Version | Notes |
|---|---|---|---|
| Backend API | ASP.NET Core | .NET 10 | Clean Architecture (4 core projects + 2 test projects) |
| Frontend Portal | Next.js | 15 + React 19 | App Router, TypeScript |
| ORM | Entity Framework Core + Npgsql | 10 | Code-first, Migrations |
| Database | PostgreSQL | 15-alpine | `smkdoc` database |
| PDF Conversion | Gotenberg | 8 | Chromium & LibreOffice |
| Object Storage | MinIO | self-host | S3-compatible (`templates/`, `outputs/`) |
| Styling | Tailwind CSS | 3 | CSS Custom Properties (Tokens SSoT) |

---

</tech_stack>

<backend_scope>
## 3. 🔵 Backend Standards: C# 12 / .NET 10 (`backend-v2/`)

**Core Stack:** C# 12, ASP.NET Core 10 Web API, Entity Framework Core 10 (PostgreSQL).  
**Architecture:** The backend strictly follows a 4-Layer Clean Architecture. All development must respect these boundaries:

### 3.1 The 4-Layer Clean Architecture Responsibilities
1. **Domain Layer (`SmkDoc.Domain`):** 
   - **The Business Core:** Contains Entities, Value Objects, and Domain Exceptions.
   - **Zero Dependencies:** Must NEVER depend on external NuGet packages, database frameworks, or outer layers.
   - **Invariant Protection:** Responsible for safeguarding business rules and data integrity at all times.
2. **Application Layer (`SmkDoc.Application`):** 
   - **The Orchestrator:** Controls the system flow via UseCases.
   - **CQRS Segregation:** Strictly divides state-mutating operations (Commands) from read-only display operations (Queries).
   - **Dependency Inversion:** Defines the interface contracts (e.g., `IRepository`) that the Infrastructure layer must fulfill.
3. **Infrastructure Layer (`SmkDoc.Infrastructure`):** 
   - **The Technology Hub:** Houses all external concerns like EF Core, PostgreSQL (Npgsql), MinIO, and Gotenberg.
   - **Fluent API Mapping:** Maps database tables and columns exclusively via `IEntityTypeConfiguration<T>`. NEVER pollute Domain Entities with EF Core Data Annotations.
4. **Presentation Layer (`SmkDoc.Api`):** 
   - **The Delivery Mechanism:** Acts solely as the HTTP entry point.
   - **Thin Controllers:** Routes parameters to Application UseCases and returns results. MUST NEVER contain any business logic.

### 3.2 CQRS Data Flow & Object Mapping
Strictly adhere to this object lifecycle. Never leak inner objects to outer layers.

```text
  [ Client / Browser ]
          │  (HTTP Body / JSON)
          ▼
     [ Request ]  ──► Presentation Layer (API / Controller)
          │
  (Mapped into Application Layer)
          ▼
     [ Command ]  (Write: Mutates state in DB)
        - OR -
     [ Query ]    (Read: Retrieves data without side-effects)
          │
          ▼
  ┌───────────────────────────────────────────────┐
  │ Use Case / Handler (Application Core)         │
  │  - Executes Business Rules with Domain logic  │
  └───────────────────────┬───────────────────────┘
                          │
                          ▼
               [ Result / Result DTO ]  ──► Wrapped inside Application
                          │
  (Mapped into Presentation Envelope)
                          ▼
     [ ApiResponse<T> ]  ──► HTTP 200/201 (Controller Response)
```

### 3.3 Code Hygiene & Instantiation
- **Explicit Variable Instantiation:** ALWAYS assign newly created objects (e.g., `new Command(...)`) to explicit local variables before passing them into methods. Do not nest object creation inside method arguments.
  ```csharp
  // ❌ Bad: Nested instantiation is hard to read and debug
  var result = await useCase.ExecuteAsync(new CreateTemplateCommand(req.Name, req.Slug));

  // ✅ Good: Explicit variables
  var command = new CreateTemplateCommand(req.Name, req.Slug);
  var result = await useCase.ExecuteAsync(command);
  ```
- **Fail Fast (Early Returns):** Avoid the "Arrow Anti-Pattern" (deeply nested `if` statements). Return or throw exceptions as early as possible to keep the "happy path" linear.
- **No Magic Strings or Numbers:** NEVER hardcode status codes, roles, or configuration keys directly in logic. Always use `const`, `Smart Enums`, or read from `appsettings.json`.
- **Primary Constructors (Zero Boilerplate):** Standardize on C# 12 `camelCase` for all injected dependencies. **NEVER re-declare them as `private readonly` fields.** Use the injected parameter directly in your methods.
  ```csharp
  // ❌ Bad: Re-declaring fields defeats the purpose of Primary Constructors
  public class TemplateController(ITemplateRepository repo) 
  {
      private readonly ITemplateRepository _repo = repo; 
  }

  // ✅ Good: Use the injected parameter directly
  public class TemplateController(ITemplateRepository templateRepo) 
  {
      public async Task Get() => await templateRepo.GetAllAsync();
  }
  ```

### 3.4 Domain Layer Standards (`SmkDoc.Domain`)

**The Golden Rules:**
- **Persistence Ignorance:** Zero external dependencies. MUST NEVER use `using` statements for external frameworks like EF Core, Npgsql, or FluentValidation.
- **Pure POCO:** Everything must be Plain Old CLR Objects (POCO).
- **Always-Valid State (Encapsulation):** Domain objects must be valid from the moment of creation and throughout their lifecycle. Encapsulate state using `private set;` and mutate only via descriptive business verbs (e.g., `Publish`, `Archive`). Expose a canonical Factory Method (e.g., `Create`) and keep constructors `internal`.
- **Domain Events (Decoupling Side-Effects):** Use `IDomainEvent` to signal when significant business state changes occur (e.g. `TemplateCreatedEvent`). Raise events from within the entity and handle them in separate `INotificationHandler` classes. Never mix core business logic with side-effects (like sending emails) inside a single UseCase.

**Directory Layout & Responsibilities:**
```text
src/SmkDoc.Domain/
├── Common/            # Base classes and abstract types (e.g., Entity, AggregateRoot)
├── Entities/          # Business models with unique Identity (ID)
├── ValueObjects/      # Immutable objects measured by their internal values, lacking Identity
├── Enums/             # Business state constants and Smart Enums
├── Exceptions/        # Domain-specific exceptions (Business Rule Violations)
└── Interfaces/        # Contracts (e.g., IRepository) enabling Dependency Inversion
```

**Code Comparison: Anemic vs Rich Domain Models**
```csharp
// ❌ Bad: Anemic Model & Framework Pollution
using System.ComponentModel.DataAnnotations; // Violation: External Dependency

public class Template 
{
    [Required] // Violation: Data Annotation inside Domain
    public string Name { get; set; } // Violation: Public setter (No Encapsulation)
    public bool IsActive { get; set; }
    
    public Template() { } // Violation: Public empty constructor allows invalid state
}

// ✅ Good: Rich Model, Pure POCO, Always-Valid State
public sealed class Template : BaseEntity
{
    public TemplateName Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    // 1. Private parameterless constructor strictly for EF Core materialization
    private Template() { }

    // 2. Internal constructor enforces invariants at creation
    internal Template(Guid? id, TemplateName name, DateTimeOffset now) 
        : base(id, createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        IsActive = true;
    }

    // 3. Canonical Factory is the only public way to create the entity
    public static Template Create(TemplateName name, DateTimeOffset now) => 
        new(null, name, now); 

    // 4. Mutate via business verbs & always track time
    public void Deactivate(DateTimeOffset now) 
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }
}
```

### 3.5 Application Layer Standards (`SmkDoc.Application`)

**Core Principles:**
- **Zero Framework Dependency:** MUST NEVER reference `Microsoft.EntityFrameworkCore`, `Npgsql`, or `Microsoft.AspNetCore.Mvc`. The Application layer orchestrates business rules, not database queries or HTTP responses.
- **Dependency Inversion (DIP):** All interactions with the outside world (Database, Disk, JWT, Storage) MUST be executed through Abstraction Interfaces (e.g., `IStorageService`, `ITemplateRepository`).
- **DTOs as Data Containers:** Never accept raw HTTP Request objects. Never leak Domain Entities out of the UseCase. Always map to and from DTOs or Result Objects. **You MUST use C# 9+ `record` types (Positional Records) for all DTOs and Requests** to ensure immutability and conciseness (e.g. `public record CreateTemplateRequest(string Name);`).

**Directory Layout (Vertical Slicing):**
```text
src/SmkDoc.Application/
├── Common/
│   ├── Interfaces/        # Application contracts (IStorageService, IRenderEngine, IUseCase)
│   ├── Exceptions/        # Application-level exceptions (e.g., NotFoundException)
│   └── Helpers/           # Shared utility logic (e.g., ThaiDataTransformer)
└── Modules/               # Feature Slices organized by Business Capability
    ├── Authoring/         # e.g., CreateTemplateUseCase, ManageFieldMappingUseCase
    ├── Rendering/         # e.g., GenerateDocumentUseCase, PreviewDocumentUseCase
    ├── IdentityAccess/    # e.g., LoginUseCase, RefreshTokenUseCase
    └── Integration/       # e.g., SyncExternalDataUseCase
```

- **Strict Action-Centric Use Cases:** Organize UseCases inside their respective `Modules/`, implementing `IUseCase<TRequest, TResponse>`.
- **Validation Offloading (DRY Boundary):** Use `FluentValidation` strictly for **Input/Format Validation** (e.g., string length, regex matching) and abstract it into pipeline behaviors. Use Domain Entities strictly for **Business Invariant Validation** (e.g., state transitions, relationship logic). NEVER validate the same rule twice across both layers.

**Code Comparison: CQRS UseCase Patterns**
```csharp
// ❌ Bad: Framework Leaks, No DTOs, Missing Cancellation Tokens
public class CreateTemplateUseCase
{
    private readonly ApplicationDbContext _db; // Violation: EF Core leaked into Application

    public CreateTemplateUseCase(ApplicationDbContext db) => _db = db;

    public async Task<Template> ExecuteAsync(CreateTemplateCommand cmd) // Violation: Returning Domain Entity
    {
        if (string.IsNullOrEmpty(cmd.Name)) throw new Exception("400"); // Violation: HTTP status leaked
        
        var template = new Template { Name = cmd.Name }; 
        _db.Templates.Add(template);
        await _db.SaveChangesAsync(); // Violation: Missing CancellationToken (ct)

        return template; // Violation: Exposing Entity directly to Presentation
    }
}

// ✅ Good: Strict Abstractions, DTOs, Deterministic Time, and Cancellation Tokens
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo, // Dependency Inversion
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand cmd, CancellationToken ct = default)
    {
        // 1. Domain Object Creation (Encapsulated)
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(cmd.ProjectId, TemplateName.Create(cmd.Name), now);

        // 2. IO Operations via Abstractions
        await templateRepo.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);

        // 3. Map to DTO before returning
        return new TemplateResultDto(template.Id, template.Name.Value, template.CreatedAt);
    }
}
```

### 3.6 Infrastructure Layer Standards (`SmkDoc.Infrastructure`)

**Core Architectural Rules:**
- **Dependency Inversion Principle (DIP):** Infrastructure services MUST implement interfaces defined in the Application or Domain layers. The Infrastructure layer never creates its own public interfaces for outer layers to consume.
- **Isolation of External Tech:** Hide the complexity of external SDKs (e.g., MinIO, Gotenberg, QRCoder, EF Core). Do not leak provider-specific exception types (e.g., `SqlException`, `NpgsqlException`) back to the Application layer.
- **Technical Mapping Boundary:** Map database schemas to Domain Entities exclusively via the Fluent API (`IEntityTypeConfiguration<T>` or `OnModelCreating`). NEVER pollute Domain Entities with `[Table]`, `[Column]`, or other EF Core Data Annotations.
- **Async/Await Integrity:** End all asynchronous methods with the `Async` suffix (e.g., `ExecuteAsync`). Always pass `CancellationToken ct` to every I/O-bound operation. Await operations explicitly; never use `.Result` or `.Wait()`.

**Directory Layout & Responsibilities:**
```text
src/SmkDoc.Infrastructure/
├── Persistence/           # EF Core (PostgreSQL) operations
│   ├── AppDbContext.cs    # EF Core context (Fluent API Mapping)
│   ├── Repositories/      # Implementations of Domain/Application IRepository contracts
│   ├── Queries/           # Fast, read-only queries (CQRS Read Models)
│   └── Migrations/        # EF Core Code-First migration history
├── Storage/               # Implementations of IStorageService (e.g., MinIO)
├── Engines/               # Implementations of IRenderEngine (Html, Word, Excel)
├── Pdf/                   # HTTP client integrating with Gotenberg
├── Imaging/               # QR Code / Barcode generators (QRCoder, SkiaSharp)
├── Schema/                # JSON Schema inference and validation logic
└── Security/              # Bcrypt, JWT token generation, DataProtection
```

**Code Comparison: Database Mapping & Tracking**
```csharp
// ❌ Bad: Polluting Domain with DB logic & Missing AsNoTracking
using System.ComponentModel.DataAnnotations.Schema;

[Table("templates")] // Violation: Data Annotation inside Domain layer
public class Template { ... }

public async Task<Template> GetTemplateDataAsync(Guid id)
{
    // Violation: Missing AsNoTracking for a read-only query
    return await _dbContext.Templates.FirstOrDefaultAsync(t => t.Id == id);
}

// ✅ Good: Clean Domain, Fluent API Mapping, and AsNoTracking for Reads
public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        // 1. Fluent API keeps the Domain class completely clean of DB metadata
        builder.ToTable("templates");
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(100);
    }
}

public async Task<Template?> GetTemplateDataForDisplayAsync(Guid id, CancellationToken ct)
{
    // 2. AsNoTracking() vastly improves performance for Read-Only operations
    return await _dbContext.Templates
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == id, ct);
}
```

### 3.7 Presentation Layer & Error Handling (`SmkDoc.Api`)

**Core Architectural Rules:**
- **Thin Controllers (No Business Logic):** Controllers solely exist to receive HTTP requests, map them to CQRS Commands/Queries, and return standard HTTP responses. MUST NEVER contain `if/else` business rules, database calls, or complex logic.
- **Orchestrator of Cross-Cutting Concerns:** Handles API security via Middleware, global exception trapping via .NET 8 `IExceptionHandler`, and readiness via Health Checks.
- **Deterministic HTTP Status Codes:**
  - `201 Created`: Resource creation (Must return `ApiResponse<T>`).
  - `200 OK`: Reads, queries, or idempotent updates returning data.
  - `204 NoContent`: Deletions or state changes returning no body.
- **RFC 7807 Compliance:** All HTTP error responses MUST conform to `ProblemDetails`. NEVER return anonymous types (e.g., `new { error = ... }`).
- **Domain Exception Mapping:** Throw strongly-typed exceptions from UseCases, letting the Global Filter map them:
  - `DomainValidationException` / `BusinessRuleViolationException` -> `400 BadRequest`
  - `NotFoundException` -> `404 NotFound`
  - `ConflictException` -> `409 Conflict`
- **Composition Root (`Program.cs`):** The central hub for assembling the application, registering cross-layer Dependency Injection (DI), and configuring the HTTP pipeline.

**Directory Layout & Responsibilities:**
```text
src/SmkDoc.Api/
├── Controllers/         # Thin endpoints divided by bounded context
│   ├── DocumentController.cs
│   └── TemplateController.cs
├── Contracts/           # Request/Response DTOs specific to HTTP APIs (Not Domain/App layer)
├── ExceptionHandlers/   # .NET 8 IExceptionHandler implementations
│   └── GlobalExceptionHandler.cs
├── Middleware/          # Low-level request pipeline interception
│   ├── ApiKeyMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
├── HealthChecks/        # Infrastructure readiness probes (Postgres, MinIO, Gotenberg)
├── appsettings.json     # Environment configurations
└── Program.cs           # WebApplication builder and DI setup
```

**Code Comparison: Controller & Middleware Standards**
```csharp
// ❌ Bad: Fat Controller & Bad Error Handling
public class TemplateController(ITemplateRepository _repo) // Violation: Underscore in primary constructor
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest req)
    {
        // Violation: Business logic inside Controller
        if (string.IsNullOrEmpty(req.Name)) 
            return BadRequest(new { error = "Name needed" }); // Violation: Anonymous type breaks RFC 7807

        var entity = new Template(req.Name);
        await _repo.AddAsync(entity); // Violation: Bypassing Application layer UseCases
        return Ok(entity); // Violation: Returning Domain Entity directly
    }
}

// ✅ Good: Thin Controller & RFC 7807 Compliance
[ApiController]
[Route("api/v1/templates")]
public class TemplateController(IUseCase<CreateTemplateCommand, TemplateResultDto> createUseCase) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create(
        [FromBody] CreateTemplateRequest request, 
        CancellationToken ct)
    {
        // 1. Map to Application Command
        var command = new CreateTemplateCommand(request.Name, request.ProjectId);
        
        // 2. Delegate to Application Layer
        var result = await createUseCase.ExecuteAsync(command, ct);
        
        // 3. Return Standard Envelope
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
    }
}

// ✅ Good: Middleware RFC 7807 enforcement
public async Task InvokeAsync(HttpContext context)
{
    if (!IsValidKey(context))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        
        // Enforces RFC 7807 standard format
        var problem = new ProblemDetails 
        { 
            Title = "Unauthorized", 
            Status = 401, 
            Detail = "Invalid API Key" 
        };
        await context.Response.WriteAsJsonAsync(problem); 
        return;
    }
    await _next(context);
}
```

### 3.8 Testing Standards (Unit & Integration)

**Core Testing Philosophy:**
- **The 3-Part Naming Rule:** All test methods MUST follow the pattern: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `Create_WithEmptyProjectId_ThrowsDomainValidationException`).
- **Deterministic Time:** Tests MUST be 100% deterministic. NEVER use `DateTime.UtcNow` or `DateTime.Now`. Always inject a static time via `TimeProvider` or a hardcoded `DateTimeOffset` variable.
- **Strict Mocking Rules:** ONLY mock Infrastructure interfaces (e.g., `IRepository`, `IStorageService`). NEVER mock Domain Entities or Data Transfer Objects (DTOs); instantiate them directly.
- **Side-Effect Verification:** On exception/guard paths, always explicitly verify that mutations NEVER occurred: `_uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);`
- **Clear AAA Anatomy:** Visually separate **Arrange**, **Act**, and **Assert** phases using blank lines.
- **Fluent Assertions:** Always use `FluentAssertions` (e.g., `.Should().Be()`) instead of standard xUnit `Assert`.

**The 3 Golden Archetypes:**

#### 🌟 Archetype A: UseCase Orchestration Test
- **Location:** `Application/Modules/{Feature}/Commands/{Action}/{Action}UseCaseTests.cs`
- **Focus:** Verifying workflow, mapping, and side-effects.
- **Rules:** Use a shared `TestFixture` to encapsulate Moq setups. Use a unified `CreateSut()` factory. Verify exact method invocations using `Times.Once` or `Times.Never`.

#### 🌟 Archetype B: Input Validator Test
- **Location:** Colocated alongside Command/Query tests.
- **Focus:** Exhaustive validation of input boundaries.
- **Rules:** Must be pure parameterized tests using `[Theory]` and `[InlineData]`. Zero mocks allowed. 

#### 🌟 Archetype C: Domain Aggregate Root Test
- **Location:** `Domain/Entities/{Aggregate}Tests.cs`
- **Focus:** Testing business invariants, exceptions, and internal state mutations.
- **Rules:** Instantiate the entity directly. Verify that properties change as expected and that audit fields (`UpdatedAt`) are accurately stamped with the injected time.

**Code Comparison: Testing Standards**
```csharp
// ❌ Bad: Non-Deterministic, Poor Naming, Cluttered AAA
[Fact]
public async Task Test_CreateTemplate()
{
    var useCase = new CreateTemplateUseCase(new MockRepo().Object); // Bad setup
    var cmd = new CreateTemplateCommand(Guid.NewGuid(), "Test", "slug", null);
    
    var result = await useCase.ExecuteAsync(cmd); // No separation
    Assert.NotNull(result); // Legacy assert
    Assert.Equal("Test", result.Name);
}

// ✅ Good: Deterministic, 3-Part Naming, Clear AAA, Fluent Assertions
[Fact]
public async Task ExecuteAsync_WhenValidHtmlTemplate_ShouldPersistAndReturnTemplateResultDto()
{
    // Arrange
    var projectId = Guid.NewGuid();
    _fixture.TemplateRepo
        .Setup(r => r.SlugExistsAsync("tax-invoice", projectId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    
    var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

    // Act
    var response = await CreateSut().ExecuteAsync(command);

    // Assert
    response.Should().NotBeNull();
    response.Name.Should().Be("Tax Invoice");
    
    _fixture.TemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

#### 🌟 Archetype D: Integration Tests (The Real World)
- **Location:** `SmkDoc.IntegrationTests/`
- **Focus:** Verifying real database constraints, mapping, and external services (MinIO, Gotenberg).
- **Rules:** MUST use `Testcontainers` (PostgreSQL, MinIO) to spin up real ephemeral containers. NEVER use In-Memory EF Core provider, as it behaves differently than PostgreSQL (e.g., date truncation, unique constraints). 

### 3.9 Nullability & Control Flow
- **Strict Nullability:** The project has `<Nullable>enable</Nullable>`. You MUST handle nulls gracefully. Never use the `!` (dammit) operator unless explicitly checking via `Guard.NotNull`.
- **Exception-Driven Control Flow:** For validation and business logic failures, throw strongly-typed Domain Exceptions (e.g., `NotFoundException`, `ConflictException`). We do **NOT** use the `Result<T>` pattern for control flow in this architecture.

---

</backend_scope>

<frontend_scope>
## 4. 🔶 Frontend Standards: TypeScript / Next.js 15 (`frontend-v2/`)

### 3.1 Strict TypeScript & Zod-First Validation (SSoT)
- **Zero `any` Policy:** Avoid the `any` type entirely. If a type is truly unknown, use `unknown` and apply type narrowing.
- **Zod as the Single Source of Truth:** Place all validation rules in `src/schemas/`. 
  - Derive TypeScript types automatically via `export type MyType = z.infer<typeof MySchema>;`. 
  - NEVER manually create interfaces in `src/types/` if a Zod schema represents the same data structure.
  - All external API responses and form submissions MUST be validated through Zod before usage.

### 3.2 Component Architecture & Organization
Strictly divide UI components into three categories to prevent tangled dependencies:
```text
src/components/
├── ui/         # "Dumb" primitives (Button, Input, Modal). Highly reusable. Zero business logic.
├── layout/     # "Shells" (Sidebar, Navbar, PageHeader). Dictates page structure.
└── features/   # "Smart" components organized by domain (e.g., /templates, /users). 
                # These can fetch data, use contexts, and contain business logic.
```
- **Tailwind Safing:** Always use the `cn()` utility (`clsx` + `tailwind-merge`) when composing Tailwind class names dynamically to prevent CSS collision.

### 3.3 Next.js 15 App Router Paradigms
- **Server Components by Default:** `page.tsx` and `layout.tsx` MUST remain Server Components (no `"use client"`). Use them to fetch initial data directly from the API/Database securely and quickly.
- **Isolate Client Components:** Place the `"use client"` directive ONLY at the leaves of the component tree (e.g., an interactive form, a chart, or a toggle button). Never wrap an entire page in `"use client"`.
- **Avoid `useEffect` for Fetching:** NEVER use `useEffect` for data fetching. Use React Server Components for initial loads, and React Query/SWR for client-side mutations or polling.

**Code Comparison: App Router Data Fetching**
```tsx
// ❌ Bad: Legacy React 18 style (Entire page is client-side, bloated bundle, slow SEO)
"use client"; 
import { useEffect, useState } from "react";

export default function TemplatePage({ params }: { params: { id: string } }) {
    const [data, setData] = useState<Template | null>(null);

    useEffect(() => {
        // Violation: Fetching initial data in a useEffect
        fetch(`/api/v1/templates/${params.id}`).then(res => res.json()).then(setData);
    }, [params.id]);

    if (!data) return <Loading />;
    return <TemplateEditor data={data} />;
}

// ✅ Good: Next.js 15 Server Component (Fast, secure, SEO-friendly)
import { notFound } from "next/navigation";
import { fetchTemplateById } from "@/lib/api/templates"; // Server-side fetcher
import { TemplateEditorClient } from "@/components/features/templates/TemplateEditorClient";

// 1. NO "use client" here. This runs securely on the server.
export default async function TemplatePage({ params }: { params: { id: string } }) {
    // 2. Direct async/await data fetching
    const data = await fetchTemplateById(params.id);
    
    if (!data) return notFound();

    // 3. Pass data down to the isolated Client Component that needs interactivity
    return (
        <main className="container mx-auto">
            <h1 className="text-2xl font-bold">{data.name}</h1>
            <TemplateEditorClient initialData={data} />
        </main>
    );
}
```

---

</frontend_scope>

## 5. 🏷️ System-Wide Naming Standards & Matrix (The 6 Pillars)
<global_standards>

Adhere strictly to these naming structures to guarantee context continuity:

| Element / Artifact | Layer / Scope | Convention | Example |
|---|---|---|---|
| **Domain Entities** | Domain | Singular `PascalCase` | `Template`, `User` |
| **Value Objects** | Domain | Semantic `PascalCase` | `TemplateSlug`, `Sha256Hash` |
| **Domain Factory** | Domain | Canonical Verb | `Create()`, `Register()` |
| **Business Mutations** | Domain | Domain Verb | `Publish()`, `Archive()` |
| **Primary Ctor Param** | All C# | C# 12 `camelCase` | `templateRepo`, `unitOfWork` |
| **Mutation Inputs** | Application | `*Command` | `CreateTemplateCommand` |
| **Query Inputs** | Application | `record *Query` | `record GetTemplateByIdQuery(Guid Id);` |
| **Output DTOs** | Application | `record *ResultDto` | `record TemplateResultDto(Guid Id);` |
| **HTTP Requests** | Presentation | `record *Request` | `record CreateTemplateRequest(string Name);` |
| **Controllers** | Presentation | `*Controller` | `TemplateController` |
| **Test Classes** | Tests | `*Tests` | `CreateTemplateUseCaseTests` |
| **Zod Schemas** | Frontend | `camelCase` + `Schema` | `createTemplateSchema` |
| **React Components** | Frontend | `PascalCase.tsx` | `TemplateCard.tsx` |
| **Custom Hooks** | Frontend | `use` + `PascalCase.ts` | `useTemplates.ts` |
| **REST Route URLs** | Presentation | `kebab-case` plural | `/api/v1/projects/{id}` |
| **Database Tables** | Persistence | `plural_snake_case` | `templates`, `generation_logs` |
| **Database Columns** | Persistence | `snake_case` / `is_*` | `project_id`, `is_active` |

### 5.1 Variable & Parameter Naming Dictionary (The Anti-Hallucination Matrix)
LLMs and developers frequently use inconsistent variable names (e.g., swapping between `req`, `request`, `cmd`, and `command`). **You MUST strictly use the exact variable names listed below based on their context/type.**

| Context / Type | STRICT Variable Name | Forbidden / Banned Names | Reason |
|---|---|---|---|
| **CQRS Commands** (`*Command`) | `command` | `req`, `request`, `cmd`, `payload` | Separates Application Commands from HTTP Requests. |
| **CQRS Queries** (`*Query`) | `query` | `req`, `request`, `qry` | Explicitly identifies read operations. |
| **HTTP Requests** (`*Request`) | `request` | `req`, `body`, `payload` | Standardizes Controller API endpoints. |
| **Domain Entities** (`Template`) | The class name (`template`, `user`) | `entity`, `model`, `data`, `obj` | Avoids generic terms. Use the actual domain name. |
| **UseCase Results** (`*ResultDto`) | `result` | `res`, `response`, `dto` | Denotes the output of an Application layer operation. |
| **CancellationToken** | `ct` | `cancellationToken`, `cancelToken` | Short, ubiquitous convention across modern .NET. |
| **Current Time** (`DateTimeOffset`) | `now` | `date`, `dt`, `currentTime` | Short and contextually clear for auditing fields. |
| **Primary Keys** (`Guid`) | `id` or `{Entity}Id` | `guid`, `uuid`, `key` | E.g., `id` (if context is obvious) or `projectId`. |

**Example of PERFECT Naming Enforcement:**
```csharp
// Controller Layer
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
{
    var command = new CreateTemplateCommand(request.Name); // Map request -> command
    var result = await _useCase.ExecuteAsync(command, ct); // Execute command -> result
    return Ok(new ApiResponse<TemplateResultDto>(result));
}

// Application Layer
public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
{
    var now = _timeProvider.GetUtcNow(); // Get time as 'now'
    var template = Template.Create(command.Name, now); // Create entity as 'template'
    await _repo.AddAsync(template, ct);
    return new TemplateResultDto(template.Id); // Return 'result' mapping
}
```
</global_standards>
