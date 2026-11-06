# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & EF Core SSoT

<context>
Scope: Infrastructure Layer Patterns, EF Core Configurations, Strongly-Typed ID Converters, JSONB Mapping, Repositories, Tenancy Query Filters, S3 Storage, Gotenberg Streaming, and SQL Driver Strategy.
SSoT_For: Infrastructure Port Adapters, EF Core Mapping, Value Converters, AppDbContext ModelSnapshot, S3 Object Storage, Redis Distributed State, and Polymorphic SQL Drivers.
Out_Of_Scope: Application UseCase orchestration (see: 02_application_layer.md), Pure Domain Entities (see: 01_domain_layer.md), HTTP Controllers (see: 04_api_presentation_layer.md), Auth middleware pipeline (see: ../03_specialized_mechanics/multi_tenant_security_pipeline.md).
Target: `SmkDoc.Infrastructure/` (`Persistence/Configurations/`, `Repositories/`, `Storage/`, `Gotenberg/`, `Data/Drivers/`).
ArchitecturalSignals: [ARCH_EVOLUTION_INFRASTRUCTURE]
</context>

---

## 1. Level 1: Affirmative Infrastructure Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Infrastructure Port Adapter) | Downstream Destination |
|---|---|---|---|
| **Port Inbound** | Application Port (`I{Aggregate}Repository`, `IUnitOfWork`) | Implements port contract via EF Core `AppDbContext` | Database / Network Socket |
| **Object Relational Mapping**| Domain POCOs (`{Aggregate}`, `{ChildEntity}`) | Maps tables, columns (`snake_case`), and ID conversions | PostgreSQL 16+ Engine (`Npgsql`) |
| **Concurrency Safeguard**| Entity State Mutation | Tracks row version via PostgreSQL `xmin` | Optimistic Concurrency Check |
| **Storage Inbound** | Application Storage Port (`IStorageService`) | Pipes seekable known-size streams to S3 object storage | MinIO Cluster / AWS S3 Bucket |
| **Rendering Inbound**| Application Render Port (`IRenderEngine`) | Converts HTML/CSS via HTTP Chromium service | Gotenberg 8 Cluster (`/forms/chromium`) |
| **Port Outbound** | Database / Gotenberg / S3 Results | Returns domain entities or response-owning streams | Application UseCases (Zero LOH buffer) |

### 1.2 Affirmative Persistence & Infrastructure Invariants
| Infrastructure Concern | Affirmative Infrastructure Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Persistence Encapsulation** | Hide `DbSet<T>` and `DbContext` strictly behind domain repository interfaces (`I{Aggregate}Repository`). Prohibit leaking ORM contexts outside Infrastructure. | Data Layer Boundary |
| **Native Identity Storage** | Map strongly-typed IDs via `.HasConversion(id => id.Value, v => new {Aggregate}Id(v))` to PostgreSQL native `uuid` columns. | Column Type Mapping |
| **Child Identity Generation** | Configure `builder.Property(e => e.Id).ValueGeneratedNever()` on all child entities with domain-generated UUIDv7 keys (`{Aggregate}Version`, `{ChildEntity}`). | Identity Lifecycle |
| **Fail-Closed Tenant Isolation**| Global Query Filter strictly enforces `IsTenantResolved && e.ProjectId == CurrentTenantId` on all entities implementing `IMustHaveProject`. Prohibit fail-open expressions (`!IsResolved || ...`). | Tenancy Isolation |
| **Child Filter Propagation** | Configure matching parent-navigation query filters on child entities (`v.{Aggregate} != null && IsTenantResolved && v.{Aggregate}.ProjectId == CurrentTenantId`) to eliminate EF Warning 10622. | Relational Tenancy |
| **Optimistic Concurrency Token**| Map PostgreSQL system column `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`. | Concurrency Safety |
| **Explicit Referential Actions**| Configure referential actions explicitly per Referential Integrity Matrix (`DeleteBehavior.Restrict`, `DeleteBehavior.SetNull`, `DeleteBehavior.Cascade`). Prohibit unspecified cascades. | Foreign Key Constraints |
| **Native JSONB Storage** | Map structured document schemas and payload dictionaries via `.HasColumnType("jsonb")` with strongly-typed `JsonSerializerOptions` converters. | Semi-Structured Data |
| **Internal Query Evaluation** | Repositories materialize queries internally and return `Task<T?>` or `Task<IReadOnlyList<T>>`. Prohibit leaking `IQueryable<T>` across interface boundaries. | Interface Segregation |
| **Tracking Partitioning** | Read-only queries apply `.AsNoTracking()` and project scalar SQL columns (`I{Aggregate}QueryService`); mutating commands fetch tracked entities for ChangeTracker updates. | Memory & Throughput |
| **Composite Index Parity** | Maintain composite unique indexes `(ProjectId, Slug)` and descending indexes `(ProjectId, CreatedAt DESC)` at 100% parity with `AppDbContextModelSnapshot`. | Database Performance |
| **Zero-LOH Stream Sizing** | Object storage uploads require seekable streams with known `Stream.Length` spooled via `HybridSpoolStream`; initialized buckets are cached in `ConcurrentDictionary`. | Storage Gateway |
| **Distributed State Topology** | Redis stores utilize atomic Lua scripts with fail-closed semantics for idempotency (`503`), fail-open for result caches, and `pg_advisory_lock` for multi-pod migrations. | Multi-Pod Scalability |
| **Polymorphic SQL Execution** | Execute external SQL datasets via `ISqlDriverFactory` resolving provider-specific strategies (`PostgresDriverStrategy`, `SqlServerDriverStrategy`) with AST and transaction read-only isolation. | Data Federation |

---

## 2. Level 2: Core Persistence SSoT ASTs & Cognitive Infrastructure Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why EF Core `IEntityTypeConfiguration<T>` over DataAnnotations:** Keeps domain POCOs completely uncontaminated by ORM attributes, maintaining Clean Architecture purity.
* **Why `.ValueGeneratedNever()` on Child Entities:** When Domain factories pre-assign `Guid.CreateVersion7()` on a child entity appended to an already-tracked aggregate root, EF Core treats `ValueGeneratedOnAdd` non-empty keys as pre-existing `EntityState.Modified` rows and throws `DbUpdateConcurrencyException` on commit.
* **Why Fail-Closed Global Query Filter (`IsTenantResolved && ...`):** Fail-open filters (`!IsResolved || ...`) return every tenant's rows if a route or background job forgets to resolve `ICurrentProjectContext`; fail-closed guarantees zero rows leak when tenant resolution is absent.
* **Why PostgreSQL `xmin` over Timestamp Columns:** PostgreSQL manages `xmin` internally per transaction, guaranteeing true row-level concurrency without manual clock tracking or trigger overhead.
* **Why `.AsNoTracking()` Separation:** Query reads bypass EF ChangeTracker overhead entirely for maximum throughput; mutating commands fetch tracked entities so `SaveChangesAsync` produces minimal SQL diffs.
* **Why Response-Owning Gotenberg Streams & Known-Size S3 Uploads:** Returning raw `response.Content.ReadAsStreamAsync()` without disposing `HttpResponseMessage` leaks sockets, while passing `ObjectSize(-1)` to MinIO SDK forces a 5 MB+ multipart LOH buffer allocation. Wrapping HTTP streams in a response-disposing decorator and spooling production outputs via `ArrayPool<byte>` into a `FileOptions.DeleteOnClose` temp `FileStream` yields deterministic socket release, exact `FileSizeBytes`, and zero-LOH S3 uploads.

### 2.2 Proactive Infrastructure Firewalls
```yaml
RepositoryBoundaryFirewall:
  Invariant: "Repositories must never return IQueryable<T> across interface boundary."
  ActionOnViolation: "Materialize query inside repository and return IReadOnlyList<T> or T?."

FailClosedTenancyFirewall:
  Invariant: "Global query filter must use fail-closed IsTenantResolved && e.ProjectId == CurrentTenantId."
  ActionOnViolation: "Remove !IsResolved or IgnoreQueryFilters() calls in tenant application routes."

ChildEntityKeyGenerationFirewall:
  Invariant: "Child entities with Domain-generated UUIDv7 keys must declare ValueGeneratedNever()."
  ActionOnViolation: "Add builder.Property(e => e.Id).ValueGeneratedNever() in EntityTypeConfiguration."

GotenbergSocketLeakFirewall:
  Invariant: "Gotenberg HTTP stream responses must be wrapped in ResponseDisposingStream."
  ActionOnViolation: "Wrap ReadAsStreamAsync() with ResponseDisposingStream and ResponseHeadersRead."

S3StreamSizeFirewall:
  Invariant: "UploadAsync requires known stream length to avoid MinIO 5MB multipart LOH buffers."
  ActionOnViolation: "Spool unseekable streams through HybridSpoolStream before calling UploadAsync."
```

### 2.3 Referential Integrity & Cascade Delete Matrix
| Relationship Category | Target Entities | EF Core DeleteBehavior | Foreign Key Invariant |
|---|---|---|---|
| **Master Hierarchy** | `Company` => `Project` => `{Aggregate}` => `{ChildEntity}` | `DeleteBehavior.Cascade` | Child entities have no operational lifecycle outside parent aggregate root |
| **Audit & Legal Records** | `GenerationLog`, `AuditTrail`, `DocumentVersion` | `DeleteBehavior.Restrict` | Prohibit physical deletion of parent if audit records exist; prevents data falsification |
| **Optional References** | `{Aggregate}.CurrentVersionId`, `User.LastActiveProjectId` | `DeleteBehavior.SetNull` | Nullable FK; decoupling lifecycle avoids circular cascade cascades |

### 2.4 Object Storage Buckets SSoT Catalog (`StorageBuckets.cs`)
| Bucket Constant | Bucket Name | Target Content | Key Path Partitioning Scheme |
|---|---|---|---|
| `StorageBuckets.Templates` | `templates` | Immutable HTML/DOCX/XLSX template versions | `templates/{projectId:N}/{templateId:N}/v{version}{ext}` |
| `StorageBuckets.Drafts` | `drafts` | Temporary tenant-isolated draft binaries prior to commit | `drafts/{projectId:N}/{draftId}{ext}` |
| `StorageBuckets.Outputs` | `outputs` | Generated production documents | `outputs/{projectId:N}/{yyyy/MM/dd}/{slug}_{generationId:N}.{ext}` |
| `StorageBuckets.AuditPayloads` | `audit-payloads` | Large generation request payloads (>16KB) | `audit/{projectId:N}/{yyyy/MM/dd}/{payloadSha256}.json` |
| `StorageBuckets.Fonts` | `fonts` | Tenant-isolated custom typography assets | `fonts/{projectId:N}/{safeFileName}` |

### 2.5 Native PostgreSQL JSONB Mapping AST
```csharp
builder.Property(e => e.ExtractionSchema)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new());
```

---

## 3. Level 3: Canonical Infrastructure Archetypes (Surgical AST Recipes)

### 3.1 Entity Configuration Archetype (`{Aggregate}Configuration`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-001]:
* Implements `IEntityTypeConfiguration<{Aggregate}>`.
* Maps tables and column names strictly to lowercase `snake_case`.
* Value converters map strongly-typed IDs and Value Objects to PostgreSQL native types (`uuid`, `varchar`).
* Concurrency token mapped via PostgreSQL system column `xmin`.

```csharp
public sealed class {Aggregate}Configuration : IEntityTypeConfiguration<{Aggregate}>
{
    public void Configure(EntityTypeBuilder<{Aggregate}> builder)
    {
        // Step 1: Table & Key Mapping <Why: explicit snake_case relational schema>
        builder.ToTable("{aggregate_table_name}");
        builder.HasKey(e => e.Id);

        // Step 2: Strongly-Typed ID Converters <Why: compile-time type safety stored as native UUID>
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new {Aggregate}Id(v))
            .HasColumnName("id");

        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v))
            .HasColumnName("project_id")
            .IsRequired();

        // Step 3: Validated Value Object Converters <Why: encapsulated domain types mapped to varchar>
        builder.Property(e => e.Name)
            .HasConversion(v => v.Value, v => {Aggregate}Name.Create(v))
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Slug)
            .HasConversion(s => s.Value, v => new {Aggregate}Slug(v))
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        // Step 4: Scalar Properties & Auditing <Why: audit trail with database-level timestamps>
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        // Step 5: Optimistic Concurrency Token <Why: prevent silent overwrite race conditions>
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // Step 6: Composite Indexes <Why: tenant-isolated fast slug lookup>
        builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();

        // Step 7: Referential Integrity <Why: prevent orphaned records per delete behavior matrix>
        builder.HasOne(e => e.CurrentVersion).WithMany().HasForeignKey(e => e.CurrentVersionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Children).WithOne().HasForeignKey(e => e.{Aggregate}Id).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 3.2 Repository Implementation Archetype (`{Aggregate}Repository`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-002]:
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

### 3.3 Transactional Unit of Work Archetype (`TransactionalUnitOfWork`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-003]:
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

### 3.4 Fail-Closed Ambient Multi-Tenant Global Query Filter
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-004]:
* Applies fail-closed query filter: `IsTenantResolved && e.ProjectId == CurrentTenantId`.
* Applies matching child query filters via parent navigation to eliminate EF Warning 10622.

```csharp
public partial class AppDbContext
{
    public ProjectId CurrentTenantId => _projectContext?.ProjectId ?? ProjectId.Empty;
    public bool IsTenantResolved => _projectContext?.IsResolved ?? false;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Step 1: Automated Ambient Tenancy Filter <Why: fail-closed zero-leak tenant isolation>
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType != null && typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ConfigureProjectFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, [modelBuilder]);
            }
        }

        // Step 2: Child Entity Navigation Filters <Why: satisfy EF Warning 10622 and secure joined reads>
        modelBuilder.Entity<{ChildEntity}>()
            .HasQueryFilter(v => v.{Aggregate} != null && IsTenantResolved && v.{Aggregate}.ProjectId == CurrentTenantId);
    }

    private void ConfigureProjectFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveProject
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => IsTenantResolved && e.ProjectId == CurrentTenantId);
    }
}
```

### 3.5 Zero-LOH Gotenberg HTTP Streaming Client Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-005]:
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

### 3.6 S3 Object Storage Service Archetype (MinIO)
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-006]:
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

### 3.7 Polymorphic SQL Driver Strategy Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-INFRA-007]:
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

### [TRAP-INFRA-002]: Multi-Tenant Isolation Bypass
```diff
- var aggregates = await dbContext.{Aggregate}s.IgnoreQueryFilters().ToListAsync(ct);
+ var aggregates = await dbContext.{Aggregate}s.ToListAsync(ct);
```
*Rationale: Disabling global query filters creates catastrophic IDOR cross-tenant data leaks in production endpoints.*

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

### [TRAP-INFRA-005]: Missing Concurrency Token Configuration
```diff
- builder.ToTable("{table_name}"); builder.HasKey(e => e.Id);
+ builder.ToTable("{table_name}"); builder.HasKey(e => e.Id); builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
```
*Rationale: Omitting PostgreSQL `xmin` concurrency tokens leaves entity records vulnerable to silent overwrite race conditions.*

### [TRAP-INFRA-006]: Unseekable Unknown-Size S3 Upload (LOH Multipart Buffer Trap)
```diff
- var args = new PutObjectArgs().WithStreamData(stream).WithObjectSize(-1);
+ if (!stream.CanSeek) throw new InvalidOperationException("Seekable stream required."); var args = new PutObjectArgs().WithStreamData(stream).WithObjectSize(stream.Length - stream.Position);
```
*Rationale: Passing `ObjectSize(-1)` forces the S3 SDK to allocate 5MB+ memory chunks in Large Object Heap (LOH) to perform multipart buffering.*
