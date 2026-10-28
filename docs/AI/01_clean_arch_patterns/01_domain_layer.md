# [PATTERN-001]: 01_domain_layer.md — Domain Layer Design Patterns & SSoT Catalog

<context>
Scope: Domain Layer Patterns, Aggregate Roots, Child Entities, Value Objects, Smart Enums, Guards, Domain Events & BaseEntity Contract
Target: `SmkDoc.Domain/` (`Entities/`, `ValueObjects/`, `Enums/`, `Common/`, `Exceptions/`, `Events/`, `Interfaces/`)
Authority: Absolute SSoT Invariant (Zero infrastructure dependency, 100% pure C# 13 POCOs)
Invariants: Encapsulated State, Single Factory Create(), UUIDv7 Identifiers, Parameterized TimeProvider
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_DOMAIN_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Domain Invariants & Encapsulation Rules

### Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Domain Core) | Downstream Destination |
|---|---|---|---|
| **Inbound Creation** | Application (`CreateTemplateCommand`) | Instantiates via SSoT Factory `Template.Create(...)` | Encapsulated Domain Aggregate Root |
| **State Mutation** | Application (`PublishVersionCommand`) | Executes expressive business verb (`template.PublishVersion`) | Mutates internal state & appends `IDomainEvent` |
| **Outbound Persistence**| Domain POCOs (`Template`) | Hydrated/Persisted via `ITemplateRepository` port | Infrastructure (`TemplateConfiguration` => PostgreSQL) |
| **Outbound Egress** | Domain State | Mapped to immutable Application DTOs | Presentation Layer (`TemplateResultDto`) |
| **Side-Effects** | Aggregate Lifecycle Events | Appends immutable `IDomainEvent` records | `AppDbContext` Outbox / Post-Commit Dispatchers |

### Domain Isolation Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **External Dependencies** | NuGet packages, EF Core, ASP.NET Core, outer layers in csproj | Pure .NET SDK; zero PackageReference and zero ProjectReference in csproj |
| **Persistence Ignorance** | ORM Data Annotations (`[Key]`, `[Table]`, `[Column]`) inside Domain | Pure C# POCOs; EF Core mapping isolated strictly in Infrastructure `IEntityTypeConfiguration<T>` |
| **Always-Valid State** | Public setters, parameterless public constructors, mutable child lists | Encapsulated `private set`, 1 canonical factory `Create()`, `IReadOnlyCollection<T>`, expressive verbs |
| **Temporal Determinism** | Direct system clock queries: `DateTime.UtcNow`, `DateTime.Now` | Time injected as explicit parameter: `DateTimeOffset now` or `TimeProvider` |
| **Cross-Aggregate Boundaries**| Cross-aggregate navigation collections (`Project._templates`, `Company._projects`)| Reference other Aggregate Roots strictly by ID (`ProjectId`, `CompanyId`); unidirectional FK |
| **Strongly-Typed Identity** | Raw `Guid`, primitive string obsession, implicit ID conversion | `readonly record struct TId(Guid Value)`, validated `sealed record class` VOs, `explicit operator` |
| **Side-Effect Decoupling** | Direct I/O, HTTP client calls, messaging bus dispatch inside Domain | Entities append immutable `IDomainEvent` in-memory; dispatched post-commit via UseCases |
| **Child Entity Lifecycle** | Creating child entities directly outside aggregate: `var v = TemplateVersion.Draft(...)` | Aggregate root encapsulates child creation & lifecycle: `template.CreateDraftVersion(...)` |
| **Tenant Partitioning** | Default optional or unguarded ProjectId: `ProjectId projectId = default` | Mandatory fail-fast assertion: `ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));` |

---

## 2. Level 2: Core Domain SSoT Catalogs & Cognitive Domain Engine

### Engineering Rationale & Architectural Decisions
* **Why Sequential UUIDv7:** Chronological ordering eliminates PostgreSQL B-Tree index fragmentation while allowing client/caller ID pre-allocation without database round-trips.
* **Why Sealed Reference Classes for Entities:** Reference identity prevents struct-copy state loss and preserves EF Core ChangeTracker snapshot fidelity.
* **Why Explicit ID Operators:** Prohibits accidental cross-domain ID assignment (e.g. assigning a `ProjectId` to a `TemplateId` slot) at compile-time.
* **Why Injected `DateTimeOffset now`:** Eliminates ambient system clock queries, guaranteeing 100% deterministic unit testing via `FakeTimeProvider`.
* **Why In-Memory Domain Events:** Decouples domain aggregates from messaging infrastructure; side-effects are dispatched strictly post-transaction.
* **Why Child Entities Use Standard `Guid`:** Child entities (`TemplateVersion`, `FieldMapping`, `TemplateDataset`) are strictly scoped within their parent aggregate root; creating strongly-typed ID structs for every child entity causes abstraction bloat without safety benefits.

### Cognitive Domain Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Domain Invariant Preservation & Pure Entity State Encapsulation
  TradeOffPriority: DomainPurity > StrictEncapsulation > TemporalDeterminism > RawBoilerplateReduction

ProactiveDomainGuards:
  - Scope: SmkDoc.Domain.Entities
    Trigger: Detection of public setter (`set;`) or public parameterless constructor
    CanonicalAction: Enforce private set and declare single canonical static Create() factory >> Ref: [RULE-DOM-001]

  - Scope: SmkDoc.Domain
    Trigger: Reference to DateTime.UtcNow, DateTime.Now, or ambient clock
    CanonicalAction: Parameterize method signature with explicit DateTimeOffset now

  - Scope: SmkDoc.Domain.ValueObjects.Ids
    Trigger: Declaration of implicit cast operator to/from Guid
    CanonicalAction: Enforce explicit cast operator to prevent cross-domain ID pollution >> Ref: [TRAP-DOM-003]

  - Scope: SmkDoc.Domain.Dependencies
    Trigger: Addition of Microsoft.EntityFrameworkCore or external packages in csproj
    CanonicalAction: Reject immediately; Domain must compile with pure .NET SDK only

CoreExtensionSlots:
  AggregateRootSlot: BaseEntity<TId> (Encapsulated identity, versioning, and domain event hooks)
  ChildEntitySlot: BaseEntity (Parent-scoped entity lifecycle)
  ValueObjectSlot: ValueObject / Record Class (Immutable self-validating business attributes)
  DomainEventSlot: IDomainEvent (Immutable event notification record)
  SmartEnumSlot: Enumeration (Type-safe behavior-driven static enum instances)
```

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

### Child & Association Entities Catalog
Child entities utilize standard `Guid` identities scoped strictly under their parent Aggregate Root ID, avoiding artificial abstraction overhead:
| Child / Association Entity | Parent Aggregate Root | Identity Type | Lifecycle Controller |
|---|---|---|---|
| `TemplateVersion` | `Template` | `Guid` | `Template.CreateDraftVersion(...)`, `Template.PublishVersion(...)` |
| `FieldMapping` | `Template` | `Guid` | `Template.AddFieldMapping(...)` |
| `TemplateDataset` | `Template` | `Guid` | `Template.AddDataset(...)` |
| `DocumentVersion` | `Document` | `Guid` | `Document.CreateVersion(...)` |
| `RefreshToken` | `User` | `Guid` | `User.AddRefreshToken(...)`, `User.RevokeRefreshToken(...)` |
| `UserProjectRole` | `User` / `Project` | `Guid` | `User.AssignRole(...)` |
| `GenerationLog` | Audit Aggregate | `Guid` | Standalone audit trail capturing render attempts |

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

### Comprehensive Value Objects Catalog
All Value Objects encapsulate validation rules and enforce immutability:
| Value Object Class | Category | Type Structure | Invariant / Validation Rule |
|---|---|---|---|
| `TemplateName` | Identity/Descriptor | `sealed record class` | Not blank, <= 100 chars |
| `TemplateSlug` | URL/Key | `sealed record class` | Lowercase alphanumeric kebab: `^[a-z0-9-]+$` |
| `ProjectName` | Identity/Descriptor | `sealed record class` | Not blank, <= 100 chars |
| `ProjectSlug` | URL/Key | `sealed record class` | Lowercase alphanumeric kebab: `^[a-z0-9-]+$` |
| `CompanyName` | Identity/Descriptor | `sealed record class` | Not blank, <= 150 chars |
| `ConnectionName` | Identity/Descriptor | `sealed record class` | Not blank, <= 100 chars |
| `DatasetName` | Identity/Descriptor | `sealed record class` | Not blank, <= 100 chars |
| `DatasetAlias` | Variable Identifier | `sealed record class` | Valid identifier format: `^[a-zA-Z_][a-zA-Z0-9_]*$` |
| `ApiKeyName` | Security Credential | `sealed record class` | Not blank, <= 100 chars |
| `EmailAddress` | Communication | `sealed record class` | Strict email regex validation |
| `EncryptedConnectionString` | Security Secret | `sealed record class` | Obfuscates output: `ToString() => "[REDACTED]"` |
| `Sha256Hash` | Cryptographic | `sealed record class` | Exactly 64 hexadecimal characters |
| `SqlQuery` | Integration Payload | `sealed record class` | Not empty, sanitized query string |
| `PageDimension` | Layout / Geometry | `readonly record struct` | WidthMm > 0, HeightMm > 0 |
| `PageMargin` | Layout / Geometry | `readonly record struct` | TopMm, BottomMm, LeftMm, RightMm >= 0 |
| `DateRange` | Temporal Range | `readonly record struct` | StartDate <= EndDate |
| `ExpirationPolicy` | Lifecycle Policy | `sealed record class` | Expiration date must be future |
| `DocumentReference` | Reference Token | `sealed record class` | Not blank, structured reference format |

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

    public static string ValidEmail(string? value, string name)
    {
        var email = NotBlank(value, name, 255);
        if (!email.Contains('@') || !email.Contains('.'))
            throw new DomainValidationException($"{name} has an invalid email format.");
        return email.ToLowerInvariant();
    }
}
```

### Domain Events Catalog
All Domain Events implement `IDomainEvent` under `SmkDoc.Domain.Events`:
| Domain Event Record | Payload Parameters | Trigger Entity & Business Verb | Bounded Context |
|---|---|---|---|
| `TemplatePublishedDomainEvent` | `TemplateId TemplateId, Guid VersionId, DateTimeOffset PublishedAt` | `Template.PublishVersion(...)` | Authoring |
| `DocumentVersionCreatedDomainEvent` | `DocumentId DocumentId, Guid VersionId, int VersionNumber, DateTimeOffset CreatedAt` | `Document.CreateVersion(...)` | Rendering |
| `ApiKeyRevokedDomainEvent` | `ApiKeyId ApiKeyId, DateTimeOffset RevokedAt` | `ApiKey.Revoke(...)` | IdentityAccess |
| `UserDeactivatedDomainEvent` | `UserId UserId, DateTimeOffset DeactivatedAt` | `User.Deactivate(...)` | IdentityAccess |

---

## 3. Level 3: Canonical Domain Archetypes

### Base Entity Contracts (`BaseEntity<TId>` & `BaseEntity`)
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-000]:
* [MUST] Provide `BaseEntity<TId>` for Aggregate Roots with strongly-typed identity and domain event harvesting.
* [MUST] Provide `BaseEntity` for child entities utilizing standard `Guid` identities.
* [MUST] Encapsulate `_domainEvents` collection with `IReadOnlyCollection<IDomainEvent>` and `AddDomainEvent()` / `ClearDomainEvents()`.
```csharp
using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Common;

public abstract class BaseEntity<TId> : IHaveDomainEvents where TId : struct, IEquatable<TId>
{
    public TId Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected BaseEntity() { }

    protected BaseEntity(TId id, DateTimeOffset createdAt)
    {
        Id = Guard.NotEmpty(id, nameof(Id));
        CreatedAt = Guard.NotDefault(createdAt, nameof(CreatedAt));
    }

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(Guard.NotNull(domainEvent, nameof(domainEvent)));
    public void ClearDomainEvents() => _domainEvents.Clear();
    protected void SetUpdated(DateTimeOffset now) => UpdatedAt = Guard.NotDefault(now, nameof(now));
}

public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid? id, DateTimeOffset createdAt)
    {
        Id = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.CreateVersion7();
        CreatedAt = Guard.NotDefault(createdAt, nameof(CreatedAt));
    }

    protected void SetUpdated(DateTimeOffset now) => UpdatedAt = Guard.NotDefault(now, nameof(now));
}
```

### Aggregate Root Archetype (`Template`)
>> MANDATORY CANONICAL TEMPLATE [RULE-DOM-001]:
* [MUST] Inherit from `BaseEntity<TId>` where `TId` is a strongly-typed ID value object.
* [MUST] Declare class as `sealed class`. Entities MUST remain reference types (prohibit `struct`/`record struct`).
* [MUST] Encapsulate all property mutations with `private set;`. Direct public setters are strictly prohibited.
* [MUST] Provide exactly 1 `private Template()` parameterless constructor for EF Core hydration.
* [MUST] Provide an `internal` constructor for factory/test hydration and exactly 1 `public static TEntity Create(...)` factory method as SSoT.
* [MUST] Require caller to pass explicit `DateTimeOffset now` parameter for time-dependent state mutations (prohibit `DateTime.UtcNow`).
* [MUST] Encapsulate collections as `IReadOnlyCollection<T>` backed by a private `List<T>` field initialized with `[]`.
```csharp
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Events;
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

        foreach (var existing in _versions.Where(v => v.Id != versionId && v.Status == TemplateVersionStatus.Published))
        {
            existing.Archive(now);
        }

        version.Publish(now);
        CurrentVersionId = versionId;
        SetUpdated(now);
        AddDomainEvent(new TemplatePublishedDomainEvent(Id, ProjectId, versionId, version.Version, now));
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
using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

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
* [MUST] Model small, immutable geometric or numeric measurements (<= 16 bytes, e.g. `PageDimension`, `PageMargin`, `DateRange`) as `readonly record struct`.
* [MUST] Execute validation guards immediately inside `Create(...)` throwing strongly-typed `DomainValidationException`.
```csharp
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

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

// Validated Communication VO
public sealed record class EmailAddress
{
    public string Value { get; }
    private EmailAddress(string value) => Value = value;
    public static EmailAddress Create(string? value) => new(Guard.ValidEmail(value, "Email address"));
    public static implicit operator string(EmailAddress email) => email.Value;
    public override string ToString() => Value;
}

// Struct Primitive VO (Layout Dimensions)
public readonly record struct PageDimension(float WidthMm, float HeightMm);

// Struct Primitive VO (Page Margins)
public readonly record struct PageMargin(float TopMm, float BottomMm, float LeftMm, float RightMm);

// Struct Primitive VO (Temporal Range)
public readonly record struct DateRange(DateTimeOffset StartDate, DateTimeOffset EndDate)
{
    public bool Contains(DateTimeOffset date) => date >= StartDate && date <= EndDate;
}

// Normalized URL-Friendly Tenant Slug VO
public sealed record class ProjectSlug
{
    public string Value { get; }
    private ProjectSlug(string value) => Value = Guard.NotBlank(value, "Project slug");
    public static ProjectSlug Create(string value) => new(value);
    public static implicit operator string(ProjectSlug slug) => slug.Value;
    public override string ToString() => Value;
}

// Cryptographic Integrity Hash VO
public sealed record class Sha256Hash
{
    public const int HashLength = 64;
    public string Value { get; }
    private Sha256Hash(string value)
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

## 4. Level 4: High-Salience Fatal Traps & Unified Micro-Diffs

### [TRAP-DOM-001]: Entity Struct Identity Collapse
```diff
- public struct Template : BaseEntity { }
+ public sealed class Template : BaseEntity<TemplateId> { }
```
*Rationale: Struct entities cause state copy-loss on pass-by-value and break EF Core ChangeTracker snapshot fidelity.*

### [TRAP-DOM-002]: Struct Value Object Default Invariant Breach
```diff
- public readonly record struct EmailAddress(string Value);
+ public sealed record class EmailAddress { public string Value { get; } private EmailAddress(string value) => Value = value; public static EmailAddress Create(string? value) => new(Guard.ValidEmail(value, "Email")); }
```
*Rationale: Struct value objects allow `default(EmailAddress)` bypass of guard checks, producing null values at runtime.*

### [TRAP-DOM-003]: Implicit ID Conversion Cross-Domain Pollution
```diff
- public static implicit operator Guid(TemplateId id) => id.Value; public static implicit operator TemplateId(Guid value) => new(value);
+ public static explicit operator Guid(TemplateId id) => id.Value; public static explicit operator TemplateId(Guid value) => new(value);
```
*Rationale: Implicit conversions allow silent accidental assignment of `TemplateId` to `ProjectId` or `DocumentId` slots without compiler error.*

### [TRAP-DOM-004]: Direct System Clock Access
```diff
- PublishedAt = DateTimeOffset.UtcNow;
+ PublishedAt = Guard.NotDefault(now, nameof(now));
```
*Rationale: Direct system clock access prevents deterministic unit testing via `FakeTimeProvider`.*

### [TRAP-DOM-005]: Public Property Mutation / Missing Encapsulation
```diff
- public TemplateName Name { get; set; }
+ public TemplateName Name { get; private set; } = null!;
```
*Rationale: Public setters allow external callers to mutate entity state bypassing aggregate invariants and validation rules.*

### [TRAP-DOM-006]: Direct Child Entity Creation Outside Aggregate Root
```diff
- var version = TemplateVersion.Create(templateId, 1, html, now);
+ var version = template.CreateDraftVersion(html, now);
```
*Rationale: Creating child entities outside their parent aggregate root breaks lifecycle boundaries and invariant validation.*
