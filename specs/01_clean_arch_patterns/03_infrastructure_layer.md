# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & Port Adapters SSoT

<context>
Scope: Infrastructure Port Adapters, Repository Implementations, Transactional Unit of Work, Object Storage Clients (MinIO), PDF Rendering Streaming (Gotenberg), Polymorphic External SQL Drivers, and Redis Distributed State.
SSoT_For: Infrastructure Port Implementations (`I{Aggregate}Repository`, `IUnitOfWork`, `IStorageService`, `IRenderEngine`, `ISqlDriverFactory`), Storage Buckets Catalog (`StorageBuckets.cs`), and Zero-LOH Streaming Pipeline.
Out_Of_Scope: Application UseCase orchestration (see: 02_application_layer.md), Pure Domain Entities (see: 01_domain_layer.md), HTTP Controllers (see: 04_api_presentation_layer.md), Multi-Tenant Dual-Auth Pipeline (see: ../03_specialized_mechanics/multi_tenant_security_pipeline.md), PostgreSQL Physical Schema, Entity Configurations, Indexing & Concurrency Tokens (see: ../03_specialized_mechanics/database_persistence_and_schema_mechanics.md).
Target: `SmkDoc.Infrastructure/` (`Repositories/`, `Storage/`, `Gotenberg/`, `Data/Drivers/`, `Persistence/TransactionalUnitOfWork.cs`).
ArchitecturalSignals: [ARCH_EVOLUTION_INFRASTRUCTURE]
</context>

---

## 1. Level 1: Affirmative Infrastructure Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Infrastructure Port Adapter) | Downstream Destination |
|---|---|---|---|
| **Repository Inbound** | Application Port (`I{Aggregate}Repository`) | Materializes tracked/untracked queries via `AppDbContext` | Database / Network Socket |
| **Unit of Work Inbound**| Application Port (`IUnitOfWork`) | Commits transactions and dispatches domain events post-commit | PostgreSQL Transaction Engine |
| **Storage Inbound** | Application Storage Port (`IStorageService`) | Pipes seekable known-size streams to S3 object storage | MinIO Cluster / AWS S3 Bucket |
| **Rendering Inbound**| Application Render Port (`IRenderEngine`) | Converts HTML/CSS via HTTP Chromium service | Gotenberg 8 Cluster (`/forms/chromium`) |
| **Driver Inbound** | Application Port (`ISqlDriverFactory`) | Dispatches polymorphic SQL query strategies | External Database Connections |
| **Port Outbound** | Database / Gotenberg / S3 Results | Returns domain entities or response-owning streams | Application UseCases (Zero LOH buffer) |

### 1.2 Affirmative Port Adapter Invariants
| Infrastructure Concern | Affirmative Infrastructure Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Persistence Encapsulation** | Hide `DbSet<T>` and `DbContext` strictly behind domain repository interfaces (`I{Aggregate}Repository`). Prohibit leaking ORM contexts outside Infrastructure. | Data Layer Boundary |
| **Internal Query Evaluation** | Repositories materialize queries internally and return `Task<T?>` or `Task<IReadOnlyList<T>>`. Prohibit leaking `IQueryable<T>` across interface boundaries. | Interface Segregation |
| **Tracking Partitioning** | Read-only queries apply `.AsNoTracking()` and project scalar SQL columns (`I{Aggregate}QueryService`); mutating commands fetch tracked entities for ChangeTracker updates. | Memory & Throughput |
| **Fail-Closed Repository Tenancy**| Repositories enforce active project context constraints on all data queries (`Where(t => t.ProjectId == projectId)`). Prohibit bypassing tenant isolation in custom queries. | Tenancy Isolation |
| **Post-Commit Event Dispatch** | `TransactionalUnitOfWork` commits database changes via `SaveChangesAsync` *before* harvesting and dispatching domain events. Prohibit pre-commit event dispatch. | Atomic Persistence |
| **Response-Owning Gotenberg Streams**| Wrap Gotenberg HTTP responses in `ResponseDisposingStream` using `ResponseHeadersRead` to deterministically release sockets upon stream closure. | Network Socket Lifecycle |
| **Zero-LOH Stream Sizing** | Object storage uploads require seekable streams with known `Stream.Length` spooled via `HybridSpoolStream`; initialized buckets are cached in `ConcurrentDictionary`. | Storage Gateway |
| **Distributed State Topology** | Redis stores utilize atomic Lua scripts with fail-closed semantics for idempotency (`503`), fail-open for result caches, and `pg_advisory_lock` for multi-pod migrations. | Multi-Pod Scalability |
| **Polymorphic SQL Execution** | Execute external SQL datasets via `ISqlDriverFactory` resolving provider-specific strategies (`PostgresDriverStrategy`, `SqlServerDriverStrategy`) with AST and transaction read-only isolation. | Data Federation |

* **Relational Schema & Concurrency Delegation:** All physical table configurations (`IEntityTypeConfiguration<T>`), primary key UUIDv7 generation, composite indexing, foreign key cascade boundaries, JSONB mapping, and PostgreSQL `xmin` concurrency mechanics are governed exclusively by [database_persistence_and_schema_mechanics.md](../03_specialized_mechanics/database_persistence_and_schema_mechanics.md).

---

## 2. Level 2: Core Infrastructure Architecture & Cognitive Rationale

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Repositories Hide `IQueryable<T>`:** Leaking `IQueryable` out of Infrastructure blurs architectural boundaries, permits deferred database execution inside presentation layers without transaction wrappers, and makes unit testing Application UseCases fragile.
* **Why `.AsNoTracking()` Separation:** Query reads bypass EF ChangeTracker overhead entirely for maximum throughput; mutating commands fetch tracked entities so `SaveChangesAsync` produces minimal SQL diffs.
* **Why Post-Commit Domain Event Dispatching:** If events are dispatched before `SaveChangesAsync` succeeds, side-effects (such as webhook notifications or audit queue pushes) execute even when database transaction commits fail, causing catastrophic state corruption.
* **Why Response-Owning Gotenberg Streams & Known-Size S3 Uploads:** Returning raw `response.Content.ReadAsStreamAsync()` without disposing `HttpResponseMessage` leaks sockets, while passing `ObjectSize(-1)` to MinIO SDK forces a 5 MB+ multipart LOH buffer allocation. Wrapping HTTP streams in a response-disposing decorator and spooling production outputs via `ArrayPool<byte>` yields deterministic socket release, exact `FileSizeBytes`, and zero-LOH S3 uploads.
* **Why Polymorphic SQL Driver Strategies:** Eliminates monolithic switch statements and provider-specific branching by abstracting external SQL execution behind isolated driver strategies with strict read-only token analysis.

### 2.2 Proactive Infrastructure Firewalls
```yaml
RepositoryBoundaryFirewall:
  Invariant: "Repositories must never return IQueryable<T> across interface boundary."
  ActionOnViolation: "Materialize query inside repository and return IReadOnlyList<T> or T?."

FailClosedTenancyFirewall:
  Invariant: "Repository queries must enforce ProjectId parameter constraints."
  ActionOnViolation: "Attach explicit Where(t => t.ProjectId == projectId) to all repository queries."

PostCommitEventFirewall:
  Invariant: "Unit of Work must call SaveChangesAsync before dispatching harvested domain events."
  ActionOnViolation: "Reorder UnitOfWork execution: 1. SaveChangesAsync, 2. ClearEvents, 3. DispatchEvents."

GotenbergSocketLeakFirewall:
  Invariant: "Gotenberg HTTP stream responses must be wrapped in ResponseDisposingStream."
  ActionOnViolation: "Wrap ReadAsStreamAsync() with ResponseDisposingStream and ResponseHeadersRead."

S3StreamSizeFirewall:
  Invariant: "UploadAsync requires known stream length to avoid MinIO 5MB multipart LOH buffers."
  ActionOnViolation: "Spool unseekable streams through HybridSpoolStream before calling UploadAsync."
```

### 2.3 Object Storage Buckets SSoT Catalog (`StorageBuckets.cs`)
| Bucket Constant | Bucket Name | Target Content | Key Path Partitioning Scheme |
|---|---|---|---|
| `StorageBuckets.Templates` | `templates` | Immutable HTML/DOCX/XLSX template versions | `templates/{projectId:N}/{templateId:N}/v{version}{ext}` |
| `StorageBuckets.Drafts` | `drafts` | Temporary tenant-isolated draft binaries prior to commit | `drafts/{projectId:N}/{draftId}{ext}` |
| `StorageBuckets.Outputs` | `outputs` | Generated production documents | `outputs/{projectId:N}/{yyyy/MM/dd}/{slug}_{generationId:N}.{ext}` |
| `StorageBuckets.AuditPayloads` | `audit-payloads` | Large generation request payloads (>16KB) | `audit/{projectId:N}/{yyyy/MM/dd}/{payloadSha256}.json` |
| `StorageBuckets.Fonts` | `fonts` | Tenant-isolated custom typography assets | `fonts/{projectId:N}/{safeFileName}` |

---

## 3. Level 3: Canonical Infrastructure Archetypes (Surgical AST Recipes)

### 3.1 Repository Implementation Archetype (`{Aggregate}Repository`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-001]:
* Primary constructor injecting `AppDbContext`.
* Queries materialized internally; returns `Task<T?>` or `Task<IReadOnlyList<T>>` (never leaks `IQueryable<T>`).
* Read queries utilize `.AsNoTracking()`. Mutating queries load tracked aggregates.

```csharp
public sealed class {Aggregate}Repository(AppDbContext dbContext) : I{Aggregate}Repository
{
    // Step 1: Tracked Read for Mutation <Why: ChangeTracker tracks state updates for SaveChanges>
    public async Task<{Aggregate}?> GetByIdAsync({Aggregate}Id id, CancellationToken ct = default)
    {
        return await dbContext.{Aggregate}s
            .Include(t => t.CurrentVersion)
            .Include(t => t.Children)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    // Step 2: Untracked Read for Queries <Why: zero ChangeTracker overhead on read paths>
    public async Task<IReadOnlyList<{Aggregate}>> ListByProjectIdAsync(ProjectId projectId, CancellationToken ct = default)
    {
        return await dbContext.{Aggregate}s
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    // Step 3: Fast Precondition Existence Check <Why: indexed boolean check without entity hydration>
    public async Task<bool> ExistsBySlugAsync(ProjectId projectId, string slug, CancellationToken ct = default)
    {
        return await dbContext.{Aggregate}s.AnyAsync(t => t.ProjectId == projectId && t.Slug == slug, ct);
    }

    // Step 4: Attach New Aggregate <Why: queues insert into ChangeTracker>
    public async Task AddAsync({Aggregate} aggregate, CancellationToken ct = default)
    {
        await dbContext.{Aggregate}s.AddAsync(aggregate, ct);
    }

    public void Update({Aggregate} aggregate)
    {
        dbContext.{Aggregate}s.Update(aggregate);
    }
}
```

---

### 3.2 Transactional Unit of Work Archetype (`TransactionalUnitOfWork`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-002]:
* Commits database changes via `SaveChangesAsync` BEFORE dispatching events.
* Clears domain events from entities to prevent duplicate dispatching.

```csharp
public sealed class TransactionalUnitOfWork(
    AppDbContext dbContext,
    IDomainEventDispatcher eventDispatcher) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        // Step 1: Harvest Events <Why: snapshot all domain events before commit>
        var aggregates = dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();

        // Step 2: Commit Transaction <Why: atomic relational persistence>
        var rowsAffected = await dbContext.SaveChangesAsync(ct);

        // Step 3: Clear Harvested Events <Why: prevent ghost duplicate event dispatches>
        aggregates.ForEach(a => a.ClearDomainEvents());

        // Step 4: Dispatch Events Post-Commit <Why: side-effects execute only upon successful commit>
        if (events.Count > 0)
        {
            await eventDispatcher.DispatchEventsAsync(events, ct);
        }

        return rowsAffected;
    }
}
```

---

### 3.3 Zero-LOH Gotenberg HTTP Streaming Client Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-003]:
* Uses `HttpCompletionOption.ResponseHeadersRead` to stream payload directly.
* Wraps stream in `ResponseDisposingStream` to deterministically dispose `HttpResponseMessage` and socket on completion.

```csharp
public sealed class GotenbergClient(HttpClient httpClient)
{
    public async Task<Stream> ConvertHtmlToPdfStreamAsync(string htmlContent, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(htmlContent), "files", "index.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html")
        {
            Content = form
        };

        // Step 1: Send with ResponseHeadersRead <Why: do not buffer response body in memory>
        var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            httpResponse.EnsureSuccessStatusCode();
            var innerStream = await httpResponse.Content.ReadAsStreamAsync(ct);

            // Step 2: Wrap in ResponseDisposingStream <Why: releases HTTP socket when consumer closes stream>
            return new ResponseDisposingStream(innerStream, httpResponse);
        }
        catch (HttpRequestException ex)
        {
            httpResponse.Dispose();
            throw new DocumentRenderException($"Gotenberg cluster unreachable or returned error: {ex.Message}", ex);
        }
        catch
        {
            httpResponse.Dispose();
            throw;
        }
    }
}
```

---

### 3.4 S3 Object Storage Service Archetype (MinIO)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-004]:
* Injects `IMinioClient` with cached bucket verification via `ConcurrentDictionary`.
* Requires seekable known-size `Stream` on `UploadAsync` to prevent 5MB+ multipart LOH allocations.

```csharp
public sealed class MinioStorageService(
    IMinioClient client,
    ILogger<MinioStorageService> logger) : IStorageService
{
    private readonly ConcurrentDictionary<string, bool> _initializedBuckets = new(StringComparer.Ordinal);

    public async Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketInitializedOnceAsync(bucket, ct);

        // Step 1: Enforce Known-Size Seekable Stream <Why: prevent MinIO SDK from allocating 5MB LOH buffers>
        if (!data.CanSeek)
            throw new InvalidOperationException("UploadAsync requires a seekable stream with known Length.");

        // Step 2: Upload Bounded Stream <Why: zero intermediate byte[] memory allocations>
        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length - data.Position)
            .WithContentType(contentType);

        await client.PutObjectAsync(args, ct);
        logger.LogInformation("Uploaded object {ObjectName} to bucket {Bucket}", objectName, bucket);
        return objectName;
    }

    private async Task EnsureBucketInitializedOnceAsync(string bucket, CancellationToken ct)
    {
        if (_initializedBuckets.ContainsKey(bucket)) return;

        var existsArgs = new BucketExistsArgs().WithBucket(bucket);
        bool exists = await client.BucketExistsAsync(existsArgs, ct);
        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(bucket);
            await client.MakeBucketAsync(makeArgs, ct);
        }
        _initializedBuckets.TryAdd(bucket, true);
    }
}
```

---

### 3.5 Polymorphic SQL Driver Strategy Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-005]:
* Resolves SQL execution drivers via polymorphic dictionary lookup (`ISqlDriverFactory`).
* Enforces read-only isolation across AST token validation and transactional commands.

```csharp
public sealed class SqlDriverFactory(IEnumerable<ISqlDriverStrategy> strategies) : ISqlDriverFactory
{
    private readonly Dictionary<string, ISqlDriverStrategy> _strategies =
        strategies.ToDictionary(s => s.Provider, s => s, StringComparer.OrdinalIgnoreCase);

    public ISqlDriverStrategy GetStrategy(string provider)
    {
        // Step 1: Polymorphic Provider Dispatch <Why: zero if/else or switch-case extension fragility>
        if (_strategies.TryGetValue(provider, out var strategy))
        {
            return strategy;
        }

        throw new NotSupportedException($"Database provider '{provider}' is not supported.");
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-INFRA-001]: IQueryable Boundary Leakage
```diff
- public IQueryable<{Aggregate}> Get{Aggregate}s() => dbContext.{Aggregate}s;
+ public async Task<IReadOnlyList<{Aggregate}>> Get{Aggregate}sAsync(ProjectId projectId, CancellationToken ct) => await dbContext.{Aggregate}s.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);
```
*Rationale: Leaking `IQueryable` crosses layer boundaries, causing deferred execution outside transaction and unit-of-work scopes.*

### [TRAP-INFRA-002]: Multi-Tenant Isolation Bypass in Repository
```diff
- var aggregates = await dbContext.{Aggregate}s.IgnoreQueryFilters().ToListAsync(ct);
+ var aggregates = await dbContext.{Aggregate}s.ToListAsync(ct);
```
*Rationale: Disabling global query filters inside repositories creates catastrophic IDOR cross-tenant data leaks in production endpoints.*

### [TRAP-INFRA-003]: Untracked Aggregate Mutation (Silent Save Loss)
```diff
- var aggregate = await dbContext.{Aggregate}s.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct); aggregate.Update(name, now); await unitOfWork.CommitAsync(ct);
+ var aggregate = await repository.GetByIdAsync(id, ct); aggregate.Update(name, now); await unitOfWork.CommitAsync(ct);
```
*Rationale: Entities fetched with `AsNoTracking()` are ignored by EF Core ChangeTracker, silently emitting zero SQL updates on commit.*

### [TRAP-INFRA-004]: Multi-MB Gotenberg Buffer Allocation in RAM
```diff
- var bytes = await response.Content.ReadAsByteArrayAsync(ct); return new MemoryStream(bytes);
+ return await response.Content.ReadAsStreamAsync(ct);
```
*Rationale: Calling `ReadAsByteArrayAsync` forces multi-MB PDFs into RAM byte arrays, triggering GC Gen 2 LOH fragmentation.*

### [TRAP-INFRA-005]: Unseekable Unknown-Size S3 Upload (LOH Multipart Buffer Trap)
```diff
- var args = new PutObjectArgs().WithStreamData(stream).WithObjectSize(-1);
+ if (!stream.CanSeek) throw new InvalidOperationException("Seekable stream required."); var args = new PutObjectArgs().WithStreamData(stream).WithObjectSize(stream.Length - stream.Position);
```
*Rationale: Passing `ObjectSize(-1)` forces the S3 SDK to allocate 5MB+ memory chunks in Large Object Heap (LOH) to perform multipart buffering.*
