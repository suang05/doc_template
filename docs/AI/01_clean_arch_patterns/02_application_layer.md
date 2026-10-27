# [PATTERN-002]: 02_application_layer.md — Application Layer Design Patterns & PEMR Pipeline

<context>
Scope: Application Layer SSoT, CQRS Slices, Streaming Pipelines, Validation, Unit of Work, Domain Event Dispatching & Caching
Target: `SmkDoc.Application/` (`Modules/{BoundedContext}/{Aggregate}/{Commands|Queries|DTOs}/`, `Common/`)
Authority: Absolute SSoT Invariant for Application Orchestration
Invariants: 100% IUseCase<TReq, TRes>, Zero-LOH Stream Pipeline, Fail-Fast Line 1 Validation, Post-Commit Event Dispatch
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
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
| **Side-Effects** | Aggregate `DomainEvents` | Dispatches post-commit via `IDomainEventDispatcher` | Outbox / Event Handlers (Audit, Notifications) |

### Inline Execution Pipeline
`[Presentation Request]` => Line-1 Fail-Fast Validation => Precondition Port Check => Domain Mutation => UnitOfWork Commit => Post-Commit Event Dispatch => `[Application DTO / Stream]`

### Engineering Rationale & Causal Decisions
* **Why Action-Centric `IUseCase` over MediatR:** Eliminates runtime reflection allocations, avoids opaque pipeline behaviors, enables transparent call stacks, and enforces 100% compile-time verified DI.
* **Why Positional `sealed record` for Commands/DTOs:** Guarantees immutability across layer boundaries and provides zero-boilerplate structural equality and JSON serialization.
* **Why Fail-Fast Validation at Line 1:** Prevents corrupted requests from touching persistence or executing expensive queries, throwing strongly-typed `ValidationException` immediately.
* **Why Application DTOs as Boundary Firewalls:** Domain entities must never cross the layer boundary into Presentation; prevents leaking internal domain state or lazy-loading EF Core traps.
* **Why Direct Stream Return for Render/Preview:** Guarantees zero Large Object Heap (LOH) RAM allocation (<85KB Gen 2 threshold) during high-throughput document streaming.
* **Why Post-Commit Domain Event Dispatch:** Dispatches domain side-effects (e.g. audit logging, cache invalidation) strictly after `unitOfWork.CommitAsync(ct)` succeeds, preventing ghost side-effects on transaction rollback.

### Cognitive Optimization & Extension Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: High-Throughput Stateless Document Gateway (Target: >= 500 req/sec)
  TradeOffPriority: MemorySafety (Zero-LOH Stream) > Testability (FakeTime) > DeveloperSimplicity > RawSpeed

ProactiveChallengeHeuristics:
  - Trigger: Detection of in-memory byte[] buffering or MemoryStream.ToArray()
    Action: Challenge and refactor to Stream pass-through >> Ref: [TRAP-APP-004]
  - Trigger: Detection of MediatR IRequestHandler indirection
    Action: Challenge and refactor to direct IUseCase<TReq, TRes> with explicit DI >> Ref: [TRAP-APP-002]
  - Trigger: Detection of Domain Entity leaking outside Application boundary
    Action: Challenge and map to sealed record Application DTO >> Ref: [TRAP-APP-001]
  - Trigger: Missing unitOfWork.CommitAsync() before returning in mutating UseCase
    Action: Challenge immediately; uncommitted mutations cause silent data loss >> Ref: [TRAP-APP-003]

CoreExtensionSlots:
  RenderingEngines: Pluggable IRenderEngine strategy dispatch (Html, Docx, Excel)
  ValidationPipeline: Automatic IValidator<TCommand> fail-fast execution at line 1
  QueryProjections: Materialized PagedResultDto<T> with boundary clamping
  CompiledTemplateCache: ICompiledTemplateCache (In-memory compiled AST cache)
  DomainEventDispatcher: IDomainEventDispatcher (Post-commit event broadcaster)
```

### Deterministic State Machine Contract
| Step | Phase Name | Execution Contract | Failure Mechanics |
|---|---|---|---|
| **1** | **Parse & Validate** | `await validator.ValidateAsync(req, ct)` | Throws `ValidationException` (HTTP 400) |
| **2** | **Precondition Query**| Check aggregate state or slug uniqueness | Throws `ConflictException` (409) or `NotFoundException` (404) |
| **3** | **Domain Mutation** | Invoke Domain Factory or Aggregate business verb | Throws `DomainValidationException` (422) |
| **4** | **Commit & Egress** | `await unitOfWork.CommitAsync(ct)` & Map DTO | Atomic rollback via DbContext on exception |
| **5** | **Post-Commit Dispatch**| `await eventDispatcher.DispatchEventsAsync(...)` | Logged failure; does not invalidate committed transaction |

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
8. `PaginationClamping`: Paginated queries MUST clamp `Page >= 1` and `1 <= PageSize <= 100` to prevent denial-of-service query exhaustion.

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

public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
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
* [MUST] Implement `IUseCase<TCommand, TResultDto>`.
* [MUST] Group Command, Validator, UseCase, and DTO in a co-located folder slice under `Modules/{BoundedContext}/{Aggregate}/Commands/{CommandName}/`.
* [MUST] Resolve tenant ID from request or fallback to ambient `ICurrentProjectContext` / `IExecutionContext`.
* [MUST] Follow strictly the 4-step PEMR execution pipeline.
* [MUST] Validate line 1 via injected `IValidator<TCommand>` and throw strongly-typed `ValidationException` on failure.
* [MUST] Dispatch domain events strictly post-commit.
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
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
    Guid? CurrentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

// 4. Single-Responsibility UseCase Orchestrator
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateTemplateCommand> validator,
    TimeProvider timeProvider,
    IDomainEventDispatcher eventDispatcher,
    ICurrentProjectContext? projectContext = null,
    IExecutionContext? executionContext = null) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Parse & Validate (Fail-Fast)
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.ToDictionary());

        // Resolve Tenant Context (Explicit Parameter > Ambient Context)
        var effectiveProjectId = command.ProjectId != Guid.Empty
            ? command.ProjectId
            : (projectContext?.IsResolved == true ? projectContext.ProjectId.Value : executionContext?.ProjectId ?? Guid.Empty);

        if (effectiveProjectId == Guid.Empty)
            throw new DomainValidationException("ProjectId cannot be empty.");

        // 2. Execute Precondition Query
        var slugExists = await templateRepo.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
            throw ConflictException.DuplicateSlug(command.Slug);

        // 3. Mutate Domain State
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            new ProjectId(effectiveProjectId),
            TemplateName.Create(command.Name),
            new TemplateSlug(command.Slug),
            command.Category,
            now);

        await templateRepo.AddAsync(template, ct);

        // 4. Commit Transaction
        await unitOfWork.CommitAsync(ct);

        // 5. Post-Commit Domain Event Dispatching
        if (template.DomainEvents.Count > 0)
        {
            await eventDispatcher.DispatchEventsAsync(template.DomainEvents, ct);
            template.ClearDomainEvents();
        }

        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CreatedAt,
            template.UpdatedAt);
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
using SmkDoc.Domain.Interfaces;
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
    IRenderEngine renderEngine,
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
        return await renderEngine.RenderStreamAsync(template.CurrentVersion?.HtmlStream ?? Stream.Null, query.PayloadJson, OutputFormat.Pdf, ct);
    }
}
```

### Archetype 3: Materialized Paged Query Slice (`ListTemplates`)
>> CANONICAL RECIPE [RULE-APP-QRY-003]:
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
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
            t.CurrentVersionId,
            t.CreatedAt,
            t.UpdatedAt)).ToList();

        return new PagedResultDto<TemplateResultDto>(dtos, totalCount, page, pageSize);
    }
}
```

### Archetype 4: Pre-Compiled Template Cache Archetype
>> CANONICAL RECIPE [RULE-APP-CACHE-004]:
* [MUST] Use `ICompiledTemplateCache` to store parsed Handlebars render trees in memory.
* [MUST] Evict or update cache upon template version publication.
```csharp
namespace SmkDoc.Application.Common.Services;

using SmkDoc.Application.Common.Interfaces;

public sealed class CachedTemplateRenderer(
    ICompiledTemplateCache templateCache,
    IRenderEngine renderEngine)
{
    public async Task<Stream> RenderCachedAsync(string templateId, string htmlContent, string payloadJson, CancellationToken ct)
    {
        var compiledTemplate = await templateCache.GetOrCompileAsync(templateId, htmlContent, ct);
        var mergedHtml = compiledTemplate.Render(payloadJson);
        return await renderEngine.RenderStreamAsync(mergedHtml, ct);
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

### [TRAP-APP-002]: MediatR Reflection Indirection vs Direct IUseCase
```diff
- public sealed class CreateTemplateHandler : IRequestHandler<CreateTemplateRequest, TemplateResult> { }
+ public sealed class CreateTemplateUseCase : IUseCase<CreateTemplateCommand, TemplateResultDto> { }
```
*Rationale: MediatR runtime reflection allocations and hidden handler indirection obscure call stacks and degrade cold-start execution.*

### [TRAP-APP-003]: Transaction Boundary: Uncommitted Mutations
```diff
- await templateRepo.AddAsync(template, ct); return template.ToDto();
+ await templateRepo.AddAsync(template, ct); await unitOfWork.CommitAsync(ct); return template.ToDto();
```
*Rationale: Adding an entity to repository without calling `unitOfWork.CommitAsync(ct)` results in silent loss of state changes.*

### [TRAP-APP-004]: LOH In-Memory Buffering vs Streaming Pass-Through
```diff
- using var ms = new MemoryStream(); await sourceStream.CopyToAsync(ms, ct); return ms.ToArray();
+ return await renderEngine.RenderStreamAsync(templateStream, payload, format, ct);
```
*Rationale: Buffering binary payloads >= 85KB into byte arrays causes GC Gen 2 LOH fragmentation and OutOfMemory crashes under concurrency.*

### [TRAP-APP-005]: Direct Clock Query vs TimeProvider
```diff
- var now = DateTime.UtcNow;
+ var now = timeProvider.GetUtcNow();
```
*Rationale: Ambient system clock queries prevent deterministic test harness assertion via `FakeTimeProvider`.*

### [TRAP-APP-006]: Unbounded Query Size (Denial of Service)
```diff
- var pageSize = query.PageSize; // Attacker requests pageSize = 1_000_000
+ var pageSize = Math.Clamp(query.PageSize, 1, 100);
```
*Rationale: Unclamped page sizes allow malicious or misconfigured callers to exhaust database memory and connection pools.*
