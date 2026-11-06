# [MECH-003]: database_persistence_and_schema_mechanics.md — Database Persistence, Schema Governance & Concurrency Mechanics SSoT

<context>
Scope: PostgreSQL Physical Schema Invariants, Table & Column Naming (`snake_case`), Primary Key Strategy (C# UUIDv7 with `.ValueGeneratedNever()`), Multi-Tenant Data Isolation (`IMustHaveProject` Global Query Filters & Matching Child Filters), Optimistic Concurrency Engine (`xmin` / `If-Match`), Composite Index Governance (`(project_id, slug)`, `(project_id, created_at DESC)`), Foreign Key Cascade Boundaries (`Cascade` for intra-aggregate vs `Restrict` for cross-aggregate), JSONB Storage & GIN Indexes, Strongly-Typed ID Value Converters, and Zero-Downtime Migration Protocols.
SSoT_For: Database Table Configurations (`IEntityTypeConfiguration<T>`), Global Query Filters, PostgreSQL Concurrency Tokens, Migration Lifecycle (`dotnet ef migrations`), and Database Seeding.
Out_Of_Scope: Infrastructure Port Adapters, Repositories, Unit of Work, Object Storage, and Gotenberg streaming clients (see: ../01_clean_arch_patterns/03_infrastructure_layer.md), Application UseCase orchestration (see: ../01_clean_arch_patterns/02_application_layer.md), HTTP wire contracts (see: ../00_architecture_core/06_api_contract_standards.md), Multi-tenant dual-auth middleware (see: multi_tenant_security_pipeline.md).
Target: `backend-v2/` (`SmkDoc.Infrastructure/Persistence/`, `Configurations/`, `Converters/`, `Migrations/`, `AppDbContext.cs`).
ArchitecturalSignals: [ARCH_EVOLUTION_DATABASE]
</context>

---

## 1. Level 1: Affirmative Database & Schema Invariants Matrix

### 1.1 Architectural Wiring & Data Persistence Flow
| Direction | Upstream Origin | Current Layer Role (PostgreSQL Persistence Core) | Downstream Destination |
|---|---|---|---|
| **Entity Mutation** | Application UseCase (`IUnitOfWork.CommitAsync`) | Translates Domain POCO to Relational Rows via EF Core | PostgreSQL Database Engine |
| **Tenant Boundary** | `ICurrentProjectContext` (`ProjectId`) | Applies Automatic Row-Level Query Filter (`IMustHaveProject`) | EF Core SQL Generation Pipeline |
| **Concurrency Safeguard**| HTTP Wire `If-Match` Header | Compares against PostgreSQL System Column `xmin` | PostgreSQL Transaction Engine |
| **Identity Assignment**| Domain Factory (`Guid.CreateVersion7()`) | Persists pre-allocated sequential UUID (`ValueGeneratedNever`) | Primary Key Column (`uuid`) |
| **Unstructured Data** | Complex JSON Payloads / Schemas | Serializes directly to binary JSON (`jsonb`) | Column Storage (`jsonb`) |
| **Audit Tracking** | Ambient Injected Time (`TimeProvider`) | Tracks creation and modification timestamps in UTC | Audit Columns (`created_at`, `updated_at`) |

### 1.2 Affirmative Database Schema & Storage Invariants
| Database Architectural Concern | Affirmative Database Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Physical Table & Column Naming**| Physical PostgreSQL database objects use plural `snake_case` tables (`templates`, `template_versions`) and singular `snake_case` columns (`project_id`, `created_at`). PascalCase in SQL DDL is prohibited. | Schema Naming |
| **Primary Key Generation** | Primary keys use sequential UUIDv7 instantiated in C# (`Guid.CreateVersion7()`) with `builder.Property(e => e.Id).ValueGeneratedNever()`. In-database identity/sequence generation is prohibited. | Identity Generation |
| **Temporal Storage Invariant** | All date/time columns use `timestamptz` (Timestamp with time zone) in UTC. Local timestamp types (`timestamp without time zone`) and client-supplied dates are prohibited. | Temporal Storage |
| **Multi-Tenant Row Isolation**| All tenant-scoped entities implement `IMustHaveProject` with automated Global Query Filter (`e.ProjectId == CurrentTenantId`). Unfiltered tenant data queries are prohibited. | Multi-Tenancy |
| **Child Filter Alignment** | All child entities in an aggregate declare matching Global Query Filters through navigation properties (`v.Template != null && v.Template.ProjectId == CurrentTenantId`) to eliminate EF Core warning 10622. | Query Filter Integrity |
| **Composite Unique Indexes** | Unique constraints on tenant-scoped entities combine `(project_id, slug)` or `(project_id, identifier)`. Global unique indexes on tenant entities are prohibited. | Index Strategy |
| **Cascade Boundary Rules** | `DeleteBehavior.Cascade` is strictly restricted to owned child entities within the same aggregate root (`Template` -> `TemplateVersion`). Cross-aggregate foreign keys enforce `DeleteBehavior.Restrict`. | Referential Integrity |
| **Optimistic Concurrency Engine**| State-mutating tables map the PostgreSQL system column `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`. Timestamp rowversion columns are prohibited. | Concurrency Control |
| **Unstructured Document Storage**| Dynamic schemas, layout trees, and JSON bindings utilize PostgreSQL `jsonb` with optional GIN indexes for queried keys. Relational decomposition of volatile schema trees is prohibited. | Document Storage |
| **Zero-Downtime Migration Policy**| Database migrations follow the Expand and Contract pattern: adding nullable columns first, backfilling, and dropping columns in a subsequent deployment. Breaking single-phase column drops are prohibited. | Migration Lifecycle |

### 1.3 PostgreSQL Column Mapping & Data Type Taxonomy
| Domain / C# Type | PostgreSQL Physical Type | EF Core Mapping Snippet | Primary Architectural Rationale |
|---|---|---|---|
| Strongly-Typed ID (`ProjectId`) | `uuid` | `.HasConversion(DomainValueConverters.ProjectIdConverter)` | 16-byte binary UUIDv7; sequential, time-sortable, zero lock contention |
| Monomorphic String (`Slug`) | `varchar(100)` | `.HasMaxLength(100).IsRequired()` | Bounded length string for indexed query parameters |
| Dynamic Content (`DataSchema`)| `jsonb` | `.HasColumnType("jsonb")` | Binary structured JSON allowing native PostgreSQL path querying |
| Concurrency Token (`xmin`) | `xid` | `.Property<uint>("xmin").HasColumnType("xid").IsConcurrencyToken()` | PostgreSQL internal transaction ID; automatically updated per row mutation |
| Audit Timestamp (`CreatedAt`) | `timestamptz` | `.HasDefaultValueSql("NOW()")` | Absolute UTC timestamp with microsecond precision |
| Lifecycle Flag (`IsActive`) | `boolean` | `.HasDefaultValue(true)` | Soft-deactivation flag for non-destructive entity retirement |

---

## 2. Level 2: Core Data Architecture & Cognitive Rationale

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Application UUIDv7 over Database Sequences:** Sequential UUIDv7 generated in C# provides chronological time-ordering and index locality in B-Trees (eliminating page splits), allows ID allocation before persistence (pure domain event emission), and eliminates database roundtrips required by database sequence triggers.
* **Why PostgreSQL `xmin` over Dedicated RowVersion Columns:** PostgreSQL manages `xmin` natively at the engine tuple header level. Mapping `xmin` as a concurrency token provides bulletproof Optimistic Concurrency Control without application-managed counter columns, triggers, or race conditions.
* **Why `snake_case` in SQL vs `camelCase` on the Wire:** PostgreSQL folds unquoted identifiers to lowercase; `snake_case` provides natural readability in raw SQL queries, pgAdmin, and monitoring scripts, while ASP.NET Core EF Core conventions handle the mapping transparently.
* **Why `DeleteBehavior.Cascade` is Prohibited Across Aggregates:** Accidental deletion of a `User` or `Project` must never automatically wipe business documents, templates, or audit logs via hidden database cascades. Cross-aggregate links must enforce `DeleteBehavior.Restrict` or `SetNull`.
* **Why Matching Child Filters are Mandatory:** When an aggregate root has an EF Core Global Query Filter, querying child entities directly (or via navigation properties) without an equivalent filter bypasses tenant security or triggers EF Core warning 10622 (Required navigation filter mismatch).

### 2.2 Proactive Database Schema Firewalls
```yaml
TableNamingFirewall:
  Invariant: "All database tables must be plural snake_case; columns must be singular snake_case."
  ActionOnViolation: "Configure builder.ToTable('plural_snake_case') and check column names."

PrimaryKeyGenerationFirewall:
  Invariant: "Entities must use C# UUIDv7 with ValueGeneratedNever()."
  ActionOnViolation: "Add builder.Property(e => e.Id).ValueGeneratedNever() in EntityTypeConfiguration."

CascadeBoundaryFirewall:
  Invariant: "Cross-aggregate foreign keys must use DeleteBehavior.Restrict."
  ActionOnViolation: "Change OnDelete(DeleteBehavior.Cascade) to OnDelete(DeleteBehavior.Restrict)."

MultiTenantIndexFirewall:
  Invariant: "Unique indexes on tenant entities must include ProjectId."
  ActionOnViolation: "Change HasIndex(e => e.Slug) to HasIndex(e => new { e.ProjectId, e.Slug })."

ConcurrencyTokenFirewall:
  Invariant: "Mutable tables must map PostgreSQL system column xmin as concurrency token."
  ActionOnViolation: "Add builder.Property<uint>('xmin').HasColumnType('xid').IsConcurrencyToken()."

MigrationExpandContractFirewall:
  Invariant: "Never drop or rename a column in a single migration pass while old code is running."
  ActionOnViolation: "Add new column as nullable; backfill data; drop old column in subsequent deployment."
```

### 2.3 Multi-Tenant Isolation & Filter Inheritance Taxonomy
Every entity in the database belongs to one of three multi-tenant tiers:

| Tenancy Tier | Identifying Contract | EF Core Global Query Filter | Referential Rule |
|---|---|---|---|
| **Tier 1: Tenant-Root Aggregate** | Implements `IMustHaveProject` | `e.ProjectId == CurrentTenantId` | Foreign key to `projects` table (`DeleteBehavior.Cascade` allowed only from Project) |
| **Tier 2: Child Member of Tenant Aggregate** | Navigates to Parent Aggregate | `v.Parent != null && v.Parent.ProjectId == CurrentTenantId` | Foreign key to Parent (`DeleteBehavior.Cascade`); matches Parent tenant filter |
| **Tier 3: Platform Governance Entity** | Does NOT implement `IMustHaveProject` | Unfiltered (Access governed strictly by `RequireSuperAdmin`) | Global entities (`projects`, `users`, `companies`, `platform_api_keys`) |

### 2.4 Zero-Downtime Migration & Evolution Protocol
1. **Migration Naming Standard**: Name migrations using PascalCase with descriptive intent prefixed by domain: `AddTemplateCategoryIndex`, `IntroduceDocumentAsyncRenderQueue`, `MigrateDataConnectionCredentialsToJsonb`.
2. **EF Core CLI Command**:
   ```bash
   dotnet ef migrations add <MigrationName> \
       --project backend-v2/src/SmkDoc.Infrastructure \
       --startup-project backend-v2/src/SmkDoc.Api \
       --output-dir Persistence/Migrations
   ```
3. **Expand-and-Contract Migration Phases**:
   * **Phase 1 (Expand)**: Add new column as nullable in EF Core; deploy backend.
   * **Phase 2 (Dual-Write)**: Application writes to both old and new columns; run background backfill script.
   * **Phase 3 (Contract)**: Update code to read exclusively from new column; make column non-nullable.
   * **Phase 4 (Purge)**: Safe drop of obsolete column in a dedicated migration.

---

## 3. Level 3: Canonical Persistence Archetypes (Surgical AST Recipes)

### 3.1 Primary Aggregate Configuration Archetype (`IEntityTypeConfiguration<{Aggregate}>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-DB-001]:
* Implements `IEntityTypeConfiguration<{Aggregate}>`.
* Sets plural `snake_case` table name.
* Configures sequential UUIDv7 with `.ValueGeneratedNever()`.
* Enforces composite unique index `(ProjectId, Slug)` and optimistic concurrency token via PostgreSQL `xmin`.

```csharp
public sealed class {Aggregate}Configuration : IEntityTypeConfiguration<{Aggregate}>
{
    public void Configure(EntityTypeBuilder<{Aggregate}> builder)
    {
        // Step 1: Physical Table & Key <Why: snake_case schema and application UUIDv7>
        builder.ToTable("{aggregate-plural-snake}");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever()
            .HasConversion(id => id.Value, v => new {Aggregate}Id(v));

        // Step 2: Tenant Foreign Key & Composite Index <Why: tenant partition uniqueness>
        builder.Property(e => e.ProjectId)
            .HasConversion(DomainValueConverters.ProjectIdConverter);
        builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();

        // Step 3: Scalar Invariants & Conversions <Why: strongly-typed value objects>
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100)
            .HasConversion(v => v.Value, v => {Aggregate}Name.Create(v));
        builder.Property(e => e.Slug).IsRequired().HasMaxLength(100)
            .HasConversion(s => s.Value, v => new {Aggregate}Slug(v));
        builder.Property(e => e.Category).HasMaxLength(50);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        // Step 4: Audit Timestamps in UTC <Why: temporal traceability>
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        // Step 5: Concurrency Token via PostgreSQL xmin <Why: zero-overhead optimistic lock>
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Step 6: Referential Integrity <Why: cascade only from parent project>
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 3.2 Child Member Configuration Archetype (`IEntityTypeConfiguration<{ChildEntity}>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-DB-002]:
* Configures child entity belonging to an aggregate.
* Enforces `DeleteBehavior.Cascade` strictly from parent aggregate root.
* Maps complex unstructured properties to `jsonb`.

```csharp
public sealed class {Aggregate}VersionConfiguration : IEntityTypeConfiguration<{Aggregate}Version>
{
    public void Configure(EntityTypeBuilder<{Aggregate}Version> builder)
    {
        // Step 1: Table & Pre-Allocated Identity <Why: avoid sequence roundtrips>
        builder.ToTable("{aggregate-singular-snake}_versions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Step 2: Parent Aggregate Relationship <Why: aggregate-level cascade boundary>
        builder.Property(e => e.{Aggregate}Id)
            .HasConversion(id => id.Value, v => new {Aggregate}Id(v));
        builder.HasIndex(e => new { e.{Aggregate}Id, e.Version }).IsUnique();

        // Step 3: Unstructured JSONB Payloads <Why: native PostgreSQL binary JSON>
        builder.Property(e => e.DataSchema).HasColumnType("jsonb");
        builder.Property(e => e.SamplePayload).HasColumnType("jsonb");
        builder.Property(e => e.MappingsSnapshot).HasColumnType("jsonb");

        // Step 4: Metadata & Audit <Why: version commit history>
        builder.Property(e => e.CommitMessage).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        // Step 5: Enforce Intra-Aggregate Cascade <Why: versions die with aggregate root>
        builder.HasOne(e => e.{Aggregate})
            .WithMany(t => t.Versions)
            .HasForeignKey(e => e.{Aggregate}Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 3.3 Multi-Tenant Global Query Filter Configuration in `AppDbContext`
>> MANDATORY CANONICAL BLUEPRINT [RULE-DB-003]:
* Scans assembly for `IMustHaveProject` implementations.
* Configures automatic tenant filtering on root entities.
* Configures matching child query filters to eliminate EF Core warning 10622.

```csharp
public sealed class AppDbContext : DbContext
{
    private static readonly MethodInfo ConfigureProjectFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureProjectFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Step 1: Apply all IEntityTypeConfiguration classes from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Step 2: Automatic Multi-Tenant Filter for Root IMustHaveProject entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType != null && typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
            {
                ConfigureProjectFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }

        // Step 3: Matching Filters for Aggregate Child Entities (Eliminates EF Core 10622)
        modelBuilder.Entity<{Aggregate}Version>()
            .HasQueryFilter(v => v.{Aggregate} != null && IsTenantResolved && v.{Aggregate}.ProjectId == CurrentTenantId);
    }

    private void ConfigureProjectFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveProject
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => IsTenantResolved && e.ProjectId == CurrentTenantId);
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-DB-001]: Global Unique Index on Multi-Tenant Entity (Missing ProjectId)
```diff
- builder.HasIndex(e => e.Slug).IsUnique();
+ builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();
```
*Rationale: A global unique index on slug prevents different tenants from using the same slug (e.g. 'invoice'), violating multi-tenant isolation.*

### [TRAP-DB-002]: In-Database ID Generation via Sequence/Identity
```diff
- builder.Property(e => e.Id).ValueGeneratedOnAdd();
+ builder.Property(e => e.Id).ValueGeneratedNever(); // Pre-allocated UUIDv7 in C#
```
*Rationale: ValueGeneratedOnAdd requires database roundtrips to obtain generated keys, prevents assigning IDs in Domain Factories, and complicates domain event harvesting.*

### [TRAP-DB-003]: Accidental Cascading Delete Across Aggregate Boundaries
```diff
- builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
+ builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
```
*Rationale: Cascading deletes across aggregate roots cause catastrophic data loss when a shared entity (e.g. User or Project) is deactivated or deleted.*

### [TRAP-DB-004]: Incomplete Global Query Filter on Child Entities (EF Core Warning 10622)
```diff
- // No query filter configured for {Aggregate}Version
+ modelBuilder.Entity<{Aggregate}Version>().HasQueryFilter(v => v.{Aggregate} != null && IsTenantResolved && v.{Aggregate}.ProjectId == CurrentTenantId);
```
*Rationale: Missing matching query filters on child entities bypasses tenant isolation during direct child queries and causes EF Core 10622 navigation filter mismatch warnings.*

### [TRAP-DB-005]: Timestamp-Based Concurrency Token instead of PostgreSQL `xmin`
```diff
- builder.Property(e => e.RowVersion).IsRowVersion();
+ builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
```
*Rationale: Application-managed row version or timestamp columns are vulnerable to clock skew and require extra database columns, whereas PostgreSQL `xmin` is updated atomically by the storage engine.*

### [TRAP-DB-006]: Destructive Single-Step Column Drop Breaking Active Application Pods
```diff
- migrationBuilder.DropColumn(name: "old_column", table: "templates");
+ // Phase 1: Deploy nullable new_column; Dual-write in code.
+ // Phase 2: Backfill data.
+ // Phase 3: Drop old_column in a subsequent migration after all pods are running Phase 2 code.
```
*Rationale: Dropping a column immediately in a rolling Kubernetes deployment crashes older pods that still expect the column to exist.*
