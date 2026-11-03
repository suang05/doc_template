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
| **Inbound Request** | Presentation Controller | Validates line 1 via `ValidatingUseCaseDecorator` (`IValidator<T>`) | Application UseCase (`IUseCase<TReq, TRes>`) |
| **Precondition** | Command Parameters | Checks uniqueness / existence via Domain Port | Repository (`ITemplateRepository.SlugExistsAsync`) |
| **Domain Mutation** | Validated Command | Invokes Entity Factory or Expressive Verb | Domain Aggregate (`Template.Create(...)`) |
| **Persistence** | Mutated Aggregate | Commits transaction & dispatches post-commit events | Unit of Work (`IUnitOfWork.CommitAsync`) |
| **Outbound Egress** | Mutated State | Maps directly to immutable DTO or Stream | Presentation Layer (`ApiResponse<T>` / `Stream`) |
| **Side-Effects** | Aggregate `DomainEvents` | Dispatches fault-isolated post-commit handlers | `IDomainEventHandler<T>` (Audit, Cache, Telemetry) |

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
  - Trigger: UseCase parameter named request or input class suffixed with *Request instead of *Command or *Query
    Action: Enforce *Command with parameter command for mutations, and *Query with parameter query for queries >> Ref: [RULE-APP-CMD-001]
  - Trigger: Declaring *Command, *Query, or *Validator inside *UseCase.cs, or placing validators in a shared Validators/ bucket folder
    Action: Split into dedicated {TypeName}.cs files co-located inside Commands/{SliceName}/ or Queries/{SliceName}/ >> Ref: [RULE-CONV-005], [TRAP-APP-007]

CoreExtensionSlots:
  RenderingEngines: Pluggable IRenderEngineFactory polymorphic dispatch (Html, Docx, Excel via FrozenDictionary)
  ValidationPipeline: Automatic IValidator<TCommand> fail-fast execution at line 1
  QueryProjections: Materialized PagedResultDto<T> with boundary clamping, ambient tenant isolation, and jsonb-free SQL projections (ITemplateQueryService, IGenerationLogQueryService)
  CompiledTemplateCache: ICompiledTemplateCache + ITemplateCacheInvalidationBus (64-stripe compiled AST cache with post-commit Redis Pub/Sub cross-pod invalidation)
  DatasetResultCache: IDatasetResultCache (Tenant-isolated external SQL JSON result cache honoring Dataset.CacheSeconds via Redis/Memory)
  AuditLoggingPipeline: IGenerationLogQueue (Bounded in-memory Channel<GenerationLog> with GenerationLogBatchWorker background flush and graceful drain, decoupling OLTP inserts from document generation requests)
  TemplateDraftCache: ITemplateDraftCache (Zero-LOH tenant-scoped draft metadata in Redis/Memory + binary stream in MinIO StorageBuckets.Drafts)
  HybridStreamSpooler: HybridSpoolStream (<= 64 KB ArrayPool<byte> fast path + > 64 KB DeleteOnClose temp FileStream spillover + CreateFromUtf8StringAsync pooled chunked encoding)
  DocumentRenderingPipeline: IDocumentRenderPipeline (Encapsulates data preparation, Draft-07 schema gate, polymorphic engine render, spooling, MinIO upload, and failure audit logging)
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
3. `CQRSNamingAndImmutability`: Mutating inputs MUST be suffixed with `Command` (parameter named `command`); Read/Stream inputs MUST be suffixed with `Query` (parameter named `query`). Strictly prohibit `*Request` suffix in Application layer (`*Request` is reserved for Presentation HTTP wire models). All Commands and Queries MUST be `sealed record` with positional primary constructor parameters.
4. `BoundaryFirewall`: Prohibit returning Domain Entities (`AggregateRoot`, `Entity`, `ValueObject`). Return immutable Application DTO records or `Stream`.
5. `TemporalResolution`: System time queries MUST consume `TimeProvider.GetUtcNow()`. Prohibit ambient clock queries (`DateTime.UtcNow`).
6. `TransactionIsolation`: Mutating use cases commit strictly via `IUnitOfWork.CommitAsync(ct)` at egress. Read queries never mutate state.
7. `StreamingIntegrity`: Render/Preview/Dry-Run operations MUST consume and return live `Stream` (`IRenderEngine`, `IPdfRenderer`, and `IStorageService` expose zero `byte[]` / `Task<byte[]>` return types). Seekable intermediate copies (DOCX/XLSX security scans, OpenXML/ClosedXML packages) MUST spool through `HybridSpoolStream` (`<= 64 KB` pooled `ArrayPool<byte>`; `> 64 KB` `FileOptions.DeleteOnClose` temp `FileStream`), and HTML dry-runs MUST drain to `Stream.Null` (`81,920`-byte buffer).
8. `PaginationClamping`: Paginated queries MUST clamp `Page >= 1` and bounded `PageSize` to prevent denial-of-service query exhaustion, and list hot-paths MUST project scalar DTO columns in SQL without hydrating multi-KB `jsonb` columns (`DataSchema`, `SamplePayload`, `MappingsSnapshot`, `InputData`).
9. `OnePrimaryTypePerSliceFile`: Every `*Command`, `*Query`, `*Validator`, `*UseCase`, `*EventHandler`, and Service/Interface MUST reside in its own `.cs` file (`{TypeName}.cs`) co-located inside its vertical slice folder (`Commands/{SliceName}/` or `Queries/{SliceName}/`). Prohibit declaring multiple primary public types inside `*UseCase.cs` or grouping validators in detached `Validators/` folders (only `*Dtos.cs` / `*ResultDto.cs` files may group immutable DTO records).
10. `BoundedMultiDatasetExecution`: `DatasetAliasMapBuilder` MUST batch-load datasets and connections (`IDatasetRepository.GetByIdsAsync` with `.AsNoTracking().Include(d => d.DataConnection)` — zero per-alias N+1 queries), and `FieldMappingApplicatorService` MUST execute distinct external SQL aliases with bounded `SemaphoreSlim(4)` concurrency, fail-fast linked `CancellationTokenSource`, and tenant-isolated SHA-256 `IDatasetResultCache` lookup when `Dataset.CacheSeconds > 0`.

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
| `DistributedStateUnavailableException` | `503 Service Unavailable` | Distributed idempotency store unreachable (Fail-Closed mutation safety) |

---

## 4. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### Archetype 1: Mutating Command Slice (`CreateTemplate`)
>> CANONICAL RECIPE [RULE-APP-CMD-001]:
* [MUST] Implement `IUseCase<TCommand, TResultDto>`.
* [MUST] Co-locate Command, Validator, and UseCase inside `Modules/{BoundedContext}/{Aggregate}/Commands/{CommandName}/` with **strictly 1 primary public type per `.cs` file** (`{CommandName}Command.cs`, `{CommandName}CommandValidator.cs`, `{CommandName}UseCase.cs`) and DTOs in `DTOs/`. Never bundle Command or Validator inside `*UseCase.cs`, and never dump validators into detached `Validators/` folders >> Ref: [RULE-CONV-005], [RULE-STRUCT-004].
* [MUST] Resolve tenant identity strictly from mandatory injected `ICurrentProjectContext` as the single channel-agnostic ambient context (zero caller `ProjectId` parameter on tenant Commands; `IExecutionContext` is retired) >> Ref: [RULE-SEC-003].
* [MUST] Follow strictly the 4-step PEMR execution pipeline.
* [MUST] Register via `services.AddUseCase<TCommand, TResultDto, TUseCase>()` so `ValidatingUseCaseDecorator` executes all `IValidator<TCommand>` instances at Line 1 (prohibit manual `IValidator<T>` injection inside UseCases).
* [MUST] Delegate transaction commit and fault-isolated post-commit domain event dispatch to `IUnitOfWork.CommitAsync(ct)`.
```csharp
// --- File: Commands/CreateTemplate/CreateTemplateCommand.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed record CreateTemplateCommand(
    string Name,
    string Slug,
    string? Category);
```
```csharp
// --- File: Commands/CreateTemplate/CreateTemplateCommandValidator.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

using FluentValidation;

public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9-]+$");
    }
}
```
```csharp
// --- File: DTOs/TemplateResultDto.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

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
```
```csharp
// --- File: Commands/CreateTemplate/CreateTemplateUseCase.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Tenant Guard (Schema/Format validation already executed by ValidatingUseCaseDecorator)
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required to create a template.");

        // 2. Execute Precondition Query
        var slugExists = await templateRepository.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
            throw ConflictException.DuplicateSlug(command.Slug);

        // 3. Mutate Domain State (Explicit Local Variable Assignments)
        var templateName = TemplateName.Create(command.Name);
        var templateSlug = new TemplateSlug(command.Slug);
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            projectContext.ProjectId,
            templateName,
            templateSlug,
            command.Category,
            now);

        await templateRepository.AddAsync(template, ct);

        // 4. Commit Transaction & Dispatch Post-Commit Domain Events via UnitOfWork
        await unitOfWork.CommitAsync(ct);

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
// --- File: Queries/RenderDocument/RenderDocumentQuery.cs ---
namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

public sealed record RenderDocumentQuery(
    Guid TemplateId,
    string PayloadJson);
```
```csharp
// --- File: Queries/RenderDocument/RenderDocumentQueryValidator.cs ---
namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

using FluentValidation;

public sealed class RenderDocumentQueryValidator : AbstractValidator<RenderDocumentQuery>
{
    public RenderDocumentQueryValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}
```
```csharp
// --- File: Queries/RenderDocument/RenderDocumentUseCase.cs ---
namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.RenderDocument;

using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

public sealed class RenderDocumentUseCase(
    ITemplateRepository templateRepository,
    IStorageService storageService,
    IRenderEngineResolver engineResolver,
    ICurrentProjectContext projectContext) : IUseCase<RenderDocumentQuery, Stream>
{
    public async Task<Stream> ExecuteAsync(RenderDocumentQuery query, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        var template = await templateRepository.GetByIdAsync(new TemplateId(query.TemplateId), ct);
        if (template is null || template.CurrentVersion is null)
            throw new NotFoundException($"Published template '{query.TemplateId}' not found.");

        await using var templateStream = await storageService.DownloadAsync(
            StorageBuckets.Templates,
            template.CurrentVersion.StorageKey,
            ct);

        var engine = engineResolver.Resolve(template.CurrentVersion.GetRenderEngineType());
        return await engine.RenderStreamAsync(templateStream, query.PayloadJson, OutputFormat.Pdf, ct);
    }
}
```

### Archetype 3: Materialized Paged Query Slice (`ListTemplates`)
>> CANONICAL RECIPE [RULE-APP-QRY-003]:
```csharp
// --- File: Queries/ListTemplates/ListTemplatesQuery.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed record ListTemplatesQuery(int Page = 1, int PageSize = 20);
```
```csharp
// --- File: Queries/ListTemplates/ListTemplatesUseCase.cs ---
namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

public sealed class ListTemplatesUseCase(
    ITemplateRepository templateRepository,
    ICurrentProjectContext projectContext) : IUseCase<ListTemplatesQuery, PagedResultDto<TemplateResultDto>>
{
    public async Task<PagedResultDto<TemplateResultDto>> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await templateRepository.GetPagedByProjectAsync(projectContext.ProjectId, null, page, pageSize, ct);

        var dtos = items.Select(template => new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CreatedAt,
            template.UpdatedAt)).ToList();

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

### [TRAP-APP-007]: Bundled Command/Validator in `*UseCase.cs` or Detached `Validators/` Bucket
```diff
- // RollbackTemplateVersionUseCase.cs (declaring Command + Validator + UseCase together)
- // OR Validators/DocumentCommandValidators.cs (detached from slice namespace)
+ // Commands/RollbackTemplateVersion/RollbackTemplateVersionCommand.cs
+ // Commands/RollbackTemplateVersion/RollbackTemplateVersionCommandValidator.cs
+ // Commands/RollbackTemplateVersion/RollbackTemplateVersionUseCase.cs
```
*Rationale: Every `*Command`, `*Query`, `*Validator`, and `*UseCase` MUST live in its own `{TypeName}.cs` file co-located inside its vertical slice namespace.*
