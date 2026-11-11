# [PATTERN-002]: 02_application_layer.md — Application Layer Design Patterns & PEMR Pipeline

<context>
Category: 01_clean_arch_patterns
Domain: Application Layer CQRS Architecture, UseCases & PEMR Pipeline
SoleResponsibilities:
  - Vertical CQRS slice topology under `Modules/{BoundedContext}/{Aggregate}/`
  - Action-centric `IUseCase<TReq, TRes>` execution and 4-step PEMR pipeline
  - Request validation (`FluentValidation`), immutable `*ResultDto` returns, and ambient concurrency
  - Modular Application DI feature installers (`{Module}ModuleExtensions.cs`)
Target: `SmkDoc.Application/` (`Modules/`, `Common/`)
Signal: [ARCH_EVOLUTION_APPLICATION]
</context>

---

## 1. Level 1: Affirmative Application Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Application Orchestrator) | Downstream Destination |
|---|---|---|---|
| **Inbound Request** | Presentation Controller | Validates line 1 via `ValidatingUseCaseDecorator` (`IValidator<T>`) | Application UseCase (`IUseCase<TReq, TRes>`) |
| **Precondition** | Command Parameters | Verifies existence / business uniqueness via Domain Port | Repository (`I{Aggregate}Repository.ExistsAsync`) |
| **Domain Mutation** | Validated Command | Invokes Entity SSoT Factory or Expressive Verb | Domain Aggregate (`{Aggregate}.Create(...)` or `{Aggregate}.{Verb}(...)`) |
| **Persistence** | Mutated Aggregate | Commits transaction & dispatches post-commit events | Unit of Work (`IUnitOfWork.CommitAsync`) |
| **Outbound Egress** | Mutated State | Maps directly to immutable Application DTO or Stream | Presentation Layer (`{Aggregate}ResultDto` / `Stream`) |
| **Side-Effects** | Aggregate `DomainEvents` | Dispatches fault-isolated post-commit handlers | `IDomainEventHandler<T>` (Audit, Cache, Telemetry) |

### 1.2 Affirmative Application Orchestration Invariants
| Application Architectural Concern | Affirmative Application Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Class Topology** | Exactly 1 public execution method per use case class: `Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct = default)`. | Class Contract |
| **Interface Binding** | Every use case implements `IUseCase<TRequest, TResponse>` or `IUseCase<TRequest>`. Registered via `services.AddUseCase<TReq, TRes, TUseCase>()`. | DI Container |
| **CQRS Input Semantics** | State mutations MUST be named `*Command` (parameter named `command`). Read/Stream operations MUST be named `*Query` (parameter named `query`). All inputs are positional `sealed record` types. | Feature Slices |
| **Boundary DTO Firewall** | UseCases return dedicated immutable `*ResultDto` records or live `Stream`. Domain Entities, Aggregate Roots, and unhydrated primitives remain strictly sealed inside the Application boundary. | Presentation Egress |
| **Fail-Fast Line 1 Validation**| Payload schema and format invariants are verified at Line 1 via `ValidatingUseCaseDecorator` wrapping `IValidator<T>`. | Pipeline Ingress |
| **Temporal Determinism** | System time is retrieved exclusively via injected `TimeProvider.GetUtcNow()`. Zero ambient system clock queries (`DateTime.UtcNow`). | Temporal Context |
| **Transaction Boundary** | State mutations commit exclusively via `IUnitOfWork.CommitAsync(ct)` at egress. Read queries remain read-only and never commit transactions. | Data Persistence |
| **Zero-LOH Streaming** | Binary render/export operations consume and return live `Stream`. Spooling utilizes `HybridSpoolStream` (<= 64 KB pooled `ArrayPool<byte>`; > 64 KB `DeleteOnClose` temp `FileStream`). Zero unbounded `byte[]` in memory. | Memory & GC |
| **Pagination Clamping** | Paginated queries clamp `Page >= 1` and `PageSize` between 1 and 100 (`Math.Clamp(query.PageSize, 1, 100)`). Hot-path list projections select scalar columns only, bypassing `jsonb` payloads. | Query Scaling |
| **Ambient Concurrency Guard** | Commands and UseCases remain free of `string? IfMatch` parameter plumbing. Mutable `*ResultDto` records implement `IHasConcurrencyToken` (`entity.GetConcurrencyToken()`). Precondition `412` verification executes fail-fast in Step 2 (Ensure) via `concurrencyContext.EnsureMatches(entity)` on mutating update/delete operations, preventing wasted domain computation or external I/O on stale ETags; `ConcurrencySaveChangesInterceptor` serves as the fail-safe interceptor on `CommitAsync(ct)`. | Concurrency Control |
| **Single-Type File Topology** | Every `*Command`, `*Query`, `*Validator`, and `*UseCase` resides in its own isolated `{TypeName}.cs` file inside its vertical slice directory (`Commands/{SliceName}/` or `Queries/{SliceName}/`). | Physical Structure |
| **Modular Module Installers** | Each bounded context slice registers its use cases and services through dedicated `{Module}ModuleExtensions.cs` (e.g. `AuthoringModuleExtensions`, `RenderingModuleExtensions`). Monolithic Application composition roots are prohibited. | DI Container |
| **Direct Pipeline Composition** | Workflows and rendering pipelines consume domain services (`IDataBindingEngine`) directly. Single-method pass-through wrapper classes and duplicate intermediate DTOs are prohibited. | Pipeline Structure |
| **Ingestion Consolidation SSoT** | Template file placeholder scanning and draft parsing execute exclusively within `CreateTemplateUseCase` and `ParseTemplateDraftUseCase`. Standalone file scanning endpoints or un-cached scanner use cases (`/scan-fields`) outside the draft/creation lifecycle are strictly prohibited. | Ingestion Pipeline |
| **PEMR Step 1 Tenancy Guard** | Multi-tenant scoping and ambient identity validation execute strictly at Step 1 (Parse & Guard) of UseCases (`if (!projectContext.IsResolved) throw new ForbiddenException(...)`). Controllers must never inject `ICurrentProjectContext` or validate tenancy before UseCase invocation. | PEMR Cadence |

---

## 2. Level 2: Core Application SSoT Catalogs & Cognitive Governance Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Action-Centric `IUseCase` over MediatR:** Eliminates runtime reflection allocations, avoids opaque pipeline behaviors, produces transparent call stacks, and guarantees compile-time verified DI.
* **Why Positional `sealed record` for Commands/DTOs:** Guarantees absolute immutability across layer boundaries, eliminates mutation side-effects, and provides zero-boilerplate structural equality.
* **Why Automatic Line-1 Validation via Decorator:** Prevents invalid or malformed payloads from touching repositories or executing expensive database queries, throwing strongly-typed `ValidationException` immediately.
* **Why Application DTOs as Boundary Firewalls:** Domain entities must never cross the layer boundary into Presentation; prevents leaking internal domain state or triggering EF Core lazy-loading traps.
* **Why Direct Stream Return for Render/Preview:** Guarantees zero Large Object Heap (LOH) RAM allocation (<85 KB Gen 2 threshold) during high-throughput document streaming.
* **Why Post-Commit Domain Event Dispatch:** Dispatches domain side-effects (e.g. audit logging, cache invalidation) strictly after `unitOfWork.CommitAsync(ct)` succeeds, preventing ghost side-effects on transaction rollback.
* **Why Ambient Concurrency Pipeline over Imperative Command/UseCase Plumbing:** Passing `string? IfMatch` through every Controller, Command, and UseCase pollutes business logic with HTTP string-trimming ceremony. Instead, `ETagFilter` populates scoped `IConcurrencyContext.IfMatch` on ingress, UseCases invoke `concurrencyContext.EnsureMatches(entity)` in Step 2 (Ensure Preconditions) to fail-fast (`PreconditionFailedException` -> `412`), and EF Core `.IsConcurrencyToken()` on `UpdatedAt` catches microsecond mid-flight database race conditions (throwing `DbUpdateConcurrencyException` -> `409` via `GlobalExceptionHandler`).
* **Why Fail-Fast `concurrencyContext.EnsureMatches` in PEMR Step 2:** Calling `concurrencyContext.EnsureMatches(entity)` immediately after retrieving the tracked entity in Step 2 enforces HTTP 412 Precondition Failed *before* executing domain state mutations, allocating domain events, or making external I/O (MinIO, Gotenberg). This eliminates the "Delayed 412" defect, and sets `concurrencyContext.IsVerified = true` so `ConcurrencySaveChangesInterceptor` skips redundant re-evaluation on `CommitAsync(ct)`.

### 2.2 Primary SSoT Interfaces & ProblemDetails RFC 9457 Mapping
```csharp
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

public interface IHasConcurrencyToken
{
    string? ConcurrencyToken { get; }
}

public interface IConcurrencyContext
{
    string? IfMatch { get; set; }
    bool IsVerified { get; }
    void EnsureMatches(IConcurrencyAware entity, string? alternateToken = null);
    void EnsureTokenMatches(string resourceName, object resourceId, string currentToken, string? alternateToken = null);
}
```

| Exception Class | HTTP Status | Causal Rationale |
|---|---|---|
| `ValidationException` | `400 Bad Request` | Fail-fast payload schema or format failure |
| `ForbiddenException` | `403 Forbidden` | Tenant boundary cross or unauthenticated project context |
| `NotFoundException` | `404 Not Found` | Target aggregate ID does not exist in tenant partition |
| `ConflictException` | `409 Conflict` | Unique business key collision or mid-flight concurrency collision (`DbUpdateConcurrencyException`) |
| `PreconditionFailedException` | `412 Precondition Failed` | Optimistic concurrency check failed (`If-Match` token mismatch) |
| `DomainValidationException` | `422 Unprocessable`| Aggregate business invariant violated |
| `DocumentRenderException` | `502 Bad Gateway` | Gotenberg rendering microservice pipeline failure |
| `DistributedStateUnavailableException` | `503 Service Unavailable` | Distributed idempotency store unreachable (Fail-Closed mutation safety) |

### 2.3 Proactive Application Firewalls
```yaml
ApplicationBoundaryFirewall:
  Invariant: "UseCases must return dedicated immutable *ResultDto records or Stream."
  ActionOnViolation: "Replace Domain Entity return types with fully-hydrated *ResultDto."

MediatRProhibitionFirewall:
  Invariant: "Application layer executes use cases directly via IUseCase<TReq, TRes>."
  ActionOnViolation: "Remove MediatR IRequestHandler and register use case with AddUseCase DI."

TransactionCommitFirewall:
  Invariant: "Mutating use cases must commit state changes via IUnitOfWork.CommitAsync(ct)."
  ActionOnViolation: "Insert await unitOfWork.CommitAsync(ct) before returning ResultDto."

ZeroLOHStreamingFirewall:
  Invariant: "Binary export and render pipelines must never buffer whole payloads in byte[]."
  ActionOnViolation: "Refactor return type to live Stream with HybridSpoolStream buffering."

FileTopologyFirewall:
  Invariant: "Every *Command, *Query, *Validator, and *UseCase must live in its own {TypeName}.cs file."
  ActionOnViolation: "Split bundled types into dedicated files co-located in slice folders."

ApplicationModularDiFirewall:
  Invariant: "Application use cases and services must register strictly within their Bounded Context module extension methods, never in a monolithic root file."
  ActionOnViolation: "Extract slice registrations into Modules/{BoundedContext}/{BoundedContext}ModuleExtensions.cs."

GodServiceEliminationFirewall:
  Invariant: "Multi-action application services are 100% prohibited. Every business capability must be an isolated Action-Centric Vertical Slice implementing IUseCase<TReq, TRes>."
  ActionOnViolation: "Decompose multi-method service into discrete single-purpose UseCases."

IngestionConsolidationFirewall:
  Invariant: "File scanning and placeholder extraction must be co-located within CreateTemplate and ParseTemplateDraft UseCases."
  ActionOnViolation: "Purge standalone scanner use cases and endpoints (/scan-fields); route all ingestion through draft/creation pipelines."

UseCaseTenancyPerimeterFirewall:
  Invariant: "Tenant boundary resolution belongs strictly in Step 1 (Parse & Guard) of UseCase execution."
  ActionOnViolation: "Remove ICurrentProjectContext from controllers; insert if (!projectContext.IsResolved) throw new ForbiddenException(...) at Line 1 of UseCase."
```

---

## 3. Level 3: Canonical Working Archetypes (Surgical AST Recipes)

### 3.1 Mutating Command Slice Archetype (`{Verb}{Aggregate}`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-CMD-001]:
* Co-located inside `Modules/{BoundedContext}/{Aggregate}/Commands/{Verb}{Aggregate}/` with strictly 1 primary public type per `.cs` file.
* Resolves tenant identity strictly from mandatory injected `ICurrentProjectContext`.
* Executes strictly via the 4-step PEMR (Parse & Guard, Preconditions, Mutate, Return) pipeline.

```csharp
// --- File: Commands/{Verb}{Aggregate}/{Verb}{Aggregate}Command.cs ---
public sealed record {Verb}{Aggregate}Command(
    string Name,
    string Slug,
    string? Category);

// --- File: Commands/{Verb}{Aggregate}/{Verb}{Aggregate}CommandValidator.cs ---
public sealed class {Verb}{Aggregate}CommandValidator : AbstractValidator<{Verb}{Aggregate}Command>
{
    public {Verb}{Aggregate}CommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9-]+$");
    }
}

// --- File: DTOs/{Aggregate}ResultDto.cs ---
public sealed record {Aggregate}ResultDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyToken = null) : IHasConcurrencyToken;

public sealed record {Aggregate}PagedResultDto(
    IReadOnlyList<{Aggregate}ResultDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// --- File: Commands/{Verb}{Aggregate}/{Verb}{Aggregate}UseCase.cs ---
public sealed class {Verb}{Aggregate}UseCase(
    I{Aggregate}Repository repository,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct = default)
    {
        // Step 1: Tenant Guard <Why: prevent cross-tenant data corruption>
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // Step 2: Ensure Preconditions <Why: fail-fast on duplicate business key before mutation>
        var exists = await repository.ExistsBySlugAsync(projectContext.ProjectId, command.Slug, ct);
        if (exists)
            throw ConflictException.DuplicateSlug(command.Slug);

        // Step 3: Mutate Domain State <Why: enforce aggregate business invariants>
        var now = timeProvider.GetUtcNow();
        var aggregate = {Aggregate}.Create(
            projectContext.ProjectId,
            new {Aggregate}Name(command.Name),
            new {Aggregate}Slug(command.Slug),
            command.Category,
            now);

        await repository.AddAsync(aggregate, ct);

        // Step 4: Commit & Egress <Why: atomic persistence and post-commit event dispatch>
        await unitOfWork.CommitAsync(ct);

        return new {Aggregate}ResultDto(
            aggregate.Id.Value,
            aggregate.ProjectId.Value,
            aggregate.Name.Value,
            aggregate.Slug.Value,
            aggregate.Category,
            aggregate.IsActive,
            aggregate.CreatedAt,
            aggregate.UpdatedAt,
            aggregate.GetConcurrencyToken());
    }
}
```

### 3.2 Zero-LOH Streaming Query Slice Archetype (`{Verb}{Aggregate}`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-STRM-002]:
* Returns live `Stream` directly to caller without intermediary `byte[]` or `MemoryStream.ToArray()` buffering.
* Uses `HybridSpoolStream` for operations requiring seekable buffers.

```csharp
// --- File: Queries/{Verb}{Aggregate}/{Verb}{Aggregate}Query.cs ---
public sealed record {Verb}{Aggregate}Query(
    Guid {Aggregate}Id,
    string PayloadJson);

// --- File: Queries/{Verb}{Aggregate}/{Verb}{Aggregate}QueryValidator.cs ---
public sealed class {Verb}{Aggregate}QueryValidator : AbstractValidator<{Verb}{Aggregate}Query>
{
    public {Verb}{Aggregate}QueryValidator()
    {
        RuleFor(x => x.{Aggregate}Id).NotEmpty();
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}

// --- File: Queries/{Verb}{Aggregate}/{Verb}{Aggregate}UseCase.cs ---
public sealed class {Verb}{Aggregate}UseCase(
    I{Aggregate}Repository repository,
    IStorageService storageService,
    IRenderEngineResolver engineResolver,
    ICurrentProjectContext projectContext) : IUseCase<{Verb}{Aggregate}Query, Stream>
{
    public async Task<Stream> ExecuteAsync({Verb}{Aggregate}Query query, CancellationToken ct = default)
    {
        // Step 1: Tenant Guard <Why: prevent cross-tenant asset inspection>
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // Step 2: Ensure Preconditions <Why: fail-fast on non-existent resource>
        var aggregate = await repository.GetByIdAsync(new {Aggregate}Id(query.{Aggregate}Id), ct);
        if (aggregate is null)
            throw new NotFoundException($"Target aggregate '{query.{Aggregate}Id}' not found.");

        // Step 3: Stream Ingress <Why: Zero-LOH memory stream passthrough>
        await using var artifactStream = await storageService.DownloadAsync(
            StorageBuckets.{Aggregate}s,
            aggregate.StorageKey,
            ct);

        // Step 4: Polymorphic Engine Stream Execution <Why: bypass Gen 2 GC LOH allocations>
        var engine = engineResolver.Resolve(aggregate.RenderEngineType);
        return await engine.RenderStreamAsync(artifactStream, query.PayloadJson, OutputFormat.Pdf, ct);
    }
}
```

### 3.3 Materialized Paged Query Slice Archetype (`List{Aggregate}s`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-QRY-003]:
* Clamps `Page` and `PageSize` parameters at Line 1.
* Projects directly to lightweight scalar DTOs, bypassing heavy `jsonb` payloads.

```csharp
// --- File: Queries/List{Aggregate}s/List{Aggregate}sQuery.cs ---
public sealed record List{Aggregate}sQuery(
    string? SearchTerm,
    string? Category,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20);

// --- File: Queries/List{Aggregate}s/List{Aggregate}sUseCase.cs ---
public sealed class List{Aggregate}sUseCase(
    I{Aggregate}QueryService queryService,
    ICurrentProjectContext projectContext) : IUseCase<List{Aggregate}sQuery, {Aggregate}PagedResultDto>
{
    public async Task<{Aggregate}PagedResultDto> ExecuteAsync(List{Aggregate}sQuery query, CancellationToken ct = default)
    {
        // Step 1: Tenant Guard <Why: ensure tenant-isolated read partition>
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // Step 2: Parameter Clamping <Why: prevent DoS via unbounded page size allocations>
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Step 3: Materialized SQL Projection <Why: bypass multi-KB jsonb payload columns>
        var (items, totalCount) = await queryService.ListPagedAsync(
            projectContext.ProjectId,
            query.SearchTerm,
            query.Category,
            query.IsActive,
            page,
            pageSize,
            ct);

        // Step 4: Wrap Paged Result <Why: immutable pagination envelope>
        return new {Aggregate}PagedResultDto(items, totalCount, page, pageSize);
    }
}
```

### 3.4 Pre-Compiled Template Cache Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-CACHE-004]:
* Pre-compiles render trees into thread-safe memory caches to avoid repeated parsing overhead.
* Evicts or refreshes cache entries upon publication events.

```csharp
public sealed class CachedTemplateRenderer(
    ICompiledTemplateCache templateCache,
    IRenderEngine renderEngine)
{
    public async Task<Stream> RenderCachedAsync(
        string templateId,
        string htmlContent,
        string payloadJson,
        CancellationToken ct)
    {
        // Step 1: Retrieve or compile AST <Why: avoid re-parsing Handlebars tree per request>
        var compiledTemplate = await templateCache.GetOrCompileAsync(templateId, htmlContent, ct);

        // Step 2: Merge JSON payload <Why: thread-safe stateless merge>
        var mergedHtml = compiledTemplate.Render(payloadJson);

        // Step 3: Stream to Render Engine <Why: Zero-LOH stream egress>
        return await renderEngine.RenderStreamAsync(mergedHtml, ct);
    }
}
```

### 3.5 Optimistic Concurrency Mutating Slice Archetype (`Update{Aggregate}` via Ambient Concurrency Pipeline)
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-CMD-005]:
* Co-located inside `Modules/{BoundedContext}/{Aggregate}/Commands/Update{Aggregate}/`.
* Command remains 100% free of manual `IfMatch` string parameters (captured by `ETagFilter` into ambient `IConcurrencyContext`).
* UseCase enforces fail-fast precondition verification in Step 2: Ensure Preconditions via `concurrencyContext.EnsureMatches(entity)`, terminating immediately with `PreconditionFailedException` (412) on stale ETag before domain state mutation or external I/O.
* Sets `concurrencyContext.IsVerified = true`, allowing `ConcurrencySaveChangesInterceptor` on `unitOfWork.CommitAsync(ct)` to skip redundant evaluation while EF Core `.IsConcurrencyToken()` on `UpdatedAt` catches mid-flight race conditions (`DbUpdateConcurrencyException` -> `409 Conflict`).

```csharp
// --- File: Commands/Update{Aggregate}/Update{Aggregate}Command.cs ---
public sealed record Update{Aggregate}Command(
    Guid Id,
    string Name,
    string? Category);

// --- File: Commands/Update{Aggregate}/Update{Aggregate}CommandValidator.cs ---
public sealed class Update{Aggregate}CommandValidator : AbstractValidator<Update{Aggregate}Command>
{
    public Update{Aggregate}CommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

// --- File: Commands/Update{Aggregate}/Update{Aggregate}UseCase.cs ---
public sealed class Update{Aggregate}UseCase(
    I{Aggregate}Repository repository,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    IConcurrencyContext concurrencyContext,
    TimeProvider timeProvider) : IUseCase<Update{Aggregate}Command, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync(Update{Aggregate}Command command, CancellationToken ct = default)
    {
        // Step 1: Tenant Guard <Why: prevent cross-tenant mutation>
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // Step 2: Ensure Preconditions <Why: fail-fast on stale If-Match before domain mutation or external I/O>
        var entity = await repository.GetByIdAsync(new {Aggregate}Id(command.Id), ct)
            ?? throw new NotFoundException($"Target {nameof({Aggregate})} '{command.Id}' not found.");

        concurrencyContext.EnsureMatches(entity); // <Why: throws PreconditionFailedException (412) if ETag mismatch; sets IsVerified = true>

        // Step 3: Mutate Domain State <Why: enforce domain invariants and advance UpdatedAt>
        var now = timeProvider.GetUtcNow();
        entity.UpdateDetails({Aggregate}Name.Create(command.Name), command.Category, now);
        repository.Update(entity);

        // Step 4: Commit & Egress <Why: atomic persistence; interceptor skips redundant check because IsVerified is true>
        await unitOfWork.CommitAsync(ct);

        return new {Aggregate}ResultDto(
            entity.Id.Value,
            entity.ProjectId.Value,
            entity.Name.Value,
            entity.Slug.Value,
            entity.Category,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.GetConcurrencyToken());
    }
}
```

### 3.6 Modular Application DI Composition Archetype (`AddApplicationServices` & Bounded Context Modules)
>> MANDATORY CANONICAL BLUEPRINT [RULE-APP-DI-006]:
* Root `AddApplicationServices()` acts strictly as a lightweight orchestrator (< 25 LOC), delegating to modular Bounded Context extensions.
* Each Bounded Context manages its own registrations via `{BoundedContext}ModuleExtensions.cs` using `services.AddUseCase<TReq, TRes, TImpl>()`.
* Prohibits monolithic dumping of all use cases or services directly into root `DependencyInjection.cs`.

```csharp
// --- File: DependencyInjection.cs (Root Application Orchestrator) ---
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 1. Shared Engine Services & Helpers <Why: cross-cutting calculation & binding SSoT>
        services.AddScoped<IDataBindingEngine, DataBindingEngine>();

        // 2. Modular Bounded Context Installers <Why: isolates slice registrations by domain boundary>
        services.AddRenderingModule();
        services.AddAuthoringModule();
        services.AddIntegrationModule();
        services.AddIdentityAccessModule();

        // 3. Automated FluentValidation Assembly Scanning <Why: fail-fast request validation>
        services.AddValidatorsFromAssemblyContaining<{AnyCommandValidator}>();

        return services;
    }
}

// --- File: Modules/{BoundedContext}/{BoundedContext}ModuleExtensions.cs ---
public static class {BoundedContext}ModuleExtensions
{
    public static IServiceCollection Add{BoundedContext}Module(this IServiceCollection services)
    {
        // Action-Centric Vertical Slices (Strictly 1 UseCase per Command/Query)
        services.AddUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto, {Verb}{Aggregate}UseCase>();
        services.AddUseCase<Get{Aggregate}Query, {Aggregate}ResultDto, Get{Aggregate}UseCase>();

        return services;
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-APP-001]: Entity Leakage vs Application DTO
```diff
- public async Task<{Aggregate}> ExecuteAsync(Get{Aggregate}Query query, CancellationToken ct)
+ public async Task<{Aggregate}ResultDto> ExecuteAsync(Get{Aggregate}Query query, CancellationToken ct)
```
*Rationale: Domain entities leak navigation proxies, internal business states, and EF Core change tracking state.*

### [TRAP-APP-002]: MediatR Reflection Indirection vs Direct IUseCase
```diff
- public sealed class {Verb}{Aggregate}Handler : IRequestHandler<{Verb}{Aggregate}Request, {Aggregate}Result> { }
+ public sealed class {Verb}{Aggregate}UseCase : IUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto> { }
```
*Rationale: MediatR runtime reflection allocations and hidden handler indirection obscure call stacks and degrade cold-start execution.*

### [TRAP-APP-003]: Transaction Boundary: Uncommitted Mutations
```diff
- await repository.AddAsync(aggregate, ct); return aggregate.ToDto();
+ await repository.AddAsync(aggregate, ct); await unitOfWork.CommitAsync(ct); return aggregate.ToDto();
```
*Rationale: Adding an entity to repository without calling `unitOfWork.CommitAsync(ct)` results in silent loss of state changes.*

### [TRAP-APP-004]: LOH In-Memory Buffering vs Streaming Pass-Through
```diff
- using var ms = new MemoryStream(); await sourceStream.CopyToAsync(ms, ct); return ms.ToArray();
+ return await renderEngine.RenderStreamAsync(sourceStream, payload, format, ct);
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

### [TRAP-APP-007]: Bundled Command/Validator in `*UseCase.cs` or Detached `Validators/` Bucket
```diff
- // {Verb}{Aggregate}UseCase.cs (declaring Command + Validator + UseCase together)
- // OR Validators/{Aggregate}Validators.cs (detached from slice namespace)
+ // Commands/{Verb}{Aggregate}/{Verb}{Aggregate}Command.cs
+ // Commands/{Verb}{Aggregate}/{Verb}{Aggregate}CommandValidator.cs
+ // Commands/{Verb}{Aggregate}/{Verb}{Aggregate}UseCase.cs
```
*Rationale: Every `*Command`, `*Query`, `*Validator`, and `*UseCase` MUST live in its own `{TypeName}.cs` file co-located inside its vertical slice namespace.*

### [TRAP-APP-008]: Polluting Commands and UseCases with Imperative If-Match String Parsing
```diff
- public sealed record Update{Aggregate}Command(Guid Id, string Name, string? IfMatch = null);
- // Inside UseCase: manual string trimming & token comparison before CommitAsync
- var sanitized = command.IfMatch.Trim('\"', 'W', '/', ' ');
- if (sanitized != (entity.UpdatedAt ?? entity.CreatedAt).ToUnixTimeMilliseconds().ToString()) throw new PreconditionFailedException(...);
+ public sealed record Update{Aggregate}Command(Guid Id, string Name);
+ // Ambient IConcurrencyContext captured by ETagFilter; UseCase verifies concurrencyContext.EnsureMatches(entity) fail-fast in Step 2
```
*Rationale: Threading HTTP `If-Match` strings through Commands and UseCases violates Clean Architecture separation and duplicates token formatting logic across slices.*

### [TRAP-APP-009]: Application Monolithic Composition Root & God Registration Sprawl
```diff
- // Root DependencyInjection.cs dumping 50+ UseCases across unrelated Bounded Contexts
- services.AddUseCase<GenerateDocumentCommand, ...>();
- services.AddUseCase<PreviewDocumentQuery, ...>();
- services.AddUseCase<CreateTemplateCommand, ...>();
- services.AddUseCase<LoginUserCommand, ...>();
+ // Modular Bounded Context Installers complying with system-wide CompositionRootFirewall
+ services.AddRenderingModule();
+ services.AddAuthoringModule();
+ services.AddIntegrationModule();
+ services.AddIdentityAccessModule();
```
*Rationale: Violates system-wide CompositionRootFirewall. Dumping UseCases across multiple Bounded Contexts into a single root file breaks domain isolation, masks dependencies, and generates severe merge conflicts.*

### [TRAP-APP-010]: Delayed Concurrency Validation (Missing Step 2 EnsureMatches)
```diff
  // Step 2: Ensure Preconditions
  var entity = await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException(...);
+ concurrencyContext.EnsureMatches(entity); // Fail-fast 412 before mutation or external I/O
  // Step 3: Mutate Domain State
  entity.UpdateDetails(name, category, now);
- // Concurrency check only happens later in Step 4 CommitAsync after wasting mutation and I/O cycles!
```
*Rationale: Deferring ETag validation to CommitAsync wastes CPU cycles on domain mutations and risks executing irreversible external I/O (S3 uploads, Gotenberg rendering) for requests doomed to fail HTTP 412. Calling `concurrencyContext.EnsureMatches(entity)` in Step 2 guarantees fail-fast rejection.*

>> SSoT DELEGATION: Document rendering pipeline composition, data binding integration, and anemic pass-through wrapper prohibitions are governed strictly by [document_rendering_mechanics.md](../03_specialized_mechanics/document_rendering_mechanics.md#trap-mech-008).
