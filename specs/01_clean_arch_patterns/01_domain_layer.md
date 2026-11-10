# [PATTERN-001]: 01_domain_layer.md — Domain Layer Design Patterns & SSoT Catalog

<context>
Category: 01_clean_arch_patterns
Domain: Domain Layer Architecture, Aggregates & Pure Business Invariants
SoleResponsibilities:
  - Pure Domain POCOs free of framework or persistence dependencies
  - `AggregateRoot<TId>` identity encapsulation and sole ownership of domain event harvesting
  - `BaseEntity` child entity modeling and `IConcurrencyAware` contract
  - Strongly-typed sequential UUIDv7 identities and immutable Value Objects
Target: `SmkDoc.Domain/` (`Entities/`, `ValueObjects/`, `Events/`, `Common/`)
Signal: [ARCH_EVOLUTION_DOMAIN]
</context>

---

## 1. Level 1: Affirmative Domain Invariants Matrix

### 1.1 Architectural Wiring & Layer Contracts
| Direction | Upstream Origin | Current Layer Role (Domain Core) | Downstream Destination |
|---|---|---|---|
| **Inbound Creation** | Application (`{Verb}{Aggregate}Command`) | Instantiates via SSoT Factory `{Aggregate}.Create(...)` | Encapsulated Domain Aggregate Root |
| **State Mutation** | Application (`{Verb}{Aggregate}Command`) | Executes expressive business verb (`entity.{Verb}`) | Mutates internal state & appends `IDomainEvent` |
| **Outbound Persistence**| Domain POCOs (`{Aggregate}`) | Hydrated/Persisted via `I{Aggregate}Repository` port | Infrastructure (`{Aggregate}Configuration` => PostgreSQL) |
| **Outbound Egress** | Domain State | Mapped to immutable Application DTOs | Presentation Layer (`{Aggregate}ResultDto`) |
| **Side-Effects** | Aggregate Lifecycle Events | Appends immutable `IDomainEvent` records | `AppDbContext` Outbox / Post-Commit Dispatchers |

### 1.2 Affirmative Domain Isolation Invariants
| Domain Architectural Concern | Affirmative Domain Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Purity & Isolation** | Pure .NET SDK C# 13 POCOs only. Zero external NuGet packages, EF Core, or ASP.NET Core references. | Project Boundary |
| **Persistence Ignorance** | Clean domain properties without ORM Data Annotations. All database mapping is strictly isolated in Infrastructure `IEntityTypeConfiguration<T>`. | Data Access |
| **State Encapsulation** | Strict `private set;` on all properties. Entity instantiation strictly through exactly 1 canonical static `Create(...)` factory method. | Entity Definition |
| **Temporal Determinism** | All time queries passed as explicit parameters (`DateTimeOffset now` or parameterized `TimeProvider`). Zero ambient clock queries. | Business Methods |
| **Aggregate Boundaries** | Reference other Aggregate Roots strictly by strongly-typed ID (`{Aggregate}Id`). Unidirectional foreign key references only. | Aggregate Boundary |
| **Identity Types** | Strongly-typed stack-allocated `readonly record struct {Aggregate}Id(Guid Value)` with explicit Guid cast operators. | Value Objects |
| **Global vs Tenant Identity** | `User` is a Global Identity (Zero `ProjectId` column, zero `IMustHaveProject`). Multi-project tenancy is mapped exclusively through `UserProjectRole`. | Identity Context |
| **Tenant Partitioning** | Tenant-scoped entities implement `IMustHaveProject` with mandatory fail-fast guard: `ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId))`. | Multi-Tenancy |
| **Child Entity Lifecycle** | Child entities created and mutated exclusively through their parent Aggregate Root (`aggregate.AddChild(...)`). Standard `Guid` identity inheriting `BaseEntity`. Zero domain event dispatching. | Entity Hierarchy |
| **Aggregate Boundaries** | Aggregate roots inherit `AggregateRoot<TId>`, encapsulating state and domain event collection. Referenced by other aggregates strictly via `{Aggregate}Id`. | Aggregate Root Boundary |
| **Concurrency Token Contract**| Mutable aggregates implement `IConcurrencyAware`, exposing `EntityId` and deterministic `GetConcurrencyToken()` (`(UpdatedAt ?? CreatedAt).ToUnixTimeMilliseconds().ToString()`) inherited from `BaseEntity<TId>`. | Optimistic Concurrency |
| **Decoupled Side-Effects** | Side-effects appended in-memory strictly on `AggregateRoot<TId>` as immutable `IDomainEvent` records and harvested post-commit by the application layer. | Event Outbox |
| **Stateless Domain Logic (GodServiceEliminationFirewall)** | Domain business invariants belong primarily inside Aggregate Roots. Multi-aggregate calculations are modeled as stateless, single-purpose Domain Services ($\le 2$ methods). Multi-action God domain services or catch-all helpers (`*Helper`) are strictly prohibited. | Domain Logic Boundary |

---

## 2. Level 2: Core Domain SSoT Catalogs & Cognitive Governance Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Sequential UUIDv7**: Chronological ordering eliminates PostgreSQL B-Tree index fragmentation while allowing client/caller ID pre-allocation without database round-trips.
* **Why Sealed Reference Classes for Entities**: Reference identity prevents struct-copy state loss and preserves EF Core ChangeTracker snapshot fidelity.
* **Why Explicit ID Operators**: Prohibits accidental cross-domain ID assignment (e.g. assigning a `ProjectId` to an `{Aggregate}Id` slot) at compile-time.
* **Why Global `User` vs `UserProjectRole`**: Modeling `User` with a `ProjectId` column forces duplicate user accounts across projects, breaks single-sign-on (SSO), and makes enterprise user management impossible. `User` is a Global Identity; multi-tenancy is scoped strictly via the `UserProjectRole` join entity.
* **Why Injected `DateTimeOffset now`**: Eliminates ambient system clock queries, guaranteeing 100% deterministic unit testing via `FakeTimeProvider`.
* **Why In-Memory Domain Events**: Decouples domain aggregates from messaging infrastructure; side-effects are dispatched strictly post-transaction.
* **Why Single-Purpose Domain Services over Fat Services (GodServiceEliminationFirewall):** Placing multi-aggregate business rules inside fat, multi-method domain services recreates the God Service anti-pattern inside the Domain Core. Domain logic must reside inside Aggregate Roots first, and any cross-aggregate service must be lean, stateless, and single-purpose.

### 2.2 Proactive Domain Firewalls
```yaml
DomainEncapsulationFirewall:
  Invariant: "Entity state properties must be encapsulated with private set."
  ActionOnViolation: "Remove public setters and expose expressive business mutation verbs."

GlobalIdentityFirewall:
  Invariant: "User entity must remain a Global Identity with zero ProjectId column."
  ActionOnViolation: "Remove ProjectId from User and map tenancy via UserProjectRole."

TemporalDeterminismFirewall:
  Invariant: "Domain entities must never query system clocks directly."
  ActionOnViolation: "Pass explicit DateTimeOffset now into factory methods and mutation verbs."

StronglyTypedIdFirewall:
  Invariant: "Cross-domain implicit conversions between Guid and strongly-typed IDs are prohibited."
  ActionOnViolation: "Enforce explicit cast operators to/from Guid on all ID structs."

DomainServiceBoundaryFirewall:
  Invariant: "Domain services must be 100% stateless and single-purpose (<= 2 methods). Catch-all *Helper classes or multi-action domain services are strictly prohibited."
  ActionOnViolation: "Decompose into dedicated single-purpose domain services or relocate logic into Aggregate Root verbs."
```

---

## 3. Level 3: Canonical Working Archetypes (Surgical AST Recipes)

### 3.1 Strongly-Typed UUIDv7 Identifier Struct Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-001]:
* Stack-allocated (16-byte) strongly-typed identifier.
* Explicit cast operators to/from `Guid` to prevent accidental cross-domain ID pollution.

```csharp
public readonly record struct {Aggregate}Id(Guid Value) : IComparable<{Aggregate}Id>
{
    public static {Aggregate}Id New() => new(Guid.CreateVersion7()); // Sequential UUIDv7
    public static {Aggregate}Id Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public int CompareTo({Aggregate}Id other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();

    // Explicit conversions: compile-time safety against accidental cross-domain ID binding
    public static explicit operator Guid({Aggregate}Id id) => id.Value;
    public static explicit operator {Aggregate}Id(Guid value) => new(value);

    public static bool operator ==({Aggregate}Id left, Guid right) => left.Value == right;
    public static bool operator !=({Aggregate}Id left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, {Aggregate}Id right) => left == right.Value;
    public static bool operator !=(Guid left, {Aggregate}Id right) => left != right.Value;
}
```

### 3.2 Base Entity, Aggregate Root & Concurrency Contracts (`IConcurrencyAware`, `BaseEntity<TId>`, `BaseEntity` & `AggregateRoot<TId>`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-002]:
* `IConcurrencyAware` defines the uniform identity and token extraction contract for optimistic concurrency verification.
* `BaseEntity<TId>` encapsulates strongly-typed ID, audit timestamps, and `EntityId`. Pure identity; zero domain events.
* `BaseEntity` encapsulates standard `Guid` identities for child entities.
* `AggregateRoot<TId>` inherits `BaseEntity<TId>`, implements `IConcurrencyAware` (`GetConcurrencyToken()`), and is the SOLE owner of in-memory domain event harvesting (`AddDomainEvent`, `ClearDomainEvents`).

```csharp
public interface IConcurrencyAware
{
    object EntityId { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
    string GetConcurrencyToken();
    string? GetAlternateConcurrencyToken() => null;
}

public abstract class BaseEntity<TId> where TId : struct, IComparable<TId>, IEquatable<TId>
{
    public TId Id { get; protected set; }
    public object EntityId => Id;
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(TId id, DateTimeOffset createdAt)
    {
        Id = Guard.NotEmpty(id, nameof(Id));
        CreatedAt = Guard.NotDefault(createdAt, nameof(CreatedAt));
    }

    protected void SetUpdated(DateTimeOffset now) => UpdatedAt = Guard.NotDefault(now, nameof(now));
}

public abstract class AggregateRoot<TId> : BaseEntity<TId>, IHaveDomainEvents, IConcurrencyAware
    where TId : struct, IComparable<TId>, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id, DateTimeOffset createdAt) : base(id, createdAt) { }

    public void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(Guard.NotNull(domainEvent, nameof(domainEvent)));

    public void ClearDomainEvents() => _domainEvents.Clear();

    public virtual string GetConcurrencyToken() =>
        (UpdatedAt ?? CreatedAt).ToUnixTimeMilliseconds().ToString();

    public virtual string? GetAlternateConcurrencyToken() => null;
}

public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public object EntityId => Id;
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid? id, DateTimeOffset createdAt)
    {
        Id = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.CreateVersion7();
        CreatedAt = Guard.NotDefault(createdAt, nameof(CreatedAt));
    }

    protected void SetUpdated(DateTimeOffset now) => UpdatedAt = Guard.NotDefault(now, nameof(now));

    public string GetConcurrencyToken() =>
        (UpdatedAt ?? CreatedAt).ToUnixTimeMilliseconds().ToString();
}
```

### 3.3 Tenant-Scoped Aggregate Root Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-003]:
* Sealed reference type inheriting `AggregateRoot<{Aggregate}Id>` and implementing `IMustHaveProject`.
* 1 Canonical static `Create(...)` factory method.
* Business mutation verbs following the 4-Step State Mutation Verb Cadence.

```csharp
public sealed class {Aggregate} : AggregateRoot<{Aggregate}Id>, IMustHaveProject
{
    // 1. Public Encapsulated Properties
    public ProjectId ProjectId { get; private set; }
    public {Aggregate}Name Name { get; private set; } = null!;
    public {Aggregate}Slug Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }

    // 2. Private Backing Child Collections
    private readonly List<{ChildEntity}> _children = [];
    public IReadOnlyCollection<{ChildEntity}> Children => _children.AsReadOnly();

    // 3. Parameterless Constructor (ORM Hydration)
    private {Aggregate}() { }

    // 4. Internal Constructor
    internal {Aggregate}(
        {Aggregate}Id? id,
        ProjectId projectId,
        {Aggregate}Name name,
        {Aggregate}Slug slug,
        DateTimeOffset now)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : {Aggregate}Id.New(), createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        IsActive = true;
    }

    // 5. Static SSoT Factory Method
    public static {Aggregate} Create(
        ProjectId projectId,
        {Aggregate}Name name,
        {Aggregate}Slug slug,
        DateTimeOffset now)
    {
        return new {Aggregate}(null, projectId, name, slug, now);
    }

    // 6. 4-Step State Mutation Verb Cadence
    public void {Verb}({Aggregate}Name newName, DateTimeOffset now)
    {
        // Step 1: Invariant Guard (Parameter validity)
        Guard.NotNull(newName, nameof(newName));

        // Step 2: Precondition Guard (Aggregate state invariants)
        if (!IsActive)
            throw new DomainRuleViolationException("Cannot mutate an inactive aggregate.");

        // Step 3: State Mutation (Encapsulated scalar transitions)
        Name = newName;
        SetUpdated(now);

        // Step 4: Event Harvesting (Record domain event for post-commit dispatch)
        AddDomainEvent(new {Aggregate}{Verb}edDomainEvent(Id, ProjectId, Name, now));
    }

    // 7. Child Entity Lifecycle Control
    public void Add{ChildEntity}(string propertyValue, DateTimeOffset now)
    {
        var child = {ChildEntity}.Create(Id, propertyValue, now);
        _children.Add(child);
        SetUpdated(now);
    }
}
```

### 3.4 Global Identity Aggregate Root Archetype (`User`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-004]:
* User is a Global Identity: zero `ProjectId` column and zero `IMustHaveProject`.
* Multi-tenancy memberships managed exclusively through `UserProjectRole` join entity.

```csharp
public sealed class User : BaseEntity<UserId>
{
    public EmailAddress Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public SystemRole SystemRole { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private readonly List<UserProjectRole> _projectRoles = [];
    public IReadOnlyCollection<UserProjectRole> ProjectRoles => _projectRoles.AsReadOnly();

    private User() { }

    public static User Create(EmailAddress email, string fullName, SystemRole systemRole, DateTimeOffset now) =>
        new(null, email, fullName, systemRole, now);

    internal User(UserId? id, EmailAddress email, string fullName, SystemRole systemRole, DateTimeOffset now)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : UserId.New(), createdAt: now)
    {
        Email = Guard.NotNull(email, nameof(Email));
        FullName = Guard.NotBlank(fullName, nameof(FullName), 150);
        SystemRole = Guard.NotNull(systemRole, nameof(SystemRole));
        IsActive = true;
    }

    public void AssignProjectRole(ProjectId projectId, RoleType role, DateTimeOffset now)
    {
        Guard.NotEmpty(projectId, nameof(ProjectId));
        Guard.NotNull(role, nameof(RoleType));

        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == projectId);
        if (existing != null)
        {
            existing.UpdateRole(role, now);
        }
        else
        {
            _projectRoles.Add(UserProjectRole.Create(Id, projectId, role, now));
        }

        SetUpdated(now);
        AddDomainEvent(new UserProjectRoleAssignedDomainEvent(Id, projectId, role, now));
    }

    public void RevokeProjectRole(ProjectId projectId, DateTimeOffset now)
    {
        Guard.NotEmpty(projectId, nameof(ProjectId));
        var existing = _projectRoles.FirstOrDefault(r => r.ProjectId == projectId);
        if (existing != null)
        {
            _projectRoles.Remove(existing);
            SetUpdated(now);
        }
    }
}
```

### 3.5 Value Object Archetypes (Reference Class & Struct)
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-005]:
* Validated domain concepts (names, slugs, emails): `sealed record class` with private constructor & static `Create(...)` factory.
* Small geometric/numeric measurements ($\le 16$ bytes): `readonly record struct`.

```csharp
// 1. Reference Validated Value Object (3-Step Cadence)
public sealed record class {Aggregate}Name
{
    public string Value { get; }
    private {Aggregate}Name(string value) => Value = value;

    public static {Aggregate}Name Create(string? value) =>
        new(Guard.NotBlank(value, "{Aggregate} name", 100));

    public static implicit operator string({Aggregate}Name name) => name.Value;
    public override string ToString() => Value;
}

// 2. Struct Primitive Measurement Value Object
public readonly record struct PageDimension(float WidthMm, float HeightMm);
public readonly record struct PageMargin(float TopMm, float BottomMm, float LeftMm, float RightMm);
```

### 3.6 Smart Enum Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-DOM-006]:
* Sealed class inheriting `Enumeration` with private `(int id, string name)` constructor and static instances.

```csharp
public sealed class {Domain}Type : Enumeration
{
    public static readonly {Domain}Type Standard = new(1, nameof(Standard));
    public static readonly {Domain}Type Advanced = new(2, nameof(Advanced));

    private {Domain}Type(int id, string name) : base(id, name) { }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-DOM-001]: Entity Struct Identity Collapse
```diff
- public struct {Aggregate} : BaseEntity { }
+ public sealed class {Aggregate} : BaseEntity<{Aggregate}Id> { }
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
- public static implicit operator Guid({Aggregate}Id id) => id.Value;
- public static implicit operator {Aggregate}Id(Guid value) => new(value);
+ public static explicit operator Guid({Aggregate}Id id) => id.Value;
+ public static explicit operator {Aggregate}Id(Guid value) => new(value);
```
*Rationale: Implicit conversions allow silent accidental assignment of `{Aggregate}Id` to other domain ID slots without compiler error.*

### [TRAP-DOM-004]: Direct Child Entity Creation Outside Aggregate Root
```diff
- var child = {ChildEntity}.Create(aggregateId, payload, now);
+ var child = aggregate.Add{ChildEntity}(payload, now);
```
*Rationale: Creating child entities outside their parent aggregate root breaks lifecycle boundaries and invariant validation.*

### [TRAP-DOM-005]: Leaking ProjectId into Global User Entity (Tenancy Confusion)
```diff
- public sealed class User : BaseEntity<UserId>, IMustHaveProject
- {
-     public ProjectId ProjectId { get; private set; }
- }
+ public sealed class User : BaseEntity<UserId>
+ {
+     // User is a Global Identity; tenancy is bridged strictly via UserProjectRole
+     private readonly List<UserProjectRole> _projectRoles = [];
+ }
```
*Rationale: Attaching `ProjectId` directly to `User` binds credentials to a single project, preventing multi-tenant organization access and breaking single-sign-on (SSO).*

### [TRAP-DOM-006]: Aggregate Root Inheriting BaseEntity Instead of AggregateRoot
```diff
- public sealed class {Aggregate} : BaseEntity<{Aggregate}Id>, IMustHaveProject
+ public sealed class {Aggregate} : AggregateRoot<{Aggregate}Id>, IMustHaveProject
```
*Rationale: Domain event harvesting (`AddDomainEvent`, `DomainEvents`, `ClearDomainEvents`) is strictly isolated to `AggregateRoot<TId>`. Child entities inherit `BaseEntity` without domain event collections.*

### [TRAP-DOM-007]: Fat Multi-Method Domain Service or Catch-All Domain Helper
```diff
- // Multi-action Fat Domain Service or static Helper class collecting unrelated methods
- public sealed class TemplateDomainService {
-     public void Validate(...) { ... }
-     public void CalculatePricing(...) { ... }
-     public void FormatFields(...) { ... }
- }
+ // Invariant encapsulated inside Aggregate Root verb, or dedicated single-purpose domain service
+ template.Publish(version, now);
```
*Rationale: Concrete Domain violation of system-wide GodServiceEliminationFirewall. Multi-action domain services and catch-all `*Helper` classes degenerate into God Classes and strip domain entities of rich business behaviors (Anemic Domain Model). Enforce rich Aggregate Root verbs and single-purpose domain logic.*
