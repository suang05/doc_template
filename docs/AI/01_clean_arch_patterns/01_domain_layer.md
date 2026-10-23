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

## 1. Level 1: Domain Invariants & Encapsulation Rules

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Property Mutability** | Public setters: `public string Name { get; set; }` | Encapsulated state: `public TemplateName Name { get; private set; } = null!;` |
| **Instantiation** | Calling `new Aggregate()` directly from outside | Exactly 1 canonical factory method: `public static Aggregate Create(...)` |
| **Business Logic** | Anemic domain models manipulated via external services | Expressive business verbs on entity: `public void Update(...)`, `public void Deactivate(...)` |
| **Time Queries** | Direct `DateTime.UtcNow` calls inside domain entities | Time injected as explicit parameter: `DateTimeOffset now` |
| **Child Collections** | Exposing `List<T>`: `public List<Item> Items { get; }` | `private readonly List<T> _items = []; public IReadOnlyCollection<T> Items => _items.AsReadOnly();` |
| **Primitives vs VOs** | Raw `Guid` or `string` for identity and domain concepts | Strongly-typed IDs (`TemplateId`) and validated Value Objects (`TemplateName`) |
| **Identity Conversion** | Implicit Guid operators allowing silent ID type cross-contamination | Explicit operators (`explicit operator Guid`, `explicit operator TId`) for strict compile-time safety |
| **Tenant Partitioning** | Default optional or unguarded ProjectId: `ProjectId projectId = default` | Mandatory fail-fast assertion: `ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));` |
| **Child Entity Lifecycle** | Creating child entities directly outside aggregate: `var version = DocumentVersion.Create(...)` | Aggregate root encapsulates child creation & lifecycle: `document.CreateVersion(...)`, `template.PublishVersion(...)` |
| **Domain Events Integration** | Direct MediatR/Event dispatch in Domain entity or un-encapsulated event list | Entities append events via `AddDomainEvent()`. Zero direct I/O inside domain. |

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

public sealed class EngineType : Enumeration
{
    public static readonly EngineType Gotenberg = new(1, nameof(Gotenberg));
    public static readonly EngineType OpenXml = new(2, nameof(OpenXml));
    public static readonly EngineType ClosedXml = new(3, nameof(ClosedXml));

    private EngineType(int id, string name) : base(id, name) { }
}
```

---

## 4. Level 4: Anti-Patterns & Micro-Diffs

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

### [TRAP-DOM-003]: Encapsulation: Public Setters vs Private
```csharp
// ! BANNED: Public setters permit external invariant bypass
public string Name { get; set; } = null!;

// >> CANONICAL: Encapsulated property mutated via business verb
public TemplateName Name { get; private set; } = null!;
public void Update(TemplateName name, DateTimeOffset now) { Name = Guard.NotNull(name, nameof(Name)); SetUpdated(now); }
```

### [TRAP-DOM-004]: Time Queries: Non-Deterministic Clock vs Injected Timestamp
```csharp
// ! BANNED: Invariant state depends on ambient system clock
public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }

// >> CANONICAL: Deterministic timestamp injected by caller
public void Deactivate(DateTimeOffset now) { IsActive = false; SetUpdated(now); }
```

### [TRAP-DOM-005]: Instantiation: Bypassing SSoT Factory
```csharp
// ! BANNED: Instantiating aggregate via public constructor
var template = new Template();

// >> CANONICAL: Canonical factory method enforcing initial invariants
var template = Template.Create(projectId, name, slug, category, now);
```

### [TRAP-DOM-006]: Implicit ID Conversion Leak
```csharp
// ! BANNED: Implicit operator allows accidental assignment between different ID types or primitive Guids
public static implicit operator Guid(TemplateId id) => id.Value;
public static implicit operator TemplateId(Guid value) => new(value);
// Inadvertent cross-domain pollution: TemplateId assigned to ProjectId or DocumentId without compiler error

// >> CANONICAL: Explicit operators prevent silent cross-domain ID pollution and force deliberate casting
public static explicit operator Guid(TemplateId id) => id.Value;
public static explicit operator TemplateId(Guid value) => new(value);
```

### [TRAP-DOM-007]: Child Entity Lifecycle Bypass & Split-Brain Mutation
```csharp
// ! BANNED: External direct instantiation or disconnected lifecycle mutation
var docVersion = DocumentVersion.Create(doc.Id, max + 1, ...);
version.Publish(now);
template.SetCurrentVersion(version.Id, now);

// >> CANONICAL: Aggregate root encapsulates child lifecycle atomically
var docVersion = doc.CreateVersion(max + 1, ...);
template.PublishVersion(version, now);
```

### [TRAP-DOM-008]: Sensitive Credential Leak via Primitive String
```csharp
// ! BANNED: Raw connection string exposes secrets via ToString() and structured logging
public string EncryptedConnectionString { get; private set; }

// >> CANONICAL: Encapsulated VO redacting sensitive payload in ToString()
public EncryptedConnectionString EncryptedConnectionString { get; private set; } = null!;
```

### [TRAP-DOM-009]: Side-Effect Coupling vs In-Memory Domain Events
```csharp
// ! BANNED: Direct service injection, async dispatch, or I/O inside domain entity
public async Task PublishVersion(IMediator mediator, Guid versionId) {
    ...
    await mediator.Publish(new VersionPublishedEvent(...)); // Violates domain purity & transaction boundary
}

// >> CANONICAL: Aggregate appends immutable event record; DbContext dispatches post-persistence
public TemplateVersion PublishVersion(Guid versionId, DateTimeOffset now) {
    ...
    AddDomainEvent(new TemplatePublishedDomainEvent(Id, versionId, now));
    return version;
}
```

