# [PATTERN-001]: 01_domain_layer.md — Domain Layer Design Patterns & SSoT Catalog

<context>
Scope: Domain Layer Patterns, Aggregate Roots, Child Entities, Value Objects, Smart Enums, Guards & ID Catalog
Target: `SmkDoc.Domain/` (`Entities/`, `ValueObjects/`, `Enums/`, `Common/`, `Exceptions/`)
Authority: Absolute SSoT Invariant (Zero infrastructure dependency, 100% pure C# 13 POCOs)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_DOMAIN_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## Architectural Wiring & Data Contracts

| Direction | Upstream Origin | Current Layer Role (Domain Core) | Downstream Destination |
|---|---|---|---|
| **Inbound Creation** | Application (`CreateTemplateCommand`) | Instantiates via SSoT Factory `Template.Create(...)` | Encapsulated Domain Aggregate Root |
| **State Mutation** | Application (`PublishVersionCommand`) | Executes expressive business verb (`template.PublishVersion`) | Mutates internal state & appends `IDomainEvent` |
| **Outbound Persistence**| Domain POCOs (`Template`) | Hydrated/Persisted via `ITemplateRepository` port | Infrastructure (`TemplateConfiguration` -> PostgreSQL) |
| **Outbound Egress** | Domain State | Mapped to immutable Application DTOs | Presentation Layer (`TemplateResultDto`) |
| **Side-Effects** | Aggregate Lifecycle Events | Appends immutable `IDomainEvent` records | `AppDbContext` Outbox / Post-Commit Dispatchers |

### Engineering Rationale & Architectural Decisions
* **Why Sequential UUIDv7:** Chronological ordering eliminates PostgreSQL B-Tree index fragmentation while allowing client/caller ID pre-allocation without database round-trips.
* **Why Sealed Reference Classes for Entities:** Reference identity prevents struct-copy state loss and preserves EF Core ChangeTracker snapshot fidelity.
* **Why Explicit ID Operators:** Prohibits accidental cross-domain ID assignment (e.g. assigning a `ProjectId` to a `TemplateId` slot) at compile-time.
* **Why Injected `DateTimeOffset now`:** Eliminates ambient system clock queries, guaranteeing 100% deterministic unit testing via `FakeTimeProvider`.
* **Why In-Memory Domain Events:** Decouples domain aggregates from messaging infrastructure; side-effects are dispatched strictly post-transaction.

---

## 1. Level 1: Domain Invariants & Encapsulation Rules

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **External Dependencies** | NuGet packages, EF Core, ASP.NET Core, outer layers in csproj | Pure .NET SDK; zero PackageReference and zero ProjectReference in csproj |
| **Persistence Ignorance** | ORM Data Annotations (`[Key]`, `[Table]`, `[Column]`) inside Domain | Pure C# POCOs; EF Core mapping isolated strictly in Infrastructure `IEntityTypeConfiguration<T>` |
| **Always-Valid State** | Public setters, parameterless public constructors, mutable child lists | Encapsulated `private set`, 1 canonical factory `Create()`, `IReadOnlyCollection<T>`, expressive domain verbs |
| **Temporal Determinism** | Direct system clock queries: `DateTime.UtcNow`, `DateTime.Now` | Time injected as explicit parameter: `DateTimeOffset now` or `TimeProvider` |
| **Cross-Aggregate Boundaries** | Cross-aggregate navigation collections (`Project._templates`, `Company._projects`) | Reference other Aggregate Roots strictly by ID (`ProjectId`, `CompanyId`); map Unidirectional FK (`.WithMany()`) |
| **Strongly-Typed Identity & VOs** | Raw `Guid`, primitive string obsession, implicit ID conversion | `readonly record struct TId(Guid Value)`, validated `sealed record class` VOs, `explicit operator` |
| **Side-Effect Decoupling** | Direct I/O, HTTP client calls, messaging bus dispatch inside Domain | Entities append immutable `IDomainEvent` in-memory; dispatched post-commit via UseCases/Outbox |
| **Child Entity Lifecycle** | Creating child entities directly outside aggregate: `var v = TemplateVersion.Draft(...)` | Aggregate root encapsulates child creation & lifecycle: `document.CreateVersion(...)`, `template.CreateDraftVersion(...)` |
| **Tenant Partitioning** | Default optional or unguarded ProjectId: `ProjectId projectId = default` | Mandatory fail-fast assertion: `ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));` |

---

## 2. Level 2: Core Domain SSoT Catalogs & AST Signatures

### Aggregate Root Strongly-Typed IDs
All Aggregate Root strongly-typed IDs are defined as `readonly record struct` under `SmkDoc.Domain.ValueObjects.Ids`:
| ID Struct Name | Underlying Type | Associated Aggregate Root | Bounded Context |
|---|---|---|---|
| `TemplateId` | `Guid` (UUIDv7) | `Template` | Authoring |
| `DocumentId` | `Guid` (UUIDv7) | `Document` | Rendering |
| `DataConnectionId` | `Guid` (UUIDv7) | `DataConnection` | Integration |
| `DatasetId` | `Guid` (UUIDv7) | `Dataset` | Integration |
| `ProjectId` | `Guid` (UUIDv7) | `Project` | IdentityAccess |
| `CompanyId` | `Guid` (UUIDv7) | `Company` | IdentityAccess |
| `UserId` | `Guid` (UUIDv7) | `User` | IdentityAccess |
| `ApiKeyId` | `Guid` (UUIDv7) | `ApiKey` | IdentityAccess |

*Note: Child entities (`TemplateVersion`, `DocumentVersion`, `FieldMapping`, `Font`) and audit entities (`GenerationLog`) utilize standard `Guid` identities scoped strictly under their parent Aggregate Root ID, avoiding artificial abstraction overhead.*

### All 10 Smart Enums
All Smart Enums inherit `Enumeration` under `SmkDoc.Domain.Enums`:
| Smart Enum Class | Values (Id / Name) | Usage Scope |
|---|---|---|
| `ApiKeyScope` | 0: `ReadOnly`, 1: `ReadWrite` | Machine-to-machine security |
| `DatabaseProvider` | 1: `PostgreSQL`, 2: `SqlServer`, 3: `MySQL`, 4: `Oracle` | External database connections |
| `DataSourceType` | 1: `json`, 2: `sql` | Field mapping data source origin |
| `GenerationStatus` | 1: `SUCCESS`, 2: `FAILED`, 3: `PROCESSING`, 4: `TIMEOUT`, 5: `VALIDATION_FAILED` | Audit trail logging |
| `OutputFormat` | 1: `pdf`, 2: `docx`, 3: `xlsx` | Document render output format |
| `RenderEngineType` | 1: `Html`, 2: `Docx`, 3: `Excel` | Low-level rendering engine dispatch |
| `RoleType` | 0: `Viewer`, 1: `Developer`, 2: `Admin` | Project tenant RBAC authorization |
| `SystemRole` | 1: `SuperAdmin`, 2: `Member`, 3: `Viewer` | Global system RBAC authorization |
| `TemplateFormat` | 1: `html`, 2: `docx`, 3: `xlsx`, 4: `pdf` | Template source file format |
| `TemplateVersionStatus`| 0: `Draft`, 1: `Published`, 2: `Archived` | Template version lifecycle |

### Guard Assertions & Validation Contract
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

### Domain Events Catalog
All Domain Events implement `IDomainEvent` (`INotification`) under `SmkDoc.Domain.Events`:
| Domain Event Record | Payload Parameters | Trigger Entity & Business Verb | Bounded Context |
|---|---|---|---|
| `TemplatePublishedDomainEvent` | `TemplateId TemplateId, Guid VersionId, DateTimeOffset PublishedAt` | `Template.PublishVersion(...)` | Authoring |
| `DocumentVersionCreatedDomainEvent` | `DocumentId DocumentId, Guid VersionId, int VersionNumber, DateTimeOffset CreatedAt` | `Document.CreateVersion(...)` | Rendering |
| `ApiKeyRevokedDomainEvent` | `ApiKeyId ApiKeyId, DateTimeOffset RevokedAt` | `ApiKey.Revoke(...)` | IdentityAccess |
| `UserDeactivatedDomainEvent` | `UserId UserId, DateTimeOffset DeactivatedAt` | `User.Deactivate(...)` | IdentityAccess |

---

## 3. Level 3: Canonical Domain Archetypes

### Aggregate Root Archetype (`Template`)
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-001]:
* [MUST] Inherit from `BaseEntity<TId>` where `TId` is a strongly-typed ID value object.
* [MUST] Declare class as `sealed class`. Entities MUST remain reference types (prohibit `struct`/`record struct`).
* [MUST] Encapsulate all property mutations with `private set;` or `init;`. Direct public setters are strictly prohibited.
* [MUST] Provide exactly 1 `private Template()` parameterless constructor for EF Core hydration.
* [MUST] Provide an `internal` constructor for factory/test hydration and exactly 1 `public static TEntity Create(...)` factory method as SSoT.
* [MUST] Require caller to pass explicit `DateTimeOffset now` parameter for time-dependent state mutations (prohibit `DateTime.UtcNow`).
* [MUST] Encapsulate collections as `IReadOnlyCollection<T>` backed by a private `List<T>` field initialized with `[]`.
```csharp
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

public sealed class Template : BaseEntity<TemplateId>, IMustHaveProject
{
    public ProjectId ProjectId { get; private set; }
    public TemplateName Name { get; private set; } = null!;
    public TemplateSlug Slug { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CurrentVersionId { get; private set; }

    private readonly List<FieldMapping> _fieldMappings = [];
    private readonly List<TemplateVersion> _versions = [];

    public IReadOnlyCollection<FieldMapping> FieldMappings => _fieldMappings.AsReadOnly();
    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();
    public TemplateVersion? CurrentVersion { get; private set; }

    private Template() { }

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

    public static Template Create(
        ProjectId projectId,
        TemplateName name,
        TemplateSlug slug,
        string? category,
        DateTimeOffset now)
    {
        return new Template(null, projectId, name, slug, category, now);
    }

    public void Update(TemplateName name, string? category, DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }

    public TemplateVersion PublishVersion(Guid versionId, DateTimeOffset now)
    {
        Guard.NotEmpty(versionId, nameof(versionId));
        if (!IsActive)
            throw new BusinessRuleViolationException("Cannot publish version on an inactive template.", "INACTIVE_TEMPLATE");

        var version = _versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new VersionNotInTemplateException(Id.Value, versionId);

        version.Publish(now);
        CurrentVersionId = versionId;
        SetUpdated(now);
        return version;
    }

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

### Child Entity Archetype (`FieldMapping`)
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-002]:
* [MUST] Inherit from `BaseEntity` (without generic ID) or `BaseEntity<TId>`.
* [MUST] Declare class as `sealed class` with parameterless `private` constructor for EF Core.
* [MUST] Provide `internal static TChild Create(...)` factory accessible only to aggregate root.
* [MUST] Enforce all child lifecycle mutations and invariants strictly through the parent Aggregate Root.
```csharp
namespace SmkDoc.Domain.Entities;

public sealed class FieldMapping : BaseEntity
{
    public TemplateId TemplateId { get; private set; }
    public string SourceField { get; private set; } = null!;
    public string TargetField { get; private set; } = null!;
    public bool IsRequired { get; private set; }

    private FieldMapping() { }

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

### Value Object Archetypes
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-003]:
* [MUST] Model validated domain concepts (names, slugs, emails) as `sealed record class` with a `private` constructor and canonical `public static T Create(...)` factory.
* [MUST] Model small, immutable geometric or numeric measurements (<= 16 bytes, e.g. `PageDimension`, `Margins`) as `readonly record struct`.
* [MUST] Execute validation guards immediately inside `Create(...)` throwing strongly-typed `DomainValidationException`.
```csharp
namespace SmkDoc.Domain.ValueObjects;

// Reference Type Validated VO
public sealed record class TemplateName
{
    public string Value { get; }
    private TemplateName(string value) => Value = value;
    public static TemplateName Create(string? value) => new(Guard.NotBlank(value, "Template name", 100));
    public static implicit operator string(TemplateName name) => name.Value;
    public override string ToString() => Value;
}

// Sensitive Credential Validated VO
public sealed record class EncryptedConnectionString
{
    public string Value { get; }
    private EncryptedConnectionString(string value) => Value = value;
    public static EncryptedConnectionString Create(string? value) => new(Guard.NotBlank(value, "Encrypted connection string"));
    public static implicit operator string(EncryptedConnectionString connStr) => connStr.Value;
    public override string ToString() => "[REDACTED]";
}

// Struct Primitive VO
public readonly record struct PageDimension(float WidthMm, float HeightMm);

// Normalized URL-Friendly Tenant Slug VO
public sealed partial record ProjectSlug
{
    public string Value { get; }
    public ProjectSlug(string value) => Value = Guard.NotBlank(value, "Project slug");
    public static ProjectSlug Create(string value) => new(value);
    public static implicit operator string(ProjectSlug slug) => slug.Value;
    public override string ToString() => Value;
}

// Cryptographic Integrity Hash VO
public sealed record class Sha256Hash
{
    public const int HashLength = 64;
    public string Value { get; }
    public Sha256Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != HashLength)
            throw new DomainValidationException($"Hash must be exactly {HashLength} hexadecimal characters.");
        Value = value.ToLowerInvariant();
    }
    public static Sha256Hash Create(string value) => new(value);
    public static implicit operator string(Sha256Hash hash) => hash.Value;
    public override string ToString() => Value;
}
```

### Smart Enum Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-004]:
* [MUST] Inherit from `SmkDoc.Domain.Common.Enumeration`.
* [MUST] Declare class as `sealed class` with `private` constructor taking `(int id, string name)`.
* [MUST] Expose instances as `public static readonly TEnum Name = new(id, nameof(Name));`.
* [MUST NOT] Use primitive C# `enum` for engine types, document formats, or template statuses requiring behavior or DB mapping.
```csharp
using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public sealed class RenderEngineType : Enumeration
{
    public static readonly RenderEngineType Html = new(1, "Html");
    public static readonly RenderEngineType Docx = new(2, "Docx");
    public static readonly RenderEngineType Excel = new(3, "Excel");

    private RenderEngineType(int id, string name) : base(id, name) { }
}
```

---
## 4. Level 4: High-Salience Fatal Traps & Interceptors

### [TRAP-DOM-001]: Entity Struct Identity Collapse
```csharp
// ! BANNED: Declaring entity as struct collapses EF Core change tracking and causes stack copy bloat
public struct Template : BaseEntity { ... }

// >> CANONICAL: Sealed class reference type preserving identity and EF Core ChangeTracker snapshots
public sealed class Template : BaseEntity<TemplateId> { ... }
```

### [TRAP-DOM-002]: Struct Value Object Default Invariant Breach
```csharp
// ! BANNED: Struct value object allows default(T) bypass of Guard validation
public readonly record struct EmailAddress(string Value); // default(EmailAddress).Value is null!

// >> CANONICAL: Sealed record class with private constructor enforcing factory execution
public sealed record class EmailAddress
{
    public string Value { get; }
    private EmailAddress(string value) => Value = value;
    public static EmailAddress Create(string? value) => new(Guard.ValidEmail(value, "Email"));
}
```

### [TRAP-DOM-003]: Implicit ID Conversion Cross-Domain Pollution
```csharp
// ! BANNED: Implicit operator allows accidental assignment between different ID types or primitive Guids
public static implicit operator Guid(TemplateId id) => id.Value;
public static implicit operator TemplateId(Guid value) => new(value);
// Inadvertent cross-domain pollution: TemplateId assigned to ProjectId or DocumentId slot without compiler error

// >> CANONICAL: Explicit operators prevent silent cross-domain ID pollution and force deliberate casting
public static explicit operator Guid(TemplateId id) => id.Value;
public static explicit operator TemplateId(Guid value) => new(value);
```
