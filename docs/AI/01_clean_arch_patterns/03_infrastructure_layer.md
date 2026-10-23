# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & EF Core SSoT

<context>
Scope: Infrastructure Layer Patterns, EF Core Configurations, Strongly-Typed ID Converters, JSONB Mapping, Repositories & Tenancy Filter
Target: `SmkDoc.Infrastructure/` (`Persistence/Configurations/`, `Repositories/`, `Storage/`, `Gotenberg/`)
Authority: Absolute SSoT Invariant (Hides all network and persistence I/O behind Application ports)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_SCHEMA_MIGRATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Persistence Invariants & Rules Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Leaking Persistence** | Exposing EF Core `DbSet<T>` or `DbContext` outside | Hide behind domain repository interfaces (`ITemplateRepository`) |
| **Identity Storage** | Mapping strongly-typed IDs as raw `string` or object | Value converter to PostgreSQL native `uuid`: `HasConversion(id => id.Value, v => new TemplateId(v))` |
| **Tenant Scoping** | Manual `where p.ProjectId == id` filter in every query | Ambient multi-tenant global query filter in `AppDbContext`: `ConfigureProjectFilter` |
| **Concurrency Token** | Unprotected mutations susceptible to lost updates | PostgreSQL `xmin` system column mapped as row version token |
| **Relationship Cascade** | Blind `Cascade` deletes on production documents/logs | `DeleteBehavior.Restrict` or `DeleteBehavior.SetNull` |
| **JSON Column Storage** | Storing structured payloads as plain escaped text | Native PostgreSQL `jsonb` column mapping |
| **IQueryable Leakage** | Leaking `IQueryable<T>` outside Infrastructure | Repositories evaluate queries internally; return `Task<IReadOnlyList<T>>` or `Task<T?>` |

---

## 2. Level 2: Core Persistence SSoT ASTs & Integrity Matrix

### Referential Integrity & Cascade Delete Matrix
| Relationship Category | Target Entities | EF Core DeleteBehavior | Foreign Key Invariant |
|---|---|---|---|
| **Master Hierarchy** | `Company` => `Project` => `Template` => `TemplateVersion` | `DeleteBehavior.Cascade` | Child entities have no operational lifecycle outside parent aggregate root |
| **Audit & Legal Records** | `GenerationLog`, `AuditTrail`, `DocumentVersion` | `DeleteBehavior.Restrict` | Prohibit physical deletion of parent if audit records exist; prevents data falsification |
| **Optional References** | `Template.CurrentVersionId`, `User.LastActiveProjectId` | `DeleteBehavior.SetNull` | Nullable FK; decoupling lifecycle avoids circular cascade cascades |

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

### Ambient Multi-Tenant Global Query Filter
>> MANDATORY CANONICAL TEMPLATE [RULE-INFRA-003]:
* [MUST] Apply global query filter in `AppDbContext.OnModelCreating` to all entities implementing `IMustHaveProject`.
* [MUST] Evaluate tenant context dynamically: `_projectContext == null || !_projectContext.IsResolved || e.ProjectId == _projectContext.ProjectId`.
* [MUST NOT] Disable query filter via `.IgnoreQueryFilters()` in standard application routes.
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
        {
            var method = typeof(AppDbContext)
                .GetMethod(nameof(ConfigureProjectFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }
}

private void ConfigureProjectFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveProject
{
    modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
        _projectContext == null || !_projectContext.IsResolved || e.ProjectId == _projectContext.ProjectId);
}
```

---

## 4. Level 4: Anti-Patterns & Micro-Diffs

### [TRAP-INFRA-001]: IQueryable Boundary Leakage
```csharp
// ! BANNED: Leaking IQueryable across layer boundaries triggers uncontrolled execution
public IQueryable<Template> GetTemplates() => dbContext.Templates;

// >> CANONICAL: Fully evaluated, async query execution within repository port
public async Task<IReadOnlyList<Template>> GetTemplatesAsync(ProjectId projectId, CancellationToken ct) =>
    await dbContext.Templates.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);
```

### [TRAP-INFRA-002]: Multi-Tenant Isolation Bypass
```csharp
// ! BANNED: Disabling global query filter without explicit elevated administrative authorization
var templates = await dbContext.Templates.IgnoreQueryFilters().ToListAsync(ct); // FATAL: Data leak!

// >> CANONICAL: Standard ambient query evaluated with automatic tenant filter
var templates = await dbContext.Templates.ToListAsync(ct);
```
