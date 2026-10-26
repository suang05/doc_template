# [PATTERN-002]: 02_application_layer.md — Application Layer Design Patterns & PEMR Pipeline

<context>
Scope: Application Layer SSoT, CQRS Slices, Streaming Pipelines, Validation & Unit of Work
Target: `SmkDoc.Application/` (`Modules/{BoundedContext}/{Aggregate}/{Commands|Queries|DTOs}/`)
Authority: Absolute SSoT Invariant for Application Orchestration
Invariants: 100% IUseCase<TReq, TRes>, Zero-LOH Stream Pipeline, Fail-Fast Line 1 Validation
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical ASCII Symbols
TriggerSignals: [SIGNAL_API_MUTATION], [SIGNAL_STRUCTURE_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Architectural Wiring & Execution Contracts

### Layer Boundary Wiring & Contracts
| Direction | Upstream Origin | Current Role (Application Orchestrator) | Downstream Destination |
|---|---|---|---|
| **Inbound Request** | Presentation Controller | Validates request line 1 via `IValidator<T>` | Application UseCase (`IUseCase<TReq, TRes>`) |
| **Precondition** | Command Parameters | Checks uniqueness / existence via Domain Port | Repository (`ITemplateRepository.SlugExistsAsync`) |
| **Domain Mutation** | Validated Command | Invokes Entity Factory or Expressive Verb | Domain Aggregate (`Template.Create(...)`) |
| **Persistence** | Mutated Aggregate | Persists changes and commits single transaction | Unit of Work (`IUnitOfWork.CommitAsync`) |
| **Outbound Egress** | Mutated State | Maps directly to immutable DTO or Stream | Presentation Layer (`ApiResponse<T>` / `Stream`) |

### Inline Execution Pipeline
`[Presentation Request]` => Line-1 Fail-Fast Validation => Precondition Port Check => Domain Mutation => UnitOfWork Commit => `[Application DTO / Stream]`

### Engineering Rationale & Causal Decisions
* **Why Action-Centric `IUseCase` over MediatR:** Eliminates runtime reflection allocations, avoids opaque pipeline behaviors, enables transparent call stacks, and enforces 100% compile-time verified DI.
* **Why Positional `sealed record` for Commands/DTOs:** Guarantees immutability across layer boundaries and provides zero-boilerplate structural equality and JSON serialization.
* **Why Fail-Fast Validation at Line 1:** Prevents corrupted requests from touching persistence or executing expensive queries, throwing strongly-typed `ValidationException` immediately.
* **Why Application DTOs as Boundary Firewalls:** Domain entities must never cross the layer boundary into Presentation; prevents leaking internal domain state or lazy-loading EF Core traps.
* **Why Direct Stream Return for Render/Preview:** Guarantees zero Large Object Heap (LOH) RAM allocation (<85KB Gen 2 threshold) during high-throughput document streaming.

### Cognitive Optimization & Extension Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: High-Throughput Stateless Document Gateway (Target: >= 500 req/sec)
  TradeOffPriority: MemorySafety (Zero-LOH Stream) > Testability (FakeTime) > DeveloperSimplicity > RawSpeed

ProactiveChallengeHeuristics:
  - Trigger: Detection of in-memory byte[] buffering or MemoryStream.ToArray()
    Action: Challenge and refactor to Stream pass-through >> Ref: [TRAP-APP-002]
  - Trigger: Detection of MediatR IRequestHandler indirection
    Action: Challenge and refactor to direct IUseCase<TReq, TRes> with explicit DI
  - Trigger: Detection of Domain Entity leaking outside Application boundary
    Action: Challenge and map to sealed record Application DTO >> Ref: [TRAP-APP-001]

CoreExtensionSlots:
  RenderingEngines: Pluggable IRenderEngine strategy dispatch (Html, Docx, Excel)
  ValidationPipeline: Automatic IValidator<TCommand> fail-fast execution at line 1
  QueryProjections: Materialized PagedResultDto<T> with boundary clamping
```


### Deterministic State Machine Contract
| Step | Phase Name | Execution Contract | Failure Mechanics |
|---|---|---|---|
| **1** | **Parse & Validate** | `await validator.ValidateAsync(req, ct)` | Throws `ValidationException` (HTTP 400) |
| **2** | **Precondition Query**| Check aggregate state or slug uniqueness | Throws `ConflictException` (409) or `NotFoundException` (404) |
| **3** | **Domain Mutation** | Invoke Domain Factory or Aggregate business verb | Throws `DomainValidationException` (422) |
| **4** | **Commit & Egress** | `await unitOfWork.CommitAsync(ct)` & Map DTO | Atomic rollback via DbContext on exception |

---

## 2. Level 1: Application Invariants (Positive Declarative AST)

>> POSITIVE ARCHITECTURAL INVARIANTS:
1. `ClassTopology`: Exactly one public execution method per class: `Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)`.
2. `InterfaceContract`: Every use case MUST implement `IUseCase<TRequest, TResponse>` or `IUseCase<TRequest>`.
3. `RequestImmutability`: All Commands and Queries MUST be `sealed record` with positional primary constructor parameters.
4. `BoundaryFirewall`: Prohibit returning Domain Entities (`AggregateRoot`, `Entity`, `ValueObject`). Return immutable Application DTO records or `Stream`.
5. `TemporalResolution`: System time queries MUST consume `TimeProvider.GetUtcNow()`. Prohibit ambient clock queries (`DateTime.UtcNow`).
6. `TransactionIsolation`: Mutating use cases commit strictly via `IUnitOfWork.CommitAsync(ct)` at egress. Read queries never mutate state.
7. `StreamingIntegrity`: Render/Preview operations MUST return live `Stream`. Prohibit buffering to `byte[]` or unbounded `MemoryStream`.

---

## 3. Level 2: Core Interfaces & Exception Mapping Matrix

### Primary SSoT Interfaces
```csharp
namespace SmkDoc.Application.Common.Interfaces;

public interface IUseCase<in TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct = default);
}

public interface IUseCase<in TRequest>
{
    Task ExecuteAsync(TRequest request, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
```

### ProblemDetails RFC 9457 HTTP Status Mapping
| Exception Class | HTTP Status | Causal Rationale |
|---|---|---|
| `ValidationException` | `400 Bad Request` | Fail-fast payload schema or format failure |
| `ForbiddenException` | `403 Forbidden` | Tenant boundary cross or insufficient scope |
| `NotFoundException` | `404 Not Found` | Target aggregate ID does not exist in tenant partition |
| `ConflictException` | `409 Conflict` | Unique business key collision (Slug, Code, Concurrent edit) |
| `DomainValidationException` | `422 Unprocessable`| Aggregate business invariant violated |
| `DocumentRenderException` | `502 Bad Gateway` | Gotenberg rendering microservice pipeline failure |

---

## 4. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### Archetype 1: Mutating Command Slice (`CreateTemplate`)
>> CANONICAL RECIPE [RULE-APP-CMD-001]:
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Repositories;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

// 1. Positional Immutable Command
public sealed record CreateTemplateCommand(
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category);

// 2. Strict Line-1 Validator
public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("ProjectId is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9-]+$");
    }
}

// 3. Response DTO Record
public sealed record TemplateResultDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    DateTimeOffset CreatedAt);

// 4. Single-Responsibility UseCase Orchestrator
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateTemplateCommand> validator,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Parse & Validate (Fail-Fast)
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.ToDictionary());

        // 2. Execute Precondition Query
        var slugExists = await templateRepo.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
            throw ConflictException.DuplicateSlug(command.Slug);

        // 3. Mutate Domain State
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            new ProjectId(command.ProjectId),
            TemplateName.Create(command.Name),
            new TemplateSlug(command.Slug),
            command.Category,
            now);

        await templateRepo.AddAsync(template, ct);

        // 4. Commit & Return Egress DTO
        await unitOfWork.CommitAsync(ct);

        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CreatedAt);
    }
}
```

### Archetype 2: Zero-LOH Streaming Query Slice (`RenderDocument`)
>> CANONICAL RECIPE [RULE-APP-STRM-002]:
```csharp
namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Repositories;
using SmkDoc.Domain.ValueObjects.Ids;

// 1. Immutable Query Contract
public sealed record RenderDocumentQuery(
    Guid ProjectId,
    Guid TemplateId,
    string PayloadJson);

// 2. Query Validator
public sealed class RenderDocumentQueryValidator : AbstractValidator<RenderDocumentQuery>
{
    public RenderDocumentQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}

// 3. Streaming UseCase (Zero byte[] allocation, direct Stream return)
public sealed class RenderDocumentUseCase(
    ITemplateRepository templateRepo,
    IDocumentRenderEngine renderEngine,
    IValidator<RenderDocumentQuery> validator) : IUseCase<RenderDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(RenderDocumentQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.ToDictionary());

        var template = await templateRepo.GetByIdAsync(new TemplateId(query.TemplateId), ct);
        if (template is null || template.ProjectId.Value != query.ProjectId)
            throw new NotFoundException($"Template '{query.TemplateId}' not found for project.");

        // Returns live Gotenberg response stream directly (Zero RAM buffering)
        return await renderEngine.RenderHtmlToPdfStreamAsync(template.HtmlContent, query.PayloadJson, ct);
    }
}
```

### Archetype 3: Materialized Paged Query Slice (`ListTemplates`)
>> CANONICAL RECIPE [RULE-APP-QRY-003]:
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Repositories;
using SmkDoc.Domain.ValueObjects.Ids;

public sealed record ListTemplatesQuery(Guid ProjectId, int Page = 1, int PageSize = 20);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class ListTemplatesUseCase(
    ITemplateRepository templateRepo) : IUseCase<ListTemplatesQuery, PagedResultDto<TemplateResultDto>>
{
    public async Task<PagedResultDto<TemplateResultDto>> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await templateRepo.CountAsync(new ProjectId(query.ProjectId), ct);
        var items = await templateRepo.ListAsync(new ProjectId(query.ProjectId), page, pageSize, ct);

        var dtos = items.Select(t => new TemplateResultDto(
            t.Id.Value,
            t.ProjectId.Value,
            t.Name.Value,
            t.Slug.Value,
            t.Category,
            t.IsActive,
            t.CreatedAt)).ToList();

        return new PagedResultDto<TemplateResultDto>(dtos, totalCount, page, pageSize);
    }
}
```

---

## 5. Level 4: Condensed Traps & High-Salience Micro-Diffs

### [TRAP-APP-001]: Entity Leakage vs Application DTO
```diff
- public async Task<Template> ExecuteAsync(GetTemplateQuery q, CancellationToken ct)
+ public async Task<TemplateResultDto> ExecuteAsync(GetTemplateQuery q, CancellationToken ct)
```
*Rationale: Domain entities leak navigation proxies, internal business states, and EF Core change tracking state.*

### [TRAP-APP-002]: LOH In-Memory Buffering vs Streaming
```diff
- using var ms = new MemoryStream();
- await sourceStream.CopyToAsync(ms, ct);
- return ms.ToArray(); // FATAL: LOH byte[] allocation!
+ return await renderEngine.RenderToStreamAsync(template, ct); // >> CANONICAL: Pure Stream pass-through
```
*Rationale: Buffering binary payloads $\ge 85\text{KB}$ degrades GC Gen 2 and leads to OutOfMemory under concurrent load.*

### [TRAP-APP-003]: Direct Clock Query vs TimeProvider
```diff
- var now = DateTime.UtcNow; // FATAL: Ambient system clock query
+ var now = timeProvider.GetUtcNow(); // >> CANONICAL: Deterministic clock for testing
```
*Rationale: Ambient clock prevents deterministic test harness assertion via `FakeTimeProvider`.*
