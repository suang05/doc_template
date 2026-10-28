# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & EF Core SSoT

<context>
Scope: Infrastructure Layer Patterns, EF Core Configurations, Strongly-Typed ID Converters, JSONB Mapping, Repositories, Tenancy Filter & S3 Storage
Target: `SmkDoc.Infrastructure/` (`Persistence/Configurations/`, `Repositories/`, `Storage/`, `Gotenberg/`)
Authority: Absolute SSoT Invariant (Hides all network and persistence I/O behind Application ports)
Invariants: Bounded Memory Streaming (<=81,920 buffer), Ambient Tenancy HasQueryFilter, xmin Concurrency Token
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_SCHEMA_MIGRATION], [SIGNAL_TOPOLOGY_MUTATION], [SIGNAL_SECURITY_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Persistence Invariants & Rules Matrix

### Architectural Wiring & Data Contracts
| Direction | Upstream Origin | Current Layer Role (Infrastructure Port Adapter) | Downstream Destination |
|---|---|---|---|
| **Port Inbound** | Application Port (`ITemplateRepository`, `IUnitOfWork`) | Implements port via EF Core `AppDbContext` | Database / Network Port |
| **Object Relational Mapping**| Domain POCOs (`Template`, `Document`) | Maps tables, columns (`snake_case`), and ID conversions | PostgreSQL 16+ Engine (`Npgsql`) |
| **Concurrency Safeguard**| Entity State Mutation | Tracks row version via PostgreSQL `xmin` | Optimistic Concurrency Check |
| **Storage Inbound** | Application Storage Port (`IStorageService`) | Pipes seekable known-size streams to S3 object storage | MinIO Cluster / AWS S3 Bucket |
| **Rendering Inbound**| Application Render Port (`IRenderEngine`) | Converts HTML/CSS via HTTP Chromium service | Gotenberg 8 Cluster (`/forms/chromium`) |
| **Port Outbound** | Database / Gotenberg / S3 Results | Returns domain entities or response-owning streams | Application UseCases (Zero LOH buffer) |

### Persistence Invariants & Rules Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Leaking Persistence** | Exposing EF Core `DbSet<T>` or `DbContext` outside | Hide behind domain repository interfaces (`ITemplateRepository`) |
| **Identity Storage** | Mapping strongly-typed IDs as raw `string` or object | Value converter to PostgreSQL native `uuid`: `HasConversion(id => id.Value, v => new TemplateId(v))` |
| **Client Child UUIDs** | Leaving child `Guid` keys as `ValueGeneratedOnAdd` when Domain generates UUIDv7 | Configure `builder.Property(e => e.Id).ValueGeneratedNever()` on all child entities (`TemplateVersion`, `DocumentVersion`, `FieldMapping`, `TemplateDataset`) |
| **Tenant Scoping** | Fail-open `!IsResolved || ...` or manual `WHERE` clauses | Fail-closed ambient Global Query Filter: `IsTenantResolved && e.ProjectId == CurrentTenantId` |
| **Child Tenant Filters** | Leaving child entities unfiltered when parent aggregate has global query filter (EF Warning 10622) | Matching query filters on child entities (`TemplateVersion`, `FieldMapping`, `TemplateDataset`, `DocumentVersion`) via parent navigation |
| **Concurrency Token** | Unprotected mutations susceptible to lost updates | PostgreSQL `xmin` system column mapped as row version token |
| **Relationship Cascade**| Blind `Cascade` deletes on production documents/logs | `DeleteBehavior.Restrict` or `DeleteBehavior.SetNull` per Level 2 matrix |
| **JSON Column Storage** | Storing structured payloads as plain escaped text | Native PostgreSQL `jsonb` column mapping |
| **IQueryable Leakage** | Leaking `IQueryable<T>` outside Infrastructure | Repositories evaluate queries internally; return `Task<IReadOnlyList<T>>` or `Task<T?>` |
| **Entity Tracking Policy**| Using `.AsNoTracking()` when fetching aggregates for mutation in Commands | Fetching with EF Tracking (`FindAsync` / query without `AsNoTracking`) in mutating UseCases |
| **MinIO Stream Sizing** | Passing `ObjectSize(-1)` on unseekable streams or calling `BucketExistsAsync` per upload | Spool unseekable outputs to `DeleteOnClose` temp `FileStream` for exact `Length`, and cache bucket initialization in `ConcurrentDictionary` |

---

## 2. Level 2: Core Persistence SSoT ASTs & Cognitive Infrastructure Engine

### Engineering Rationale & Architectural Decisions
* **Why EF Core `IEntityTypeConfiguration<T>` over DataAnnotations:** Keeps domain POCOs completely uncontaminated by ORM attributes, maintaining Clean Architecture purity.
* **Why `.ValueGeneratedNever()` on Child Entities:** When Domain factories pre-assign `Guid.CreateVersion7()` on a child entity appended to an already-tracked aggregate root (`document.CreateVersion`, `template.CreateDraftVersion`), EF Core 8/10 treats `ValueGeneratedOnAdd` non-empty keys as pre-existing `EntityState.Modified` rows and throws `DbUpdateConcurrencyException` on commit.
* **Why Fail-Closed Global Query Filter (`IsTenantResolved && ...`):** Fail-open filters (`!IsResolved || ...`) return every tenant's rows if a route or background job forgets to resolve `ICurrentProjectContext`; fail-closed guarantees zero rows leak when tenant resolution is absent.
* **Why PostgreSQL `xmin` over Timestamp Columns:** PostgreSQL manages `xmin` internally per transaction, guaranteeing true row-level concurrency without manual clock tracking or trigger overhead.
* **Why `.AsNoTracking()` Separation:** Query reads bypass EF ChangeTracker overhead entirely for maximum throughput; mutating commands fetch tracked entities so `SaveChangesAsync` produces minimal SQL diffs.
* **Why Response-Owning Gotenberg Streams & Known-Size S3 Uploads:** Returning raw `response.Content.ReadAsStreamAsync()` without disposing `HttpResponseMessage` leaks sockets, while passing `ObjectSize(-1)` to MinIO SDK forces a 5 MB+ multipart LOH buffer allocation. Wrapping HTTP streams in a response-disposing decorator and spooling production outputs via `ArrayPool<byte>` into a `FileOptions.DeleteOnClose` temp `FileStream` yields deterministic socket release, exact `FileSizeBytes` + `SHA-256`, and zero-LOH S3 uploads.

### Cognitive Infrastructure Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Bounded Memory Footprint & Absolute Persistence Isolation
  TradeOffPriority: MemorySafety (Zero-LOH) > TenantIsolation > ConcurrencyFidelity > DirectQueryPerformance

ProactiveInfraGuards:
  - Scope: SmkDoc.Infrastructure.Repositories
    Trigger: Method returning IQueryable<T> across interface boundary
    CanonicalAction: Materialize query asynchronously within repository and return IReadOnlyList<T> or T? >> Ref: [RULE-INFRA-002]

  - Scope: SmkDoc.Infrastructure.Persistence
    Trigger: Fail-open !IsResolved query filter or call to .IgnoreQueryFilters() in tenant routes
    CanonicalAction: Enforce fail-closed IsTenantResolved && e.ProjectId == CurrentTenantId >> Ref: [RULE-INFRA-003]

  - Scope: SmkDoc.Infrastructure.Persistence.Configurations
    Trigger: Child entity with Domain-generated Guid missing ValueGeneratedNever()
    CanonicalAction: Add builder.Property(e => e.Id).ValueGeneratedNever() >> Ref: [RULE-INFRA-001]

  - Scope: SmkDoc.Infrastructure.Gotenberg
    Trigger: Returning raw ReadAsStreamAsync without owning HttpResponseMessage disposal
    CanonicalAction: Wrap stream in ResponseDisposingStream with HttpCompletionOption.ResponseHeadersRead >> Ref: [RULE-INFRA-004]

  - Scope: SmkDoc.Infrastructure.Storage
    Trigger: Passing ObjectSize(-1) or calling BucketExistsAsync on every UploadAsync call
    CanonicalAction: Require known Stream.Length (via DeleteOnClose temp-file spool) and cache initialized buckets >> Ref: [RULE-INFRA-005]

CoreExtensionSlots:
  RepositoryAdapterSlot: IRepository<TAggregate, TId> (Pluggable entity persistence adapter)
  StorageServiceSlot: IStorageService (MinIO S3 / AWS S3 / Azure Blob Storage adapter)
  RenderingEngineSlot: IRenderEngine (Gotenberg Chromium / OpenXML Word / ClosedXML Excel)
```

### Referential Integrity & Cascade Delete Matrix
| Relationship Category | Target Entities | EF Core DeleteBehavior | Foreign Key Invariant |
|---|---|---|---|
| **Master Hierarchy** | `Company` => `Project` => `Template` => `TemplateVersion` | `DeleteBehavior.Cascade` | Child entities have no operational lifecycle outside parent aggregate root |
| **Audit & Legal Records** | `GenerationLog`, `AuditTrail`, `DocumentVersion` | `DeleteBehavior.Restrict` | Prohibit physical deletion of parent if audit records exist; prevents data falsification |
| **Optional References** | `Template.CurrentVersionId`, `User.LastActiveProjectId` | `DeleteBehavior.SetNull` | Nullable FK; decoupling lifecycle avoids circular cascade cascades |

### Object Storage Buckets SSoT Catalog (`StorageBuckets.cs`)
| Bucket Constant | Bucket Name | Target Content | Key Path Partitioning Scheme |
|---|---|---|---|
| `StorageBuckets.Templates` | `templates` | Immutable HTML/DOCX/XLSX template versions | `templates/{projectId:N}/{templateId:N}/v{version}{ext}` |
| `StorageBuckets.Outputs` | `outputs` | Generated production documents | `outputs/{projectId:N}/{yyyy/MM/dd}/{slug}_{generationId:N}.{ext}` |
| `StorageBuckets.AuditPayloads` | `audit-payloads` | Large generation request payloads (>16KB) | `audit/{projectId:N}/{yyyy/MM/dd}/{payloadSha256}.json` |
| `StorageBuckets.Fonts` | `fonts` | Tenant-isolated custom typography assets | `fonts/{projectId:N}/{safeFileName}` |

### Native PostgreSQL JSONB Mapping AST
```csharp
builder.Property(e => e.ExtractionSchema)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new());
```

---

## 3. Level 3: Canonical Infrastructure Archetypes

### Entity Configuration Archetype (`TemplateConfiguration`)
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-001]:
* [MUST] Implement `IEntityTypeConfiguration<TEntity>`.
* [MUST] Map table names and database column names strictly to lowercase `snake_case`.
* [MUST] Map strongly-typed IDs (`TemplateId`, `ProjectId`) and Value Objects using `.HasConversion(...)` to PostgreSQL native types (`uuid`, `varchar`).
* [MUST] Configure optimistic concurrency via PostgreSQL system column `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`.
* [MUST] Configure referential integrity delete behaviors explicitly per Level 2 matrix (prohibit unspecified default cascades).
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(e => e.Id);

        // Strongly-Typed ID Value Converters
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new TemplateId(v))
            .HasColumnName("id");

        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v))
            .HasColumnName("project_id")
            .IsRequired();

        // Validated Value Object Converters
        builder.Property(e => e.Name)
            .HasConversion(v => v.Value, v => TemplateName.Create(v))
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Slug)
            .HasConversion(s => s.Value, v => new TemplateSlug(v))
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        // PostgreSQL xmin Concurrency Token
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();

        builder.HasOne(e => e.CurrentVersion).WithMany().HasForeignKey(e => e.CurrentVersionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.FieldMappings).WithOne().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Repository Implementation Archetype (`TemplateRepository`)
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-002]:
* [MUST] Implement domain repository interface (`ITemplateRepository`) using C# 13 primary constructor injecting `AppDbContext`.
* [MUST] Materialize queries internally and return `Task<T?>` or `Task<IReadOnlyList<T>>` (strictly prohibit returning or leaking `IQueryable<T>`).
* [MUST] Apply `.AsNoTracking()` to all read-only queries to prevent EF Core ChangeTracker memory overhead.
* [MUST] Accept `CancellationToken ct = default` on every async query method and forward to EF Core async operators (`FirstOrDefaultAsync`, `ToListAsync`).
```csharp
using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Infrastructure.Repositories;

public sealed class TemplateRepository(AppDbContext dbContext) : ITemplateRepository
{
    public async Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct = default)
    {
        return await dbContext.Templates
            .Include(t => t.CurrentVersion)
            .Include(t => t.FieldMappings)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<Template>> ListByProjectIdAsync(ProjectId projectId, CancellationToken ct = default)
    {
        return await dbContext.Templates
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        return await dbContext.Templates.AnyAsync(t => t.Slug == slug, ct);
    }

    public async Task AddAsync(Template template, CancellationToken ct = default)
    {
        await dbContext.Templates.AddAsync(template, ct);
    }

    public void Update(Template template)
    {
        dbContext.Templates.Update(template);
    }
}
```

### Fail-Closed Ambient Multi-Tenant Global Query Filter
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-003]:
* [MUST] Apply fail-closed global query filter in `AppDbContext.OnModelCreating` to all entities implementing `IMustHaveProject`: `IsTenantResolved && e.ProjectId == CurrentTenantId`.
* [MUST] Apply matching child query filters on `TemplateVersion`, `FieldMapping`, `TemplateDataset`, and `DocumentVersion` via their parent aggregate navigation to satisfy EF Core warning `10622` and prevent cross-tenant child queries.
* [MUST NOT] Use fail-open expressions (`!IsResolved || ...`) or call `.IgnoreQueryFilters()` in tenant application routes.
```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence;

public partial class AppDbContext
{
    public ProjectId CurrentTenantId => _projectContext?.ProjectId ?? ProjectId.Empty;
    public bool IsTenantResolved => _projectContext?.IsResolved ?? false;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

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

        modelBuilder.Entity<TemplateVersion>()
            .HasQueryFilter(v => v.Template != null && IsTenantResolved && v.Template.ProjectId == CurrentTenantId);
        modelBuilder.Entity<FieldMapping>()
            .HasQueryFilter(m => m.Template != null && IsTenantResolved && m.Template.ProjectId == CurrentTenantId);
        modelBuilder.Entity<TemplateDataset>()
            .HasQueryFilter(td => td.Template != null && IsTenantResolved && td.Template.ProjectId == CurrentTenantId);
        modelBuilder.Entity<DocumentVersion>()
            .HasQueryFilter(dv => dv.Document != null && IsTenantResolved && dv.Document.ProjectId == CurrentTenantId);
    }

    private void ConfigureProjectFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveProject
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => IsTenantResolved && e.ProjectId == CurrentTenantId);
    }
}
```

### Zero-LOH Gotenberg HTTP Streaming Client Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-004]:
* [MUST] Use `HttpCompletionOption.ResponseHeadersRead` when sending conversion requests to Gotenberg.
* [MUST] Wrap `response.Content.ReadAsStreamAsync(ct)` in a `ResponseDisposingStream` so `HttpResponseMessage` is deterministically disposed when the caller closes the stream (and dispose `response` inside `catch` on early failure).
* [MUST] Forward `CancellationToken` to `SendAsync` and `ReadAsStreamAsync`.
```csharp
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmkDoc.Infrastructure.Pdf;

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

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            response.EnsureSuccessStatusCode();
            var innerStream = await response.Content.ReadAsStreamAsync(ct);
            return new ResponseDisposingStream(innerStream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
```

### S3 Object Storage Service Archetype (MinIO)
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-005]:
* [MUST] Implement `IStorageService` using MinIO S3 SDK with cached one-time bucket verification (`ConcurrentDictionary<string, bool>`).
* [MUST] Require seekable known-size `Stream` on `UploadAsync` (callers spool unseekable render streams via `FileOptions.DeleteOnClose | FileOptions.Asynchronous` temp `FileStream` so `ObjectSize` is exact and MinIO SDK never allocates 5 MB+ multipart LOH buffers).
* [MUST] Support prefix-scoped listing (`ListObjectsAsync`) for tenant-partitioned assets such as `fonts/{projectId:N}/`.
```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Storage;

public sealed class MinioStorageService(
    IMinioClient client,
    ILogger<MinioStorageService> logger) : IStorageService
{
    private readonly ConcurrentDictionary<string, bool> initializedBuckets = new(StringComparer.Ordinal);

    public async Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketInitializedOnceAsync(bucket, ct);

        if (!data.CanSeek)
            throw new InvalidOperationException("UploadAsync requires a seekable stream with known Length to prevent LOH multipart buffering.");

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

    public async Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        await EnsureBucketInitializedOnceAsync(bucket, ct);
        var keys = new List<string>();
        var args = new ListObjectsArgs().WithBucket(bucket).WithPrefix(prefix).WithRecursive(false);
        await foreach (var item in client.ListObjectsEnumAsync(args, ct))
        {
            if (!item.IsDir && !string.IsNullOrEmpty(item.Key))
                keys.Add(item.Key);
        }
        return keys;
    }

    private async Task EnsureBucketInitializedOnceAsync(string bucket, CancellationToken ct)
    {
        if (initializedBuckets.ContainsKey(bucket)) return;
        bool exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
        initializedBuckets.TryAdd(bucket, true);
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-INFRA-001]: IQueryable Boundary Leakage
```diff
- public IQueryable<Template> GetTemplates() => dbContext.Templates;
+ public async Task<IReadOnlyList<Template>> GetTemplatesAsync(ProjectId projectId, CancellationToken ct) => await dbContext.Templates.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);
```
*Rationale: Leaking `IQueryable` crosses layer boundaries, causing deferred execution outside transaction and unit-of-work scopes.*

### [TRAP-INFRA-002]: Multi-Tenant Isolation Bypass
```diff
- var templates = await dbContext.Templates.IgnoreQueryFilters().ToListAsync(ct);
+ var templates = await dbContext.Templates.ToListAsync(ct);
```
*Rationale: Disabling global query filters creates catastrophic IDOR cross-tenant data leaks in production endpoints.*

### [TRAP-EF-003]: Untracked Aggregate Mutation (Silent Save Loss)
```diff
- var template = await dbContext.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct); template.Update(name, cat, now); await unitOfWork.CommitAsync(ct);
+ var template = await templateRepo.GetByIdAsync(id, ct); template.Update(name, cat, now); await unitOfWork.CommitAsync(ct);
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
- builder.ToTable("templates"); builder.HasKey(e => e.Id);
+ builder.ToTable("templates"); builder.HasKey(e => e.Id); builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
```
*Rationale: Omitting PostgreSQL `xmin` concurrency tokens leaves entity records vulnerable to silent overwrite race conditions.*
