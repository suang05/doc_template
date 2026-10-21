# [PATTERN-003]: 03_infrastructure_layer.md — Infrastructure Layer Design Patterns & EF Core SSoT

<context>
Scope: Infrastructure Layer Patterns, EF Core Configurations, Strongly-Typed ID Converters, JSONB Mapping, Repositories & Tenancy Filter
Target: `SmkDoc.Infrastructure/` (`Persistence/Configurations/`, `Repositories/`, `Storage/`, `Gotenberg/`)
Authority: Absolute SSoT Invariant (Hides all network and persistence I/O behind Application ports)
</context>

---

## 1. Infrastructure Layer Invariants & Persistence Rules

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

## 2. EF Core Entity Configuration Archetype (`IEntityTypeConfiguration<T>`)

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
        // 1. Table & Primary Key
        builder.ToTable("templates");
        builder.HasKey(e => e.Id);

        // 2. Strongly-Typed ID Value Converters
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new TemplateId(v))
            .HasColumnName("id");

        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, v => new ProjectId(v))
            .HasColumnName("project_id")
            .IsRequired();

        // 3. Validated Value Object Converters
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

        // 4. Scalar Properties & Audit Columns
        builder.Property(e => e.Category)
            .HasColumnName("category")
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // 5. Optimistic Concurrency Token via PostgreSQL xmin
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // 6. Indexes
        builder.HasIndex(e => new { e.ProjectId, e.Slug }).IsUnique();

        // 7. Relationships & Delete Behaviors
        builder.HasOne(e => e.CurrentVersion)
            .WithMany()
            .HasForeignKey(e => e.CurrentVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.FieldMappings)
            .WithOne()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 3. PostgreSQL Native JSONB Column Mapping

Store dynamic extraction schemas, data snapshots, or template metadata in native PostgreSQL `jsonb`:

```csharp
// In Entity Configuration:
builder.Property(e => e.ExtractionSchema)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new());
```

---

## 4. Repository Implementation Archetype & IQueryable Boundary

Repositories hide EF Core `DbSet<T>` queries behind domain interfaces, executing strictly asynchronous operations.

[RULE-INFRA-001]: Repositories MUST NEVER return `IQueryable<T>` outside `SmkDoc.Infrastructure`. Returning `IQueryable<T>` across boundaries leaks persistence concerns, triggers uncontrolled deferred execution outside database transactions, and induces N+1 query traps. All queries must be executed within the repository (`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`) returning evaluated collections (`IReadOnlyList<T>`).

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
        return await dbContext.Templates
            .AnyAsync(t => t.Slug == slug, ct);
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

---

## 5. Multi-Tenant Global Query Filter Archetype

In `AppDbContext.cs`, configure global query filters to enforce ambient tenant isolation:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // Ambient Multi-Tenant Filter SSoT
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
    // Evaluates projectContext at query execution time
    modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
        _projectContext == null || !_projectContext.IsResolved || e.ProjectId == _projectContext.ProjectId);
}
```

---

## 6. Unit of Work Archetype

Transactions spanning multiple repository mutations are committed atomically:

```csharp
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        return await dbContext.SaveChangesAsync(ct);
    }
}
```

---

## 7. Audit Log Referencing & Cascade Delete Invariant

[RULE-INFRA-002]: Referential Integrity Matrix:

| Relationship Category | Target Entities | EF Core DeleteBehavior | Foreign Key Invariant |
|---|---|---|---|
| **Master Hierarchy** | `Company` => `Project` => `Template` => `TemplateVersion` | `DeleteBehavior.Cascade` | Child entities have no operational lifecycle outside parent aggregate root |
| **Audit & Legal Records** | `GenerationLog`, `AuditTrail`, `DocumentVersion` | `DeleteBehavior.Restrict` | Prohibit physical deletion of parent if audit records exist; prevents data falsification |
| **Optional References** | `Template.CurrentVersionId`, `User.LastActiveProjectId` | `DeleteBehavior.SetNull` | Nullable FK; decoupling lifecycle avoids circular cascade cascades |

```csharp
// Example: Audit Log Configuration with DeleteBehavior.Restrict
public sealed class GenerationLogConfiguration : IEntityTypeConfiguration<GenerationLog>
{
    public void Configure(EntityTypeBuilder<GenerationLog> builder)
    {
        builder.ToTable("generation_logs");
        builder.HasKey(e => e.Id);

        // Retain generation history even if template or project is archived/deleted
        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```
