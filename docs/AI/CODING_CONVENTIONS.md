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

### 2.7 🧪 Unit Testing Standards & Anti-Bloat Patterns

To keep test suites maintainable, fast, and resilient to refactoring:

1. **Test Fixture Pattern (SUT Isolation):**
   - Group related Use Case mocks and SUT instantiation inside dedicated Fixture classes under `SmkDoc.Tests/Common/Fixtures/` (e.g., `UserManagementTestFixture`, `GenerateDocumentTestFixture`).
   - Use Case tests MUST instantiate SUTs via `_fixture.Build*UseCase()`. When new dependencies are added to Use Cases, update only the Fixture—never ripple breakages across dozens of test files.

2. **Domain Builders (Object Mother Pattern):**
   - Entities MUST be instantiated using fluent test builders under `SmkDoc.Tests/Common/Builders/` (e.g., `UserBuilder`, `UserProjectRoleBuilder`, `TemplateBuilder`).
   - Test methods specify only properties relevant to the specific scenario (e.g., `new UserProjectRoleBuilder().AsAdmin().Build()`).

3. **Separation of Validator vs. Business Flow:**
   - **Validator Tests (`*ValidatorTests`):** Test input constraints (empty GUIDs, invalid roles, email formats) independently using `[Theory]` and `[InlineData]`. No mocks required; ultra-fast execution.
   - **Use Case Tests (`*UseCaseTests`):** Test purely core business flow, conflict scenarios, and state mutations (keep to 3–5 high-value scenarios per Use Case).

4. **Outcome Verification over Implementation Coupling:**
   - Verify only state changes and critical side effects (`CommitAsync()`, `Remove()`, domain events).
   - Avoid asserting internal read/query methods (`GetAsync(...)`) with `Times.Once` unless the orchestration sequence itself is a strict business requirement.

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

## 4. 🏷️ Naming Conventions Matrix

| Element / Artifact | Convention | Example |
|---|---|---|
| **C# Classes & Records** | `PascalCase` | `GenerateDocumentUseCase`, `TemplateDto` |
| **C# Interfaces** | `I` + `PascalCase` | `IRepository<T>`, `IPdfRenderer` |
| **C# Methods & Properties** | `PascalCase` | `GetByIdAsync()`, `CurrentVersionId` |
| **C# Method Parameters** | `camelCase` | `(Guid templateId, CancellationToken ct)` |
| **C# Private Fields** | `_` + `camelCase` | `private readonly IStorageService _storageService;` |
| **C# Mutation Inputs** | `*Command` | `CreateTemplateCommand`, `UpdateRoleCommand` |
| **C# Query Inputs** | `*Query` | `PreviewDocumentQuery`, `GetLogsQuery` |
| **C# Output DTOs** | `*Dto` / `*ResultDto` | `TemplateDto`, `ValidatedApiKeyDto` |
| **C# Domain Exceptions** | `*Exception` | `NotFoundException`, `RenderException` |
| **C# Smart Enums** | `PascalCase` (class & items) | `TemplateFormat.Html`, `RoleType.Admin` |
| **TypeScript Interfaces/Types** | `PascalCase` | `TemplateItem`, `UserSession` |
| **TypeScript Zod Schemas** | `camelCase` + `Schema` | `createTemplateSchema`, `loginRequestSchema` |
| **React Components** | `PascalCase` | `TemplateCard.tsx`, `AppShell.tsx` |
| **React Custom Hooks** | `use` + `PascalCase` | `useTemplates()`, `useDebounce()` |
| **REST Route URL Segments** | `kebab-case` or lowercase plural | `/api/data-connections`, `/api/templates` |
| **Database Tables & Columns** | `snake_case` (PostgreSQL) | `template_versions`, `created_at`, `file_format` |

---

## 5. 🧼 Code Review & Pre-Commit Quality Gate

Before submitting any code changes, verify:
- [ ] **No Warnings:** Zero compiler warnings (`dotnet build` and `npx tsc --noEmit` pass with 0 errors).
- [ ] **Deterministic Async:** All I/O operations have `await` and receive `CancellationToken`.
- [ ] **No Leaked Entities:** Domain entities remain within Domain and Application; never exposed via Controller responses.
- [ ] **Resource Disposal:** All OpenXml, ClosedXML, and MemoryStream instances are enclosed in `using` declarations.
- [ ] **Clean Git Diff:** No debug logs (`Console.WriteLine`, `console.log`), unused usings, or commented-out scratch code.
