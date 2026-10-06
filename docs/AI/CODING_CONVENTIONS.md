# CODING_CONVENTIONS.md — SMK Document Server

> **Canonical Engineering Standards & Conventions**  
> Applicable across all backend (`backend-v2/`) and frontend (`frontend-v2/`) codebases.  
> Updated September 2026.

---

## 1. 🏛️ Core Philosophy: Clean Code & Simplicity

1. **Readability First:** Code must read like clean prose. Choose clear, intention-revealing names over cryptic abbreviations. Code is read far more often than it is written.
2. **Small & Focused Units (SRP):** Functions, methods, and components must remain short, doing exactly one thing well with clear boundaries. If a method exceeds ~30–40 lines, investigate if private helper methods or specialized services should be extracted.
3. **KISS (Keep It Simple, Stupid):** Simple and explicit beats clever and convoluted. Never introduce premature abstractions, design patterns, or layers unless business requirements or scalability targets genuinely warrant them.
4. **DRY & Single Source of Truth (SSoT):** Never duplicate logic, regex patterns, or types. Always import from authoritative SSoT modules:
   - Placeholder Regex: `PlaceholderHelper.Pattern`
   - Thai Formatting: `ThaiDataTransformer`
   - Frontend API Client: `apiClient<T>` & `apiClientBlob`
   - Frontend Types: `types/api.ts` (re-exported from Zod `schemas/`)
5. **Zero Dead Code & High Hygiene:** Never leave commented-out code blocks, unused imports, redundant whitespace, or debug artifacts (`Console.WriteLine`, `console.log`) in submitted code. Maintain pristine namespace and formatting consistency.
6. **Invariant-Driven Documentation (Zero High-Churn Drift):** Documentation, PR descriptions, and guidelines must describe system capabilities using **Invariant-Driven Quality Gates** (e.g. 100% test pass rate with 0 errors/failures, Bounded Context architectural boundaries) rather than volatile frozen metrics (e.g. "614 tests", "17 controllers"). Business and domain constants (such as 1 Canonical Factory per Entity, SHA-256 = 64 characters, MaxChangeNoteLength = 500) must remain strictly exact (❌ AP-041).

---

## 2. 🔷 Backend Standards: C# 13 / .NET 10 (`backend-v2/`)

### 2.1 Solution-Wide Code Hygiene & Modern C# Standards

These three hygiene pillars apply universally across **ALL C# layers** (Domain, Application, Infrastructure, Presentation, Tests) without exception:

#### 2.1.1 Namespace & Usings Hygiene
- **File-Scoped Namespaces:** Always use file-scoped namespaces to reduce unnecessary indentation:
  ```csharp
  namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
  ```
- **Clean Usings (Zero Inline Namespaces):** All external types, exceptions, and DTOs MUST be imported at the top of the file via `using` directives. **NEVER** write inline fully-qualified namespaces in method bodies, signatures, or attributes (❌ AP-037).
  ```csharp
  // ❌ WRONG — Inline namespace clutter and risk of layer leaking
  throw new SmkDoc.Domain.Exceptions.DomainValidationException("Invalid input.");

  // ✅ CORRECT — Clean top-level using + concise symbol
  using SmkDoc.Domain.Exceptions;
  ...
  throw new DomainValidationException("Invalid input.");
  ```
- **Zero Unused Usings:** Remove all redundant `using` directives before committing code.

#### 2.1.2 Standardized Primary Constructor Parameter Naming
Primary constructors are standard for Dependency Injection across UseCases, Controllers, Repositories, and Services. Parameter names must strictly follow these rules:
- **1:1 camelCase Mapping:** Parameter names MUST directly mirror the class or interface name in `camelCase`:
  - *Interface dependencies:* Drop the `I` prefix and convert to `camelCase` (e.g., `ITemplateRepository` → `templateRepo` or `templateRepository`, `IUnitOfWork` → `unitOfWork`, `IValidator<T>` → `validator`, `ILogger<T>` → `logger`).
  - *UseCase dependencies (in Controllers):* Use `camelCase` matching the UseCase class name (e.g., `CreateTemplateUseCase` → `createTemplateUseCase` or `createUseCase`, `ValidateTemplatePayloadUseCase` → `validatePayloadUseCase`).
- **BAN Underscore Prefix (`_`):** Primary constructor parameters are NOT private fields; **NEVER** prefix them with `_` (❌ `_templateRepo`, ❌ `_unitOfWork`) (❌ AP-038).
- **BAN Ambiguous Generic Names:** Never use generic or vague names like `service`, `repo`, `helper`, or `handler` without context.

#### 2.1.3 Whitespace Consistency & Clean Layout Rhythm
- **Single Blank Line Between Members:** Maintain a consistent 1-blank-line rhythm between methods, properties, and constructors. **NEVER** leave two or more consecutive blank lines (`\n\n\n`).
- **Zero Blank Lines at Boundaries:** Do NOT leave blank lines immediately after opening braces `{` or immediately before closing braces `}` of classes, records, or methods.
- **Zero Trailing Whitespace:** Lines must not contain trailing spaces or tabs.
- **Zero Dead Code:** Never leave commented-out code blocks, unused local variables, or debug artifacts (`Console.WriteLine`).

#### 2.1.4 Language Idioms & Safety
- **Primary Constructors for DI:** Use primary constructors for dependency injection across UseCases, Controllers, and Services to eliminate boilerplate fields:
  ```csharp
  public sealed class CreateTemplateUseCase(
      ITemplateRepository templateRepo,
      IUnitOfWork unitOfWork,
      IValidator<CreateTemplateCommand> validator) : IUseCase<CreateTemplateCommand, TemplateResponse>
  {
      // Dependencies are immediately accessible without field declarations
  }
  ```
- **Sealed Classes:** Mark UseCases, DTO records, validators, and service implementations as `sealed` by default unless specifically designed for inheritance.
- **Nullable Reference Types (NRT):** NRT is enabled across the solution. Treat warnings as errors:
  - Do NOT use the null-forgiving operator (`!`) blindly.
  - Explicitly handle nullability with guards, pattern matching, or null-coalescing expressions:
    ```csharp
    var template = await templateRepo.GetByIdAsync(id, ct)
        ?? throw new NotFoundException($"Template '{id}' not found.");
    ```
#### 2.1.5 Strict Pure DDD Domain Entity Standards (ADR-023 — Reference: Template.cs)
Domain Entities in `SmkDoc.Domain/Entities/` must strictly adhere to pure DDD principles:
1. **Single Canonical Factory Method (SSoT):**
   - Entities must define exactly **one public `Create` factory method**.
   - Parameters must be strongly-typed **Value Objects only** (e.g., `TemplateName`, `TemplateSlug`).
   - Requires explicit, deterministic timestamp (`DateTimeOffset now`).
   - **Zero Primitive Overloads:** Never declare `Create(string name, string slug)` in the Entity. Application UseCases are responsible for converting DTO primitives to Value Objects.
2. **Zero Test-Specific Backdoors in Domain:**
   - **Never declare `CreateForTest`** inside `SmkDoc.Domain.dll`.
   - Test instantiations with custom `Id` or pre-populated state belong in `SmkDoc.Tests/Common/Builders/` (`*Builder`) or `Factories/` (`*TestFactory`).
3. **Internal Parameterized Constructor:**
   - The parameterized constructor is `internal`, granting instantiation access exclusively to `SmkDoc.Domain` and `SmkDoc.Tests` via `[assembly: InternalsVisibleTo("SmkDoc.Tests")]`.
   - The parameterless constructor is `private` for EF Core materialization only.
4. **Canonical Entity Template (`Template.cs` Reference):**
   ```csharp
   public sealed class Template : BaseEntity, IMustHaveProject
   {
       public Guid ProjectId { get; private set; }
       public TemplateName Name { get; private set; } = null!;
       public TemplateSlug Slug { get; private set; } = null!;
       public string? Category { get; private set; }
       public bool IsActive { get; private set; }

       // Private EF Core ctor
       private Template() { }

       // Internal ctor accessible to Test Builders via InternalsVisibleTo
       internal Template(Guid? id, Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now)
           : base(id, createdAt: now)
       {
           ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
           Name = Guard.NotNull(name, nameof(Name));
           Slug = Guard.NotNull(slug, nameof(Slug));
           Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
           IsActive = true;
       }

       // Single Canonical Factory Method
       public static Template Create(Guid projectId, TemplateName name, TemplateSlug slug, string? category, DateTimeOffset now) =>
           new(null, projectId, name, slug, category, now);
   }
   ```

### 2.2 DTOs & Positional Records
- All DTOs in `SmkDoc.Application/DTOs/` MUST be **immutable positional records**:
  ```csharp
  // ✅ CORRECT — Immutable Positional Record
  public sealed record TemplateResponse(
      Guid Id,
      Guid ProjectId,
      string Name,
      string Slug,
      string? Category,
      bool IsActive,
      Guid? CurrentVersionId,
      string? FileFormat,
      DateTimeOffset CreatedAt,
      DateTimeOffset? UpdatedAt);

  // ❌ WRONG — Mutable class with getters/setters
  public class TemplateDto { public Guid Id { get; set; } }
  ```
- **Command vs Query vs Result Naming:**
  - Mutation inputs: Suffix with `*Command` (e.g., `CreateTemplateCommand`, `GenerateDocumentCommand`)
  - Read/Filter inputs: Suffix with `*Query` (e.g., `GetTemplateByIdQuery`, `ListTemplatesQuery`)
  - Outputs: Suffix with `*Response`, `*Dto`, or `*ResultDto` (e.g., `TemplateResponse`, `DocumentVersionDto`, `LoginResultDto`)
- **Presentation Layer Contracts (`SmkDoc.Api/Contracts/{BoundedContext}/`):**
  - HTTP Request and Response models MUST be decoupled positional `record` types located in feature folders (e.g., `Contracts/IdentityAccess/Projects/CreateProjectRequest.cs`).
  - **Zero Inheritance from Commands:** Request records MUST NOT inherit from Application Commands (`Request : Command`). Controllers explicitly map HTTP Request inputs into UseCase Commands (e.g., combining route params/claims with request body).
  - **Direct DTO Returns:** Controllers return Application DTOs directly wrapped in `ApiResponse<T>` / `PagedApiResponse<T>`. Never create shallow empty subclasses (e.g., `ProjectListItemDto : ProjectResultDto`).

### 2.3 Async/Await & Cancellation Safety
- **Non-blocking Async All the Way:** Every method performing I/O (Database, MinIO, Gotenberg HTTP) MUST return `Task` or `Task<T>`.
- **NEVER use `.Result` or `.Wait()`:** Blocking async calls causes Thread Pool starvation and deadlocks under enterprise loads.
- **Mandatory `CancellationToken` Propagation:** Always accept and pass `CancellationToken ct` down the entire call chain (Controller → UseCase → Repository/Service → SDK call):
  ```csharp
  public async Task<DocumentResultDto> HandleAsync(GenerateDocumentCommand command, CancellationToken ct)
  {
      var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct);
      var stream = await _storageService.GetAsync(template.Path, ct);
      return await _engine.RenderAsync(stream, command.Payload, ct);
  }
  ```

### 2.4 Error Handling & Domain Exceptions
- **Thin Controllers with Zero try-catch:** Controllers MUST NOT contain `try-catch` blocks for business logic. Throw strongly-typed Domain Exceptions from UseCases and let `GlobalExceptionFilter` map them to RFC 7807 Problem Details:
  ```csharp
  // ✅ Throw in UseCase:
  if (slugExists)
      throw new ConflictException($"Slug '{command.Slug}' is already in use.");

  // ✅ In Controller:
  [HttpPost]
  public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, [FromServices] CreateTemplateUseCase useCase, CancellationToken ct)
  {
      var result = await useCase.ExecuteAsync(command, ct);
      return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResponse>(result));
  }
  ```
- **Standard Domain Exceptions Hierarchy** (ทั้งหมดสืบทอด `abstract DomainException` + `ErrorCode`):
  - *Entity / Value Object invariants (โยนจาก Domain Layer):*
    - `DomainValidationException` → HTTP 400 (`DOMAIN_VALIDATION_ERROR`) — input ของ ctor/method ผิด
    - `BusinessRuleViolationException` → HTTP 400 (ErrorCode เฉพาะกฎ, `UPPER_SNAKE_CASE`) — ผิดกฎ/ผิด state transition
  - *Use-case outcomes (โยนจาก Application Layer):*
    - `NotFoundException` → HTTP 404 (`RESOURCE_NOT_FOUND`)
    - `ConflictException` → HTTP 409 (`RESOURCE_CONFLICT`)
    - `DraftExpiredException` → HTTP 410 (`DRAFT_EXPIRED`)
    - `SchemaValidationException` → HTTP 400 (`SCHEMA_VALIDATION_FAILED`)
    - `RenderException` → HTTP 500 (`DOCUMENT_RENDER_FAILED`)
- **Domain Layer ห้ามโยน `ArgumentException` / `InvalidOperationException`** — ใช้ 2 ตัวแรกข้างบน (`DomainValidationException` / `BusinessRuleViolationException`) 100% ครบทุก Entity และ Value Object (รวม `Sha256Hash`, `DataSourceType` และ `TemplateSlug`)

### 2.5 🎯 Strict Action-Centric Use Cases (Clean Architecture Invariant)

1. **Single Responsibility (1 Intent = 1 Use Case):**
   - **Zero God Services:** Never group unrelated CRUD actions in a single class (e.g. ❌ `TemplateManagementUseCase`, ❌ `ApiKeyUseCase` containing both create, list, and validate).
   - Each Use Case represents exactly ONE business intent (e.g. ✅ `CreateTemplateUseCase`, `UpdateTemplateDetailsUseCase`, `ActivateTemplateVersionUseCase`, `GetTemplateByIdUseCase`).
2. **Vertical Slice Folder Layout:**
   - Group Command/Query, Validator, and UseCase together in dedicated action folders:
     ```text
     Modules/{BoundedContext}/{SubModule}/
     ├── Commands/
     │   └── CreateTemplate/
     │       ├── CreateTemplateCommand.cs            # Immutable positional record
     │       ├── CreateTemplateCommandValidator.cs   # FluentValidation rule
     │       └── CreateTemplateUseCase.cs            # IUseCase<CreateTemplateCommand, TemplateResponse>
     └── Queries/
         └── GetTemplateById/
             ├── GetTemplateByIdQuery.cs
             └── GetTemplateByIdUseCase.cs
     ```
3. **Standard Contract & Entry Point:**
   - Every Use Case MUST implement `IUseCase<TRequest, TResponse>` or `IUseCase<TRequest>` with a single entry point:
     ```csharp
     public Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct = default);
     ```
4. **The Golden Use Case Template:**
   ```csharp
   namespace SmkDoc.Application.Modules.{BoundedContext}.{SubModule}.Commands.{Action}{Entity};

   public sealed class {Action}{Entity}UseCase(
       I{Entity}Repository entityRepo,
       IValidator<{Action}{Entity}Command> validator,
       IUnitOfWork uow) : IUseCase<{Action}{Entity}Command, {Entity}ResponseDto>
   {
       public async Task<{Entity}ResponseDto> ExecuteAsync(
           {Action}{Entity}Command command, 
           CancellationToken ct = default)
       {
           // 1. Fail-Fast Input Validation
           var validationResult = await validator.ValidateAsync(command, ct);
           if (!validationResult.IsValid)
           {
               throw new ValidationException(validationResult.ToDictionary());
           }

           // 2. Domain Rule & Invariant Verification
           var existing = await entityRepo.GetBySlugAsync(command.Slug, ct);
           if (existing != null)
           {
               throw new ConflictException($"Entity with slug '{command.Slug}' already exists.");
           }

           // 3. Domain Entity Creation via Rich Constructor
           var entity = new {Entity}(command.Name, command.Slug);

           // 4. Persistence & Atomic Unit of Work
           await entityRepo.AddAsync(entity, ct);
           await uow.CommitAsync(ct);

           // 5. Return Application DTO ONLY (Never leak Domain Entity)
           return new {Entity}ResponseDto(entity.Id, entity.Name, entity.Slug, entity.CreatedAt);
       }
   }
   ```
5. **Collaborator Services for Complex Workflows:**
   - If a Use Case exceeds ~40 lines or requires more than 5 dependencies (e.g. `GenerateDocumentUseCase`), decompose the orchestration into focused Domain Collaborator Services (e.g., `IDocumentDataPreparationService`, `IDocumentVersioningService`, `IDocumentAuditService`).
   - The Use Case remains a high-level conductor; it does not perform low-level mapping, checksum hashing, or multi-step entity mutation directly.
6. **Minimal Dependencies & Domain Interface for I/O (DIP Enforcement):**
   - Constructor injection MUST contain ONLY the dependencies strictly required for that action.
   - Use Cases MUST interact with persistence solely through explicit Domain Interfaces (`ITemplateRepository`, `IUserRepository`, `IUnitOfWork`) defined in `SmkDoc.Domain.Interfaces`.
   - **Zero ORM / EF Core leaks:** `Microsoft.EntityFrameworkCore`, `DbContext`, or `DbSet` MUST NEVER be imported into Use Cases.
7. **Encapsulated Invariants & DTO Boundaries:**
   - Use Case orchestrates; business rules are executed inside Domain Entities (`template.UpdateDetails()`, `template.Activate()`). Never bypass entity constructors with object initializers.
   - Input MUST be an immutable `*Command` or `*Query` record.
   - Output MUST be a safe Application DTO (`*Response` or `*Dto`). Domain Entities MUST NEVER be returned to Presentation Layer.
8. **Controller Primary Constructor Injection & Declarative RBAC:**
   - Controllers MUST inject specific Use Cases directly via **C# 12 Primary Constructor** by default. Use `[FromServices]` ONLY for rarely called, computationally heavy scoped services.
   - Controllers MUST use Declarative Authorization (`[Authorize(Roles = "Admin")]` or policies) rather than imperative in-method checks (`User.RequireAdmin()`).
   - Mutation endpoints that create resources MUST return `201 Created` with `ApiResponse<T>` and a valid `Location` header pointing to the single-resource endpoint.

### 2.6 🎮 Presentation Layer & Controller Standards (`SmkDoc.Api`)

Controllers in `SmkDoc.Api/Controllers` are thin HTTP facades that bridge HTTP requests to Application UseCases. All controllers MUST strictly adhere to the **8 Controller Golden Rules**:

```
                                  HTTP Request
                                       │
                      ┌────────────────┴────────────────┐
                      ▼                                 ▼
             Channel A: M2M                     Channel B: Portal
          Header: X-API-Key                  Header: Bearer <JWT>
         (ApiKeyMiddleware)                 ([Authorize] / RBAC)
                      │                                 │
                      └────────────────┬────────────────┘
                                       ▼
                   ┌─────────────────────────────────────────┐
                   │        ApiController (Thin Facade)      │
                   │  - C# 12 Primary Constructor DI ONLY    │
                   │  - Injects IUseCase<TReq, TRes> Only    │
                   │  - Route: api/v1/[management/]resource  │
                   │  - Validates [ProducesResponseType]     │
                   └───────────────────┬─────────────────────┘
                                       │
                    Executes Command / Query via UseCase
                                       │
                                       ▼
                   ┌─────────────────────────────────────────┐
                   │             Response Formats            │
                   │  • JSON Data: ApiResponse<T> (200 / 201)│
                   │  • Mutations without body: 204 NoContent│
                   │  • Binary/Stream: File / Content (Raw)  │
                   │  • Errors: Handled by ExceptionFilter   │
                   └─────────────────────────────────────────┘
```

#### 1. Thin Orchestrator Only (Zero Business Logic)
- Controllers translate HTTP requests (headers, route parameters, query strings, body) into Application Commands/Queries, pass them to a **single dedicated UseCase**, and translate the result into HTTP responses.
- **NEVER** inject `AppDbContext`, repositories (`IRepository`), domain entities, or domain/infrastructure services (e.g. `ITemplateScannerService`) directly into controllers. Inject only UseCases (`*UseCase`).
- **Parameter Naming in Primary Constructors:** Name injected UseCases using `camelCase` matching their UseCase class name (e.g. `CreateTemplateUseCase createTemplateUseCase`, `ValidateTemplatePayloadUseCase validatePayloadUseCase`, `GenerateDocumentUseCase generateUseCase`). **NEVER** prefix with underscore (`_`) in primary constructors, and **NEVER** use ambiguous names like `service` or `handler`.
- **Clean Usings (No Inline Namespaces):** Always place imports at top of file (e.g. `using SmkDoc.Domain.Exceptions;`). **NEVER** inline fully-qualified namespaces in method bodies or signatures (e.g. ❌ `throw new SmkDoc.Domain.Exceptions.DomainValidationException(...)`).
- **Explicit Command/Query Instantiation (No Inline Nested Objects):** Always instantiate `Command` or `Query` into an explicit local variable (`var command = new ...;` or `var query = new ...;`) before passing to `ExecuteAsync(command, ct)`. **NEVER** instantiate objects inline nested directly inside `ExecuteAsync(new DoSomethingCommand(...), ct)` (❌ AP-036). This ensures clean 3-phase readability (1. Input/Preparation → 2. Execution → 3. Response) and trivial debugging.

#### 2. Strict Route Versioning & Prefixes
- **External / M2M Routes:** MUST use `api/v1/{resource}` (e.g. `api/v1/documents`, `api/v1/templates`).
- **Portal Management Routes:** MUST use `api/v1/management/projects/{projectId:guid}/{resource}` or `api/v1/management/settings/...`.
- **Canonical vs. Legacy Routes:** All new canonical controllers and endpoints MUST be prefixed exclusively with `api/v1/...`. Pre-existing dual routes (`[Route("api/...")]`) exist strictly as temporary legacy fallbacks for backward compatibility with frontend clients. **NEVER** add unversioned routes to new controllers or endpoints.
- **Ban Dual-Routing on Canonical Endpoints:** Never decorate new controllers with both `[Route("api/v1/x")]` and `[Route("api/x")]`. Legacy compatibility routes must be placed in explicit backward-compatibility redirect middleware or deprecated legacy adapters.

#### 3. Canonical Response Envelope Policy
- **JSON Data:** MUST always be wrapped in `ApiResponse<T>(T Data)` or `PagedApiResponse<T>(IEnumerable<T> Data, int Total, int Page, int Limit)`.
- **Binary & Media Streams:** Raw PDF, DOCX, XLSX, or HTML streams (`FileResult`, `ContentResult`) MUST return the direct binary stream with proper MIME headers (`application/pdf`, `Content-Disposition: inline`) and **NEVER** be wrapped in JSON envelopes.
- **Strictly BAN Anonymous Return Objects:** Never return anonymous objects (`return Ok(new { success = true });`, `return Ok(new { id = result.Id });`). Always return strongly-typed DTOs wrapped in `ApiResponse<T>` or `204 NoContent`.

#### 4. Deterministic HTTP Status Codes for Mutations
| Action Type | HTTP Status | Response Payload |
|---|---|---|
| **Resource Creation** | `201 Created` / `CreatedAtAction(...)` | `ApiResponse<TDto>` |
| **Idempotent Update (Data returned)** | `200 OK` | `ApiResponse<TDto>` |
| **Idempotent Update / State Change (No body)** | `204 NoContent` | *None* |
| **Deletion / Revocation** | `204 NoContent` | *None* |
| **Read / Query / RPC Execution** | `200 OK` | `ApiResponse<TResultDto>` |
| **Stateless Stream Render / Preview** | `200 OK` | Stream (`application/pdf`) |

#### 5. Declarative Security & Dual-Channel Authorization
- All Portal endpoints MUST be guarded by declarative attributes:
  - `[Authorize]` at controller level for authenticated users.
  - `[Authorize(Roles = "Admin")]` for administrative actions.
- Machine-to-Machine (M2M) controllers (e.g., `DocumentController`, `TemplateController`) must be clearly documented with XML doc comments specifying authentication channel (`X-API-Key via ApiKeyMiddleware`), and tenant isolation verified from `IExecutionContext`.

#### 6. Zero try-catch & RFC 7807 Error Delegation
- Controllers MUST NOT contain `try-catch` blocks.
- Controllers MUST NOT return ad-hoc error shapes like `BadRequest(new { error = "..." })`.
- All errors must be thrown as strongly-typed `DomainException` or validated by FluentValidation/ModelBinding, allowing `GlobalExceptionFilter` to emit uniform RFC 7807 Problem Details.

#### 7. Complete OpenAPI & Swagger Documentation
- Every endpoint MUST define XML doc `<summary>` explaining the business intent.
- Every endpoint MUST declare explicit `[ProducesResponseType]` for all expected status codes (e.g. 200/201, 204, 400, 401, 403, 404, 409).

#### 8. Canonical Controller Reference Example
```csharp
namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Project user lifecycle management within a tenant project.
/// Auth: Channel B (Bearer JWT) — Admin role required for mutations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/users")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class UserManagementController(
    ListProjectUsersUseCase listUsersUseCase,
    InviteUserUseCase inviteUserUseCase,
    RemoveUserUseCase removeUserUseCase) : ControllerBase
{
    /// <summary>List all users belonging to the specified project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new ListProjectUsersQuery(projectId);
        var users = await listUsersUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<UserResultDto>>(users));
    }

    /// <summary>Invite a new user to the project. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser(
        [FromRoute] Guid projectId,
        [FromBody] InviteUserRequest req,
        CancellationToken ct)
    {
        var command = new InviteUserCommand(projectId, req.Email, req.Password, req.FirstName, req.LastName, req.Role);
        var user = await inviteUserUseCase.ExecuteAsync(command, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<UserResultDto>(user));
    }

    /// <summary>Remove a user from the project. Admin only.</summary>
    [HttpDelete("{userId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        await removeUserUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}
```

---

### 2.7 🧪 Unit Testing Standards & The Golden Archetypes

To ensure absolute consistency, zero test rot, and effortless pattern replication, all unit tests MUST mirror the **3 Golden Archetypes**. These archetypes natively embody all testing requirements (1:1 CQRS folder parity, Single SUT isolation, `TestConstants.BaselineTime`, `*Builder`, `*TestFixture`, Roy Osherove naming, pure `[Theory]` validation, and Aggregate Root invariant testing).

```
┌────────────────────────────────────────────────────────────────────────┐
│                   THE 3x3 CLEAN TESTING FRAMEWORK                      │
├────────────────────┬────────────────────┬──────────────────────────────┤
│  1. 🏗️ STRUCTURE   │   2. ⏳ STATE       │   3. ✍️ CONVENTION           │
│     (จัดวางให้ถูกที่)  │      (ข้อมูลต้องนิ่ง)  │      (เขียนให้อ่านง่าย)         │
├────────────────────┼────────────────────┼──────────────────────────────┤
│ 1.1 Solution Split │ 2.1 Builder &      │ 3.1 Roy Osherove             │
│     (Unit vs Integ)│     Factory SSoT   │     Naming Standard          │
│ 1.2 1:1 CQRS Parity│ 2.2 BaselineTime   │ 3.2 Pure Parameterization    │
│     (Folder mirror)│     (Ban UtcNow)   │     ([Theory] vs loop)       │
│ 1.3 Single SUT     │ 2.3 Semantic       │ 3.3 Modern C# &              │
│     (1 Intent = 1) │     Fixtures (Given) Clean Usings Hygiene       │
└────────────────────┴────────────────────┴──────────────────────────────┘
```

---

#### 🌟 Archetype A: Use Case Test (Single SUT Isolation)
- **Placement:** Mirror Application 1:1 (e.g. `SmkDoc.Tests/Application/Modules/{Context}/{SubModule}/Commands/{Action}/{Action}UseCaseTests.cs`)
- **Key Traits:** Exactly 1 Use Case tested per file, instantiates SUT via `_fixture.Build*UseCase()`, domain data from `*Builder`, deterministic time from `TestConstants.BaselineTime`.

```csharp
namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

/// <summary>
/// 📌 GOLDEN ARCHETYPE: Use Case Test (1 SUT Isolation per File)
/// สะท้อนโครงสร้าง Application 1:1, ใช้ SUT Factory (CreateSut), *TestFixture (Given*), *Builder, และ TestConstants.BaselineTime
/// </summary>
public sealed class CreateTemplateUseCaseTests
{
    private readonly TemplateTestFixture _fixture = new();

    // 🌟 SSoT SUT Factory: จุดเดียวเท่านั้นที่ instantiate SUT
    private CreateTemplateUseCase CreateSut() => _fixture.BuildCreateTemplateUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenValidInput_ReturnsSuccessResult()
    {
        // 1. Arrange: สร้าง Domain Data ผ่าน Builder + BaselineTime เสมอ
        var project = new ProjectBuilder().WithDefaults().Build();
        _fixture.GivenProjectExists(project);

        var command = new CreateTemplateCommand(project.Id, "Invoice", "invoice-01", null);

        // 2. Act: เรียกผ่าน CreateSut() โดยตรง (ตัด temporary variable 'sut' ออก)
        var result = await CreateSut().ExecuteAsync(command);

        // 3. Assert: ตรวจสอบผลลัพธ์และ Side-effects
        result.Should().NotBeNull();
        result.Slug.Should().Be("invoice-01");
        _fixture.VerifyCommitted();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        _fixture.GivenTemplateSlugExists("invoice-01");
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Invoice", "invoice-01", null);

        // Act & Assert: Exception Path ชัดเจนผ่าน Lambda โดยตรง
        var act = () => CreateSut().ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
```

##### 🎯 The Canonical SUT Factory Standard (`CreateSut`)
เพื่อรักษา Clean Code, Readability, และ Simplicity ให้ทุกคลาสทดสอบ UseCase ในระบบเหมือนกัน 100% ให้ปฏิบัติตาม **5 เสาหลัก (The 5 Pillars)**:
1. **SSoT Factory:** ทุกคลาสทดสอบ UseCase ต้องมี `private {UseCase} CreateSut(...)` เมธอดเดียวเท่านั้น **ห้าม** เขียน `new {UseCase}` ภายใน Test Method เด็ดขาด
2. **Standardized Signature & Placement:**
   - วางไว้ใต้ mock fields / fixture (ก่อน test method แรกเสมอ)
   - ใช้ Expression-bodied (`=> new(...)` หรือ `=> _fixture.Build...()`)
   - ตั้งชื่อ `CreateSut` เสมอ (ห้ามใช้ `BuildSut`, `CreateUseCase`, `BuildUseCase`)
3. **Optional Parameter Overrides:** หาก test method ใดต้องการ mock พิเศษ (เช่น validator ล้มเหลว) ให้ส่งผ่าน optional parameter (`CreateSut(validator: customValidator.Object)`) โดย `CreateSut` จะ fallback กลับไปหา default mock หากส่ง `null`
4. **Pure Factory (Zero Side-Effects):** `CreateSut()` ต้องคืน fresh instance เสมอ และปราศจาก mock setup หรือ state mutation ภายใน factory
5. **Unified 3-A Invocation Flow:**
   - **Happy Path:** `var result = await CreateSut().ExecuteAsync(command);` (ไม่มีตัวแปรซ้ำซ้อน `var sut = ...`)
   - **Exception Path:** `var act = () => CreateSut().ExecuteAsync(command); await act.Should().ThrowAsync<...>();`

##### 🎯 The Assertion & Mock Verification Matrix (Precision Testing)
เพื่อยกระดับ Unit Test สู่ระดับ Enterprise-Grade และป้องกัน False Positives รวมถึงหลีกเลี่ยง Brittle Tests:
1. **Deep Semantic Assertions (Not Just Exception Type):**
   - ❌ **Anti-Pattern (Shallow Type Only):** `await act.Should().ThrowAsync<ConflictException>();` (เสี่ยง False Positive เมื่อเกิด Exception ชนิดเดียวกันจากคนละสาเหตุ)
   - ❌ **Anti-Pattern (Brittle Exact Match):** `.WithMessage("Exact long hardcoded string...");` (เปราะบางต่อการแก้ wording/punctuation)
   - ✅ **Best Practice (Semantic Wildcard Match):** ตรวจสอบ Business Identifier หรือ Keyword สำคัญด้วย Wildcard `*`:
     ```csharp
     await act.Should().ThrowAsync<ConflictException>()
         .WithMessage($"*'{command.Slug}'*");
     ```
   - ✅ **Best Practice (Structured Properties):** หากเป็น Exception ที่มี Property เฉพาะ (เช่น `SchemaValidationException`) ให้ assert ที่ property โดยตรง:
     ```csharp
     var ex = await act.Should().ThrowAsync<SchemaValidationException>();
     ex.Which.TemplateSlug.Should().Be("invoice");
     ```
2. **Zero Mock State Pollution:**
   - ใน xUnit ทุก Test Method ถูกสร้างคลาสใหม่เสมอ (`new TestClass()`) ดังนั้น class-level mocks จึงแยกขาดจากกันโดยธรรมชาติ
   - **ห้าม** ตั้งค่า mock พร่ำเพรื่อใน Constructor ให้ mock fields เป็น Blank Mocks เสมอ และทำ Setup เฉพาะสิ่งที่ Test Method นั้นสนใจ
   - **ห้าม** นำ Mock ที่ไม่ได้เป็น Dependency ของ UseCase นั้นเข้ามาใน Test Class (รักษา 1:1 CQRS Parity)
3. **The Golden Side-Effect Verification Matrix:**
   - ใน Clean Architecture "การไม่เกิด Side-effect เมื่อเกิดข้อผิดพลาด" สำคัญเท่ากับ "การเกิด Side-effect เมื่อสำเร็จ":

   | Scenario / Path | Target Method | Expectation | Rationale |
   |---|---|:---:|---|
   | **Happy Path (Success)** | Mutation Repo (`Add`, `Update`, `Remove`) | `Times.Once()` | ยืนยันการเปลี่ยนแปลง State |
   | **Happy Path (Success)** | `IUnitOfWork.CommitAsync` | `Times.Once()` | ยืนยันการ Commit Transaction |
   | **Guard/Exception Path** | Mutation Repo (`Add`, `Update`, `Remove`) | `Times.Never()` | ป้องกัน Data Mutation ขณะเกิดข้อผิดพลาด |
   | **Guard/Exception Path** | `IUnitOfWork.CommitAsync` | `Times.Never()` | ป้องกัน Data Corruption เด็ดขาด |
   | **Guard/Exception Path** | Downstream I/O (`IStorageService`, `IRenderEngine`) | `Times.Never()` | ป้องกัน Resource Leak / Side-effect |

##### 🎯 Test Data Management, Deterministic Clock (`FakeTimeProvider`), & Role-Based Testing
เพื่อป้องกัน Test Cascade Breakages, ขจัดปัญหา Non-deterministic Flakiness, และรักษาความกระชับของ Test Suite:
1. **Mandatory Builders & Factories (Zero Ad-hoc Entity Instantiation):**
   - ใน `SmkDoc.Tests/Application/` **ห้าม** เรียก `new Entity(...)` หรือ `Entity.Create(...)` แบบ Hardcode เองเด็ดขาด
   - **`*Builder` (สำหรับ Aggregate Roots ซับซ้อน):** ใช้ `new ProjectBuilder().WithSlug("...").Build()` เมื่อต้องการ override ค่าเฉพาะบางตัว โดยที่ค่าอื่นๆ เป็น default baseline ที่สมบูรณ์
   - **`*TestFactory` (สำหรับ Child Entities หรือ POCO ง่ายๆ):** ใช้ `DataConnectionTestFactory.Create(...)` เมื่อต้องการสร้าง entity ที่มี parameter พื้นฐานครบในบรรทัดเดียว
2. **Deterministic Time with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`):**
   - **ห้าม** เรียก `DateTimeOffset.UtcNow` ใน Unit Tests เด็ดขาด (ใช้ `TestConstants.BaselineTime` เสมอ)
   - สำหรับ UseCase ที่รับ `TimeProvider? timeProvider = null` ให้ `CreateSut` inject `timeProvider ?? TestConstants.CreateFakeClock()` เพื่อรับประกันว่า timestamp ทุกตัวที่ UseCase สร้างจะตรงกับ BaselineTime 100%
   - ทดสอบ TTL / Expiry ด้วย `fakeClock.Advance(TimeSpan.FromHours(24))` แทนการ sleep จริง
3. **Role-Based Testing: `[Fact]` vs `[Theory]` (Pragmatic Selection):**
   - **`[Fact]` = "One Unique Behavior / Complex Story":** ใช้สำหรับ UseCase Workflows, State Transitions, Happy Paths หลัก, หรือเคสที่มี Arrange และ Mock Setup เฉพาะเจาะจง (ห้ามฝืนทำเป็น Theory จนต้องมี if-else ในเทสต์)
   - **`[Theory]` + `[InlineData]` = "One Rule, Multiple Inputs":** ใช้สำหรับ Input Validators (`*ValidatorTests`), Converters, Formatters, และ Boundary Value Analysis (Null, Empty, Whitespace, Min/Max Length) เพื่อเห็นตารางความถูกต้องในจุดเดียวและลดโค้ดซ้ำซ้อน

---

#### 🌟 Archetype B: Input Validator Test (Pure Parameterized Testing)
- **Placement:** Colocated alongside Command/Query in the same folder (`*ValidatorTests.cs`).
- **Key Traits:** Pure function testing without mocks, exhaustive constraint verification using `[Theory]` + `[InlineData]`.

```csharp
namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

/// <summary>
/// 📌 GOLDEN ARCHETYPE: Input Validator Test (Pure Function Parameterized Testing)
/// วางประกบคู่กับ Command ในโฟลเดอร์เดียวกัน, ทดสอบ constraints ด้วย [Theory], ปราศจาก mock 100%
/// </summary>
public sealed class CreateTemplateCommandValidatorTests
{
    private readonly CreateTemplateCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenNameIsInvalid_HasValidationError(string? invalidName)
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), invalidName!, "valid-slug", null);

        var result = _sut.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTemplateCommand.Name));
    }
}
```

---

#### 🌟 Archetype C: Domain Aggregate Root Test (Business Invariants & Mutations)
- **Placement:** `SmkDoc.Tests/Domain/Entities/{Aggregate}Tests.cs` (e.g. `TemplateTests.cs`, `UserTests.cs`).
- **Key Traits:** All invariants, encapsulation, and state mutations tested directly inside the aggregate test file. Strictly ban separate generic dumping grounds (`DomainInvariantTests`).

```csharp
namespace SmkDoc.Tests.Domain.Entities;

/// <summary>
/// 📌 GOLDEN ARCHETYPE: Aggregate Root Invariant Test
/// รวมการทดสอบกฎธุรกิจและการกลายสภาพ (Mutation) ไว้ที่ Entity โดยตรง ห้ามแยกไฟล์ dumping ground
/// </summary>
public sealed class TemplateTests
{
    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsBusinessRuleViolationException()
    {
        // Arrange: ใช้ BaselineTime จาก Builder
        var template = new TemplateBuilder().AsActive().Build();

        // Act & Assert: ทุก mutation ต้องส่ง BaselineTime
        var act = () => template.Activate(TestConstants.BaselineTime);
        act.Should().Throw<BusinessRuleViolationException>();
    }
}
```

---

## 3. 🔶 Frontend Standards: TypeScript / Next.js 15 (`frontend-v2/`)

### 3.1 Strict TypeScript & Zod-First Validation
- **Zero `any` Policy:** The `any` type is strictly forbidden. Use `unknown` with type narrowing if the type is truly dynamic.
- **Zod as Single Source of Truth:**
  - Define schemas in `src/schemas/` (e.g., `template.schema.ts`, `document.schema.ts`).
  - Derive TypeScript types via `z.infer<typeof ...>` and re-export them from `src/types/api.ts`.
  - Always validate form inputs and external API responses through Zod schemas.

### 3.2 Data Fetching & API Client
- **Route all HTTP calls through `apiClient`:** Never use raw `fetch()` or `axios`.
  - Standard JSON: `apiClient<T>(endpoint, options)`
  - Binary/PDF Downloads: `apiClientBlob(endpoint, options)`
  - Automatic Auth: `apiClient` automatically injects the active `X-API-Key` or Bearer JWT token from storage.

### 3.3 Component Architecture & Design Tokens
- **Composable Primitives:** Always check `src/components/ui/` (Button, Input, Modal, Table, Badge, CardBlock) before creating new UI elements.
- **HyperUI Design Patterns:** When building new complex views (Stats, Filter Bars, Data Tables), reference [HyperUI](https://www.hyperui.dev) structures.
- **Design Tokens Discipline:** Strictly use tailwind token classes (`bg-surface`, `text-textPrimary`, `text-textSecondary`, `border-border`, `text-primary`, `rounded-sm` / 2–4px radius, Ice-White theme palette). Never introduce arbitrary hardcoded hex codes.

---

## 4. 🏷️ System-Wide Naming Standards & Matrix (The 6 Pillars)

To ensure universal consistency, clean readability, and seamless LLM context adherence, all code MUST strictly follow the 6 Naming Pillars:

### 4.1 The 6 Pillars Breakdown

1. **Flow & Contract Suffixes (Presentation vs. Application Separation):**
   - **HTTP Requests (`SmkDoc.Api/Contracts/{BoundedContext}/`):** Suffix with `*Request` (e.g., `CreateTemplateRequest`, `InviteUserRequest`). Positional record representing external JSON payload.
   - **Application Commands (`SmkDoc.Application/Modules/`):** Suffix with `*Command` (e.g., `CreateTemplateCommand`, `GenerateDocumentCommand`). Represents an internal state mutation request.
   - **Application Queries (`SmkDoc.Application/Modules/`):** Suffix with `*Query` (e.g., `GetTemplateByIdQuery`, `ListTemplatesQuery`). Represents an internal data read/filter request.
   - **Application Outputs:** Suffix with `*Response` or `*ResultDto` (e.g., `TemplateResponse`, `UserResultDto`).
   - **API Presentation Response:** Always wrapped in `ApiResponse<T>` or `PagedApiResponse<T>`. Never return raw domain entities or ad-hoc anonymous objects (`new { id }`).

2. **Domain Ubiquitous Language & Rich Modeling (`SmkDoc.Domain`):**
   - **Entities:** Singular `PascalCase` nouns (e.g., `Template`, `DocumentVersion`, `GenerationLog`, `User`).
   - **Value Objects:** Semantic `PascalCase` nouns describing specific domain concepts (e.g., `TemplateName`, `TemplateSlug`, `Sha256Hash`, `EmailAddress`).
   - **Canonical Factory Methods (SSoT):** Canonical verb (`Create`, `Register`, `Draft`, `Issue` or specialized `CreateSuccess`/`CreateFailure`).
   - **Business Mutation Methods:** Domain intention verbs in `PascalCase` (`Activate`, `Deactivate`, `Publish`, `Archive`, `AssignRole`, `Revoke`, `SetUpdated`). **NEVER** use generic JavaBean-style setters (`SetCategory`, `SetName`).

3. **Primary Constructor Parameter Naming (Universal C#):**
   - MUST be `camelCase` mirroring the dependency class or interface name directly (e.g., `templateRepo`, `unitOfWork`, `createTemplateUseCase`, `logger`).
   - **Strictly BAN Underscore (`_`) prefix:** Parameters in primary constructors are NOT private fields (❌ `_templateRepo`).
   - **Strictly BAN Generic names:** Never use `repo`, `service`, `helper`, or `handler` without descriptive context.

4. **Unit & Integration Test Standards (The Golden Archetypes):**
   - **Pure In-Memory (Zero I/O):** `SmkDoc.Tests` MUST be 100% in-memory unit tests (Zero Disk/Network/DB I/O). Benchmarks, generators, and container fixtures belong in `SmkDoc.IntegrationTests`.
   - **1:1 CQRS Single SUT:** Exactly 1 Use Case tested per class file, mirroring Application 1:1 under `Commands/{Action}/` or `Queries/{Action}/` (Strictly BAN monolithic test classes).
   - **Deterministic SSoT:** Always instantiate domain data via `*Builder` / `*TestFactory` with `TestConstants.BaselineTime` (Strictly BAN `DateTimeOffset.UtcNow` inside unit tests).
   - **Mirror the 3 Golden Blueprints:** Refer to [§2.7](#27--unit-testing-standards--the-golden-archetypes) for Archetype A (UseCase SUT), Archetype B (Pure Validator `[Theory]`), and Archetype C (Domain Aggregate Root).

5. **Frontend File & Component Standards (`frontend-v2/`):**
   - **React Components:** `PascalCase.tsx` (e.g., `TemplateCard.tsx`, `AppShell.tsx`, `StudioWorkspace.tsx`).
   - **Custom Hooks:** `camelCase.ts` prefixed with `use` (e.g., `useTemplates.ts`, `useDebounce.ts`).
   - **Zod Schemas:** `camelCase` + `Schema` inside `*.schema.ts` (e.g., `templateSchema`, `createTemplateSchema` in `template.schema.ts`).
   - **API Modules:** `*.api.ts` (e.g., `templates.api.ts`, `documents.api.ts`).
   - **Type Definitions:** `src/types/api.ts` re-exported from Zod schemas (never import schemas directly in UI components).

6. **Database Persistence Standards (PostgreSQL):**
   - **Table Names:** `plural_snake_case` (e.g., `templates`, `template_versions`, `generation_logs`, `user_project_roles`).
   - **Primary Key:** `id` (UUIDv7 string or uuid).
   - **Foreign Keys:** `{singular_entity}_id` (e.g., `project_id`, `template_id`, `company_id`).
   - **Timestamp Columns:** `created_at`, `updated_at`, `revoked_at`, `generated_at`.
   - **Boolean Columns:** `is_*` (e.g., `is_active`, `is_success`, `is_system`).

---

### 4.2 Comprehensive Naming Conventions Matrix

| Element / Artifact | Layer / Scope | Convention | Example |
|---|---|---|---|
| **C# Domain Entities** | Domain | Singular `PascalCase` | `Template`, `DocumentVersion`, `User` |
| **C# Value Objects** | Domain | Semantic `PascalCase` | `TemplateName`, `TemplateSlug`, `Sha256Hash` |
| **C# Smart Enums** | Domain | `PascalCase` (Class & Items) | `TemplateFormat.Html`, `RoleType.Admin` |
| **C# Domain Exceptions** | Domain | `*Exception` | `NotFoundException`, `ConflictException` |
| **C# Canonical Factory** | Domain | Canonical Verb | `Create()`, `Register()`, `Draft()` |
| **C# Business Mutations** | Domain | Domain Verb | `Publish()`, `Archive()`, `AssignRole()` |
| **C# Primary Ctor Param** | All C# | `camelCase` (no `_`) | `templateRepo`, `createTemplateUseCase` |
| **C# Mutation Inputs** | Application | `*Command` | `CreateTemplateCommand`, `PublishVersionCommand` |
| **C# Query Inputs** | Application | `*Query` | `GetTemplateByIdQuery`, `ListTemplatesQuery` |
| **C# Output DTOs** | Application | `*Response` / `*ResultDto` | `TemplateResponse`, `UserResultDto` |
| **C# HTTP Requests** | Presentation | `*Request` | `CreateTemplateRequest`, `InviteUserRequest` |
| **C# Controllers** | Presentation | `*Controller` (Bounded Context) | `TemplateController`, `UserManagementController` |
| **C# Test Classes** | Tests | `*Tests` | `CreateTemplateUseCaseTests`, `TemplateTests` |
| **C# Test Methods** | Tests | `Method_Scenario_Result` | `Create_WhenSlugEmpty_ThrowsValidationException` |
| **C# Test Factories** | Tests | `*TestFactory` | `TemplateTestFactory`, `DocumentTestFactory` |
| **C# Test Builders** | Tests | `*Builder` | `TemplateBuilder`, `UserBuilder` |
| **C# Test Fixtures** | Tests | `*TestFixture` | `GenerateDocumentTestFixture` |
| **TypeScript Types** | Frontend | `PascalCase` | `TemplateItem`, `UserSession` |
| **TypeScript Zod Schemas** | Frontend | `camelCase` + `Schema` | `createTemplateSchema`, `loginRequestSchema` |
| **React Components** | Frontend | `PascalCase.tsx` | `TemplateCard.tsx`, `AppShell.tsx` |
| **React Custom Hooks** | Frontend | `use` + `PascalCase.ts` | `useTemplates.ts`, `useDebounce.ts` |
| **REST Route URLs** | Presentation | `kebab-case` / lower plural | `/api/v1/management/projects/{id}/templates` |
| **Database Tables** | Persistence | `plural_snake_case` | `templates`, `template_versions` |
| **Database Columns** | Persistence | `snake_case` / `is_*` | `project_id`, `is_active`, `created_at` |

---

## 5. 🧼 Code Review & Pre-Commit Quality Gate

Before submitting any code changes, verify:
- [ ] **No Warnings:** Zero compiler warnings (`dotnet build` and `npx tsc --noEmit` pass with 0 errors).
- [ ] **Deterministic Async:** All I/O operations have `await` and receive `CancellationToken`.
- [ ] **No Leaked Entities:** Domain entities remain within Domain and Application; never exposed via Controller responses.
- [ ] **Resource Disposal:** All OpenXml, ClosedXML, and MemoryStream instances are enclosed in `using` declarations.
- [ ] **Clean Git Diff:** No debug logs (`Console.WriteLine`, `console.log`), unused usings, or commented-out scratch code.
