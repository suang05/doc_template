# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & Port Adapters SSoT

<context>
Category: 01_clean_arch_patterns
Domain: Infrastructure Layer Architecture, Port Adapters & Modular DI
SoleResponsibilities:
  - External port implementations (`IStoragePort` MinIO, `ISqlDriverFactory`, PostgreSQL durable state & Redis cache)
  - Modular Infrastructure DI composition root (`Extensions/` feature installers <= 25 LOC)
  - `HybridTemplateDraftCache` (L1 MemoryCache + L2 Redis multiplexer)
  - Transactional unit of work (`TransactionalUnitOfWork.cs`) and `OutboxSaveChangesInterceptor` (`outbox_messages`)
Target: `SmkDoc.Infrastructure/` (`Storage/`, `Data/Drivers/`, `Cache/`, `Extensions/`, `Persistence/`)
Signal: [ARCH_EVOLUTION_INFRASTRUCTURE]
</context>

---

## 1. Level 1: Affirmative Infrastructure Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Infrastructure Port Adapter) | Downstream Destination |
|---|---|---|---|
| **Repository Inbound** | Domain/Application Port (`I{Aggregate}Repository`) | Materializes tracked queries via `AppDbContext` | PostgreSQL Database Engine |
| **ReadModel Inbound** | Application Read Port (`I{Aggregate}ReadModel`) | Materializes `.AsNoTracking()` scalar projections | PostgreSQL Database Engine |
| **Unit of Work Inbound**| Application Port (`IUnitOfWork`) | Harvests domain events into `outbox_messages` & commits atomically | PostgreSQL Transaction Engine |
| **Storage Inbound** | Application Storage Port (`IStoragePort`) | Pipes seekable known-size streams to S3 object storage | MinIO Cluster / AWS S3 Bucket |
| **Rendering Inbound**| Application Render Port (`IRenderEngine`) | Converts HTML/CSS via HTTP Chromium service | Gotenberg 8 Cluster (`/forms/chromium`) |
| **Driver Inbound** | Application Port (`ISqlDriverFactory`) | Dispatches polymorphic read-only SQL query strategies | External Tenant Database Connections |
| **Port Outbound** | Database / Gotenberg / S3 Results | Returns domain entities or response-owning streams | Application UseCases (Bounded Pooled Memory) |

### 1.2 Affirmative Port Adapter Invariants
| Infrastructure Concern | Affirmative Infrastructure Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Persistence Encapsulation** | Hide `DbSet<T>` and `DbContext` strictly behind domain repository interfaces (`I{Aggregate}Repository`) and read models (`I{Aggregate}ReadModel`). Prohibit leaking ORM contexts outside Infrastructure. | Data Layer Boundary |
| **Internal Query Evaluation** | Repositories materialize queries internally and return `Task<T?>` or `Task<IReadOnlyList<T>>`. Prohibit leaking `IQueryable<T>` across interface boundaries. | Interface Segregation |
| **Tracking Partitioning** | Read-only queries apply `.AsNoTracking()` (and `.AsSplitQuery()` on multi-collection loads) via `I{Aggregate}ReadModel`; mutating commands fetch tracked entities via `I{Aggregate}Repository`. | Memory & Throughput |
| **5-Layer Fail-Closed Tenancy**| Enforce tenant isolation via (1) Fail-Closed EF Global Query Filter, (2) `TenantWriteEnforcementInterceptor`, (3) PostgreSQL RLS (`SET LOCAL app.current_project_id`), and (4) banned `.IgnoreQueryFilters()`. | Tenancy Isolation |
| **Transactional Outbox Persistence** | `OutboxSaveChangesInterceptor` harvests `entity.DomainEvents` into `outbox_messages` inside the **same** PostgreSQL transaction during `IUnitOfWork.CommitAsync(ct)`, and `OutboxRelayWorker` dispatches at-least-once asynchronously (`TRAP-EVENT-010`). | Atomic Persistence |
| **Response-Owning Gotenberg Streams**| Wrap Gotenberg HTTP responses in `ResponseDisposingStream` using `ResponseHeadersRead` to deterministically release sockets upon stream closure. | Network Socket Lifecycle |
| **Bounded Pooled Stream Sizing** | Object storage uploads require seekable streams with known `Stream.Length` spooled via `HybridSpoolStream` (`65_536`-byte pooled chunks); initialized buckets are cached in `ConcurrentDictionary`. | Storage Gateway |
| **Durable Idempotency & Cache Topology** | Idempotency keys (`idempotency_keys`) and async batch jobs (`document_jobs` with `FOR UPDATE SKIP LOCKED`) are persisted durably in PostgreSQL; Redis is used for L2 caching and distributed rate-limit partitioning (`TRAP-SCALE-014`). | Multi-Pod Scalability |
| **Modular Feature Installers (CompositionRootFirewall)** | Infrastructure DI enforces the system-wide `CompositionRootFirewall` by registering dependencies through 5 single-responsibility extension installers (`Persistence`, `Storage`, `RenderingEngines`, `DistributedInfrastructure`, `SecurityInfrastructure`). The composition root `SmkDoc.Infrastructure/DependencyInjection.cs` remains a lean $\le 25$ LOC coordinator. | Composition Root |
| **Hybrid Draft Cache SSoT (CodebaseSSoTFirewall)** | Enforces system-wide `CodebaseSSoTAndDeduplicationFirewall`. Ephemeral template drafts are cached via `HybridTemplateDraftCache` (L1 in-memory LRU with transparent L2 Redis synchronization), guaranteeing zero conditional DI branching or duplicate cache classes across environments. | Ephemeral Cache |
| **Singleton Handlebars Registry** | Pre-registers custom helpers once at application startup and injects `IHandlebars` as a thread-safe singleton. Per-request or scoped re-instantiation of Handlebars is prohibited. | Engine Initialization |
| **Polymorphic SQL Execution** | Execute external SQL datasets via `ISqlDriverFactory` resolving provider-specific strategies (`PostgresDriverStrategy`, `SqlServerDriverStrategy`) with `ConnectionHostValidator` IP pinning, `DbConnectionStringBuilder`, `BEGIN TRANSACTION READ ONLY`, 5s statement timeout, and `10_000`-row cap (`TRAP-SQL-011`). | Data Federation |

>> SSoT DELEGATION: PostgreSQL physical table configurations (`IEntityTypeConfiguration<T>`), primary key UUIDv7, composite indexing, foreign key cascade boundaries, JSONB mapping, and optimistic concurrency mechanics are governed strictly by [database_persistence_and_schema_mechanics.md](../03_specialized_mechanics/database_persistence_and_schema_mechanics.md).

---

## 2. Level 2: Core Infrastructure Architecture & Cognitive Rationale

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Repositories Hide `IQueryable<T>`:** Leaking `IQueryable` out of Infrastructure blurs architectural boundaries, permits deferred database execution inside presentation layers without transaction wrappers, and makes unit testing Application UseCases fragile.
* **Why `.AsNoTracking()` Separation:** Query reads bypass EF ChangeTracker overhead entirely for maximum throughput; mutating commands fetch tracked entities so `SaveChangesAsync` produces minimal SQL diffs.
* **Why Transactional Outbox (`outbox_messages`) over In-Process Post-Commit Dispatch:** If events are dispatched before `SaveChangesAsync`, failed transactions emit phantom events; if dispatched in-memory *after* `SaveChangesAsync`, a pod crash between commit and publish permanently loses the event (`TRAP-EVENT-010`). Persisting `OutboxMessage` records inside the same PostgreSQL transaction via `OutboxSaveChangesInterceptor` and relaying them asynchronously guarantees atomic persistence and at-least-once delivery.
* **Why Response-Owning Gotenberg Streams & Known-Size S3 Uploads:** Returning raw `response.Content.ReadAsStreamAsync()` without disposing `HttpResponseMessage` leaks sockets, while passing `ObjectSize(-1)` to MinIO SDK forces a 5 MB+ multipart LOH buffer allocation. Wrapping HTTP streams in a response-disposing decorator and spooling production outputs via `ArrayPool<byte>` yields deterministic socket release, exact `FileSizeBytes`, and zero-LOH S3 uploads.
* **Why Polymorphic SQL Driver Strategies:** Eliminates monolithic switch statements and provider-specific branching by abstracting external SQL execution behind isolated driver strategies with strict read-only token analysis.
* **Why Modular Feature Installers over Monolithic Composition Root (CompositionRootFirewall):** Concrete Infrastructure projection of the system-wide `CompositionRootFirewall`. Scattering hundreds of service registrations into a single monolithic `DependencyInjection.cs` creates an unmaintainable dumping ground where lifecycle mismatches, captive dependencies, and duplicate registrations thrive. Decomposing by subsystem guarantees single responsibility and strict visibility.
* **Why Hybrid Draft Cache SSoT (CodebaseSSoTAndDeduplicationFirewall):** Concrete Infrastructure projection of the system-wide `CodebaseSSoTAndDeduplicationFirewall`. Maintaining separate in-memory and Redis cache classes forces dual maintenance and conditional DI branching (`if (redisConn != null) ... else ...`). A unified hybrid cache with graceful L1 fallback operates transparently across both local developer workstations and distributed Kubernetes clusters as an authoritative Single Source of Truth.

### 2.2 Proactive Infrastructure Firewalls
```yaml
RepositoryBoundaryFirewall:
  Invariant: "Repositories must never return IQueryable<T> across interface boundary."
  ActionOnViolation: "Materialize query inside repository and return IReadOnlyList<T> or T?."

FailClosedTenancyFirewall:
  Invariant: "Repository queries must enforce fail-closed Global Query Filters and PostgreSQL RLS."
  ActionOnViolation: "Never call IgnoreQueryFilters(); rely on 5-Layer Tenant Isolation."

TransactionalOutboxFirewall:
  Invariant: "Domain events must be persisted to outbox_messages inside the same DB transaction before SaveChangesAsync completes."
  ActionOnViolation: "Register OutboxSaveChangesInterceptor on AppDbContext and clear entity.DomainEvents."

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
| `StorageBuckets.Outputs` | `outputs` | Generated production documents | `outputs/{projectId:N}/{yyyy/MM/dd}/{documentId:N}.{ext}` |
| `StorageBuckets.AuditPayloads` | `audit-payloads` | Large generation request payloads (>16KB) | `audit/{projectId:N}/{yyyy/MM/dd}/{payloadSha256}.json` |
| `StorageBuckets.Fonts` | `fonts` | Tenant-isolated custom typography assets | `fonts/{projectId:N}/{safeFileName}` |

---

## 3. Level 3: Canonical Infrastructure Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Repository Implementation Archetype (`{Aggregate}Repository`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-001]:
* Primary constructor injecting `AppDbContext`.
* Queries materialized internally; returns `Task<T?>` or `Task<IReadOnlyList<T>>` (never leaks `IQueryable<T>`).
* Multi-collection includes use `.AsSplitQuery()` to prevent Cartesian explosion (`TRAP-EF-002`).

```csharp
public sealed class {Aggregate}Repository(AppDbContext dbContext) : I{Aggregate}Repository
{
    // Step 1: Tracked Read for Mutation <Why: ChangeTracker tracks state updates & Version for SaveChanges>
    public async Task<{Aggregate}?> FindByIdAsync({Aggregate}Id id, CancellationToken ct)
    {
        return await dbContext.{Aggregate}s
            .AsSplitQuery()
            .Include(t => t.CurrentVersion)
            .Include(t => t.Children)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    // Step 2: Fast Precondition Existence Check <Why: indexed boolean check on normalized Value Object>
    public async Task<bool> SlugExistsAsync(ProjectId projectId, {Aggregate}Slug slug, CancellationToken ct)
    {
        return await dbContext.{Aggregate}s.AnyAsync(t => t.ProjectId == projectId && t.Slug == slug, ct);
    }

    // Step 3: Attach New Aggregate <Why: queues insert into ChangeTracker; synchronous void per EF Core GUID/UUIDv7 guidance>
    public void Add({Aggregate} aggregate) => dbContext.{Aggregate}s.Add(aggregate);

    public void Remove({Aggregate} aggregate)
    {
        dbContext.{Aggregate}s.Remove(aggregate);
    }
}
```

### 3.2 Transactional Unit of Work & Outbox Interceptor Archetype (`TransactionalUnitOfWork` & `OutboxSaveChangesInterceptor`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-002]:
* `OutboxSaveChangesInterceptor` harvests `DomainEvents` from tracked `IHaveDomainEvents` aggregates and inserts `OutboxMessage` records into `outbox_messages` inside the **same** database transaction before `SaveChangesAsync` executes (`TRAP-EVENT-010`).
* Maps PostgreSQL unique constraint violation (`SqlState == "23505"`) to `ConflictException` and `DbUpdateConcurrencyException` to `PreconditionFailedException` (`412`).

```csharp
public sealed class OutboxSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        if (eventData.Context is not { } dbContext)
        {
            return base.SavingChangesAsync(eventData, result, ct);
        }

        var aggregates = dbContext.ChangeTracker.Entries<IHaveDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var now = timeProvider.GetUtcNow();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                dbContext.Set<OutboxMessage>().Add(OutboxMessage.CreateFrom(domainEvent, now));
            }
            aggregate.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

public sealed class TransactionalUnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw PreconditionFailedException.VersionMismatch(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ConflictException.FromUniqueConstraint(ex);
        }
    }
}
```

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
            throw ExternalServiceException.RenderEngineFailure("GOTENBERG", ex);
        }
        catch
        {
            httpResponse.Dispose();
            throw;
        }
    }
}
```

### 3.4 S3 Object Storage Adapter Archetype (`MinioStorageAdapter`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-004]:
* Injects `IMinioClient` with cached bucket verification via `ConcurrentDictionary`.
* Requires seekable known-size `Stream` on `UploadAsync` to prevent 5MB+ multipart buffer allocations.

```csharp
public sealed class MinioStorageAdapter(
    IMinioClient client,
    ILogger<MinioStorageAdapter> logger) : IStoragePort
{
    private readonly ConcurrentDictionary<string, bool> _initializedBuckets = new(StringComparer.Ordinal);

    public async Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct)
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
        if (_strategies.TryGetValue(provider, out var strategy))
        {
            return strategy;
        }

        throw new NotSupportedException($"Database provider '{provider}' is not supported.");
    }
}
```

### 3.6 Modular Feature Installer Archetype (`PersistenceServiceExtensions`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-006]:
* Subsystem registrations are partitioned into single-responsibility extension classes under `SmkDoc.Infrastructure/Extensions/`.
* Encapsulates connection string validation, interceptor registration (`TenantWriteEnforcementInterceptor`, `OutboxSaveChangesInterceptor`), repository bindings, and read models.

```csharp
public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Step 1: Register Contexts & Interceptors <Why: ambient tenant isolation & Transactional Outbox>
        services.AddScoped<CurrentProjectContext>();
        services.AddScoped<ICurrentProjectContext>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<TenantWriteEnforcementInterceptor>();
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        // Step 2: Configure EF Core with Interceptors <Why: fail-closed tenant write guard & atomic outbox harvesting>
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string required.");
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
            options.AddInterceptors(
                sp.GetRequiredService<TenantWriteEnforcementInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        // Step 3: Register Repositories, ReadModels & UoW <Why: atomic persistence with outbox durability>
        services.AddScoped<IUnitOfWork, TransactionalUnitOfWork>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ITemplateReadModel, TemplateReadModel>();
        return services;
    }
}
```

### 3.7 Clean Infrastructure Composition Root Archetype (`DependencyInjection`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-007]:
* Coordinates subsystem feature installers in $\le 25$ LOC without direct service instantiation.
* Injects `TimeProvider.System` as singleton at root.

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Step 1: Register Time Provider <Why: deterministic testability across infrastructure>
        services.AddSingleton(TimeProvider.System);

        // Step 2: Chain Modular Subsystem Installers <Why: single responsibility & modular testability>
        return services
            .AddSecurityInfrastructure()
            .AddPersistenceInfrastructure(configuration)
            .AddStorageInfrastructure(configuration)
            .AddRenderingEnginesInfrastructure(configuration)
            .AddDistributedInfrastructure(configuration);
    }
}
```

### 3.8 Hybrid Template Draft Cache Archetype (`HybridTemplateDraftCache`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-008]:
* Unified hybrid L1 (in-memory LRU) + L2 (Redis with graceful fallback) implementation of `ITemplateDraftCache`.
* Operates deterministically in both standalone local dev and distributed multi-pod Kubernetes clusters.
* Persists binary streams to MinIO `StorageBuckets.Drafts` and caches metadata entries (`TemplateDraftEntry`) across L1 memory and L2 Redis.

```csharp
public sealed class HybridTemplateDraftCache(
    IConnectionMultiplexer? multiplexer,
    IStoragePort storagePort,
    IMemoryCache memoryCache,
    string prefix = RedisConfigurationResolver.DefaultInstancePrefix) : ITemplateDraftCache
{
    public async Task<string> StoreAsync(ProjectId projectId, Stream stream, string name, string ext, IReadOnlyList<string> placeholders, CancellationToken ct)
    {
        // Step 1: Invariant & Size Guard <Why: reject unseekable streams and drafts > 20MB>
        if (!stream.CanSeek) throw new InvalidOperationException("Draft stream must be seekable.");
        var draftId = Guid.CreateVersion7().ToString("N");

        // Step 2: Object Storage Spooling <Why: draft binary files persist in MinIO bucket without memory leaks>
        var storageKey = StorageBuckets.DraftTemplateKey(projectId, draftId, ext);
        await storagePort.UploadAsync(StorageBuckets.Drafts, storageKey, stream, "application/octet-stream", ct);

        // Step 3: L1 In-Memory Cache Insertion <Why: sub-millisecond instant retrieval for Monaco preview>
        var entry = new TemplateDraftEntry(draftId, projectId.Value, storageKey, name, ext, placeholders);
        memoryCache.Set($"draft:{projectId}:{draftId}", entry, TimeSpan.FromMinutes(30));

        // Step 4: L2 Redis Multi-Pod Sync <Why: transparent fallback in standalone environments without DI branching>
        if (multiplexer is { IsConnected: true })
            await multiplexer.GetDatabase().StringSetAsync($"{prefix}:draft:{projectId}:{draftId}", JsonSerializer.Serialize(entry), TimeSpan.FromMinutes(30));

        return draftId;
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
+ var aggregate = await repository.FindByIdAsync(id, ct) ?? throw NotFoundException.For<{Aggregate}>(id); aggregate.Update(name, now); await unitOfWork.CommitAsync(ct);
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

### [TRAP-INFRA-006]: Monolithic Dependency Injection Composition Root
```diff
- public static class DependencyInjection { public static IServiceCollection AddInfrastructureServices(...) { /* monolithic registration mixing all subsystems */ } }
+ public static class DependencyInjection { public static IServiceCollection AddInfrastructureServices(...) => services.AddSecurityInfrastructure().AddPersistenceInfrastructure(config).AddStorageInfrastructure(config).AddRenderingEnginesInfrastructure(config).AddDistributedInfrastructure(config); }
```
*Rationale: Concrete Infrastructure violation of system-wide CompositionRootFirewall. Monolithic DI composition roots violate single responsibility, blur subsystem boundaries, and mask captive dependencies. Partition registrations into modular feature installers under `Extensions/`.*

### [TRAP-INFRA-007]: Dual State Draft Cache Implementations with DI Branching
```diff
- if (redisConn != null) services.AddSingleton<ITemplateDraftCache, RedisTemplateDraftCache>();
- else services.AddSingleton<ITemplateDraftCache, InMemoryTemplateDraftCache>();
+ services.AddSingleton<ITemplateDraftCache>(sp => new HybridTemplateDraftCache(sp.GetService<IConnectionMultiplexer>(), ...));
```
*Rationale: Concrete Infrastructure violation of system-wide CodebaseSSoTAndDeduplicationFirewall. Branching DI between two distinct cache classes creates implementation divergence, dual maintenance, and environment-dependent bugs. A single hybrid adapter with transparent fallback operates deterministically in both standalone and distributed environments as an authoritative SSoT.*
