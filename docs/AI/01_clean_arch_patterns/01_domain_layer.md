# [PATTERN-001]: 01_domain_layer.md — Domain Layer Design Patterns & SSoT Catalog

<context>
Scope: Domain Layer Patterns, Aggregate Roots, Child Entities, Value Objects, Smart Enums, Guards & ID Catalog
Target: `SmkDoc.Domain/` (`Entities/`, `ValueObjects/`, `Enums/`, `Common/`, `Exceptions/`)
Authority: Absolute SSoT Invariant (Zero infrastructure dependency, 100% pure C# 13 POCOs)
</context>

---

## 1. Domain Layer Invariants & Encapsulation Rules

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Property Mutability** | Public setters: `public string Name { get; set; }` | Encapsulated state: `public TemplateName Name { get; private set; } = null!;` |
| **Instantiation** | Calling `new Aggregate()` directly from outside | Exactly 1 canonical factory method: `public static Aggregate Create(...)` |
| **Business Logic** | Anemic domain models manipulated via external services | Expressive business verbs on entity: `public void Update(...)`, `public void Deactivate(...)` |
| **Time Queries** | Direct `DateTime.UtcNow` calls inside domain entities | Time injected as explicit parameter: `DateTimeOffset now` |
| **Child Collections** | Exposing `List<T>`: `public List<Item> Items { get; }` | `private readonly List<T> _items; public IReadOnlyCollection<T> Items => _items.AsReadOnly();` |
| **Primitives vs VOs** | Raw `Guid` or `string` for identity and domain concepts | Strongly-typed IDs (`TemplateId`) and validated Value Objects (`TemplateName`) |

---

## 2. Aggregate Root Pattern Archetype

```csharp
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain aggregate root representing a document template.
/// Owns TemplateVersion and FieldMapping child entities.
/// </summary>
public sealed class Template : BaseEntity<TemplateId>, IMustHaveProject
{
    public ProjectId ProjectId { get; private set; }
    public TemplateName Name { get; private set; } = null!;
    public TemplateSlug Slug { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CurrentVersionId { get; private set; }

    // Encapsulated child collections (Strictly immutable to external callers)
    private readonly List<FieldMapping> _fieldMappings = new();
    private readonly List<TemplateVersion> _versions = new();

    public IReadOnlyCollection<FieldMapping> FieldMappings => _fieldMappings.AsReadOnly();
    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();

    // Navigation property for EF Core materialization
    public TemplateVersion? CurrentVersion { get; private set; }

    // Private constructor for EF Core reflection materialization only
    private Template() { }

    // Internal constructor invoked exclusively via canonical factory method
    internal Template(
        TemplateId? id,
        ProjectId projectId,
        TemplateName name,
        TemplateSlug slug,
        string? category,
        DateTimeOffset now)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : TemplateId.New(), createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        IsActive = true;
    }

    /// <summary>Canonical factory method — SSoT for Aggregate creation.</summary>
    public static Template Create(
        ProjectId projectId,
        TemplateName name,
        TemplateSlug slug,
        string? category,
        DateTimeOffset now)
    {
        return new Template(null, projectId, name, slug, category, now);
    }

    /// <summary>Business verb: Mutate template metadata with deterministic timestamp.</summary>
    public void Update(TemplateName name, string? category, DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        SetUpdated(now);
    }

    /// <summary>Business verb: Deactivate template and record timestamp.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }

    /// <summary>Business verb: Manage child entity lifecycle within aggregate boundary.</summary>
    public void AddFieldMapping(string sourceField, string targetField, bool isRequired)
    {
        if (_fieldMappings.Any(m => m.SourceField.Equals(sourceField, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainValidationException($"Field mapping for '{sourceField}' already exists.");
        }

        _fieldMappings.Add(FieldMapping.Create(Id, sourceField, targetField, isRequired));
    }
}
```

---

## 3. Child Entity Pattern Archetype

Child entities inherit `BaseEntity` (Guid UUIDv7 identity) or `BaseEntity<TId>`. Their lifecycle is strictly managed by their Aggregate Root:

```csharp
namespace SmkDoc.Domain.Entities;

public sealed class FieldMapping : BaseEntity
{
    public TemplateId TemplateId { get; private set; }
    public string SourceField { get; private set; } = null!;
    public string TargetField { get; private set; } = null!;
    public bool IsRequired { get; private set; }

    private FieldMapping() { } // EF Core only

    internal static FieldMapping Create(TemplateId templateId, string sourceField, string targetField, bool isRequired) =>
        new()
        {
            TemplateId = Guard.NotEmpty(templateId, nameof(TemplateId)),
            SourceField = Guard.NotBlank(sourceField, nameof(SourceField), 100),
            TargetField = Guard.NotBlank(targetField, nameof(TargetField), 100),
            IsRequired = isRequired
        };
}
```

---

## 4. Value Object Pattern Archetypes

### Validated String Value Object (`sealed record class`)
Use `sealed record class` for reference types to avoid default uninitialized null struct traps:
```csharp
namespace SmkDoc.Domain.ValueObjects;

public sealed record class TemplateName
{
    public string Value { get; }
    private TemplateName(string value) => Value = value;

    public static TemplateName Create(string? value) =>
        new(Guard.NotBlank(value, "Template name", 100));

    public static implicit operator string(TemplateName name) => name.Value;
    public override string ToString() => Value;
}
```

### Stack-Allocated Struct Value Object (`readonly record struct`)
Use `readonly record struct` for small layout primitives ($\le 16$ bytes):
```csharp
public readonly record struct PageDimension(float WidthMm, float HeightMm);
```

---

## 5. Smart Enum Pattern Archetype

Inherit from `Enumeration(int id, string name)`. Enforces type-safety and provides static instances:

```csharp
using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public sealed class EngineType : Enumeration
{
    public static readonly EngineType Gotenberg = new(1, nameof(Gotenberg));
    public static readonly EngineType OpenXml = new(2, nameof(OpenXml));
    public static readonly EngineType ClosedXml = new(3, nameof(ClosedXml));

    private EngineType(int id, string name) : base(id, name) { }
}
```

---

## 6. Guard Validation & Domain Exception

Fail-fast assertions throwing strongly-typed `DomainValidationException`:
```csharp
namespace SmkDoc.Domain.Common;

internal static class Guard
{
    public static T NotNull<T>(T? value, string name) where T : class =>
        value ?? throw new DomainValidationException($"{name} cannot be null.");

    public static string NotBlank(string? value, string name, int maxLength = 255)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException($"{name} cannot be empty.");
        if (value.Length > maxLength)
            throw new DomainValidationException($"{name} cannot exceed {maxLength} characters.");
        return value.Trim();
    }

    public static TId NotEmpty<TId>(TId value, string name) where TId : struct, IEquatable<TId> =>
        value.Equals(default) ? throw new DomainValidationException($"{name} cannot be empty.") : value;
}
```

---

## 7. Master System SSoT Catalog

### All 12 Strongly-Typed IDs
All strongly-typed IDs are defined under `SmkDoc.Domain.ValueObjects.Ids`:
| ID Struct Name | Underlying Type | Associated Entity | Bounded Context |
|---|---|---|---|
| `TemplateId` | `Guid` (UUIDv7) | `Template` | Authoring |
| `TemplateVersionId` | `Guid` (UUIDv7) | `TemplateVersion` | Authoring |
| `FieldMappingId` | `Guid` (UUIDv7) | `FieldMapping` | Authoring |
| `FontId` | `Guid` (UUIDv7) | `Font` | Authoring |
| `DocumentId` | `Guid` (UUIDv7) | `Document` | Rendering |
| `DocumentVersionId` | `Guid` (UUIDv7) | `DocumentVersion` | Rendering |
| `GenerationLogId` | `Guid` (UUIDv7) | `GenerationLog` | Rendering |
| `DataConnectionId` | `Guid` (UUIDv7) | `DataConnection` | Integration |
| `DatasetId` | `Guid` (UUIDv7) | `Dataset` | Integration |
| `ProjectId` | `Guid` (UUIDv7) | `Project` | IdentityAccess |
| `CompanyId` | `Guid` (UUIDv7) | `Company` | IdentityAccess |
| `UserId` | `Guid` (UUIDv7) | `User` | IdentityAccess |
| `ApiKeyId` | `Guid` (UUIDv7) | `ApiKey` | IdentityAccess |

### All 9 Smart Enums
All Smart Enums inherit `Enumeration` under `SmkDoc.Domain.Enums`:
| Smart Enum Class | Values (Id / Name) | Usage Scope |
|---|---|---|
| `EngineType` | 1: `Gotenberg`, 2: `OpenXml`, 3: `ClosedXml` | Rendering Engine selection |
| `TemplateType` | 1: `Html`, 2: `Docx`, 3: `Xlsx` | Document Template format |
| `TemplateStatus` | 1: `Draft`, 2: `Published`, 3: `Archived` | Authoring lifecycle |
| `DocumentStatus` | 1: `Pending`, 2: `Processing`, 3: `Completed`, 4: `Failed` | Production render state |
| `GenerationStatus`| 1: `Queued`, 2: `Rendering`, 3: `Success`, 4: `Error` | Audit trail logging |
| `ProjectRole` | 1: `Owner`, 2: `Admin`, 3: `Editor`, 4: `Viewer` | Tenant RBAC authorization |
| `ApiKeyScope` | 1: `Read`, 2: `Write`, 3: `Admin`, 4: `RenderOnly` | Machine-to-machine security |
| `DataConnectionType`| 1: `PostgreSql`, 2: `MySql`, 3: `SqlServer`, 4: `RestApi`| Data source adapters |
| `ExtractionStrategy`| 1: `DirectQuery`, 2: `Parameterized`, 3: `Batch` | Dataset extraction mode |
