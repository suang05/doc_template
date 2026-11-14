# [CORE-002]: 02_coding_conventions.md — Language Idioms, Syntax Grammar & Clean Code Standards

<context>
Category: 00_architecture_core
Domain: Language Idioms, Syntax Grammar & Clean Code Standards
SoleResponsibilities:
  - Modern .NET 10 / C# 14 idioms (primary constructors, collection expressions, `TimeProvider`, `field` keyword where useful)
  - Universal Linear Cadences (Controller, UseCase, Entity, ValueObject, Adapter, UnitTest) with the SINGLE canonical code listing of each
  - Step comment anchors in cadence examples and `// <Why: ...>` notes for non-obvious invariants
  - One-primary-type-per-file and file-scoped namespace invariants
  - Baseline tooling rules (nullable, analyzers, logging, exceptions, async, EF Core read rules)
Target: `backend-v2/` (frontend conventions: see `02_frontend_patterns/`; Cadence 7 is kept here only until it is moved)
Platform baseline: declared in `Directory.Build.props` (TargetFramework `net10.0`, LangVersion default for the SDK). Prose does not hard-code a language version.
Signal: [ARCH_EVOLUTION_CONVENTIONS]
</context>

---

## 1. Level 1: Language & Syntax Invariants Matrix

| Concern | Invariant (positive SSoT law) | Enforcement |
|---|---|---|
| **Namespace** | File-scoped namespaces (`namespace SmkDoc.Domain.Authoring;`). | `.editorconfig` `csharp_style_namespace_declarations = file_scoped:error` |
| **Constructor DI** | Primary constructors for DI-injected classes, camelCase parameters. When a captured parameter must be immutable or is used in many members, an explicit `private readonly` field is allowed. Domain entities do not use primary constructors (EF rehydration). | IDE0290 as suggestion, review |
| **Collections** | Collection expressions (`[]`) when the target type is known. | IDE0300-IDE0305 |
| **Layout** | One primary type per file (`{TypeName}.cs`), co-located in feature slices. | Analyzer + NetArchTest |
| **Control flow** | Flat code with early-return guard clauses. Nesting depth ≤ 2 and cognitive complexity ≤ 15 per method. | Sonar S3776 / CA1502 as errors |
| **Controller** | No business logic: map request → command/query, call exactly one UseCase, return the result. Roughly 5 statements is a guideline. | NetArchTest, complexity analyzer |
| **UseCase** | Four-step PEMR: Parse → Ensure → Mutate → Return (§3.2). | Review + tests |
| **Async** | End-to-end `async`/`await`; **`CancellationToken ct` is a required parameter** on UseCase, repository and port methods (no `= default`); forward it to every I/O call. `= default` is allowed only on public library-style APIs. | CA2016 as error, VSTHRD002/VSTHRD100 |
| **Time** | Never read the ambient clock. Application injects `TimeProvider`; Domain receives `DateTimeOffset now`. (Documented exception: PostgreSQL `now()` for job leases, see 01 §1.4.1.) | BannedApiAnalyzers |
| **Nested construction** | Avoid constructing objects inside the argument list of a call when it is more than one level deep or has more than three arguments. Simple `new X(a)` arguments are fine. | Review |
| **Optimistic concurrency** | Aggregates have an explicit `long Version` (`IConcurrencyAware`). Update/transition commands carry `long ExpectedVersion` (parsed from `If-Match: "v{version}"` by `IfMatchVersionBinder`). Step 2 (Ensure) verifies `entity.Version == command.ExpectedVersion` fail-fast (`PreconditionFailedException`), and EF Core maps `Version` as `.IsConcurrencyToken()` so a mid-flight race at commit also raises `PreconditionFailedException` (`412`). No ambient context and no ORM methods on Domain repositories (01 §1.4.4, 06 §2.4). | Unit + integration tests |
| **Tests** | Arrange-Act-Assert with explicit setup (DAMP), `FakeTimeProvider`, negative-path tests, Testcontainers for persistence behaviour. | Review |
| **Why notes** | `// <Why: ...>` only for non-obvious guards, invariants and limits. Step comments are used in cadence examples; production code prefers well-named methods over step comments. | Review |
| **YAGNI hygiene** | Remove dead code, unused parameters and unused variables in the same change. | IDE0051, IDE0052, IDE0060, CS0168/CS0219 as errors |

---

## 2. Level 2: Engineering Rationales & Syntax Firewalls

### 2.1 Rationale
* **Primary constructors** remove field-assignment boilerplate. Trade-off: captured parameters are mutable, so use an explicit readonly field where that matters.
* **Collection expressions** give one idiom for arrays, lists and spans; the target type must be known (`var x = []` is invalid), and `IEnumerable<T>` targets still allocate.
* **File-scoped namespaces** remove one indentation level in every file.
* **DAMP tests**: helper chains hide why a test fails; explicit arrange/act/assert keeps failures self-explanatory.
* **Required `ct`**: a default value makes it easy to forget propagation, which defeats cancellation and request-abort handling.
* **Injected time** makes behaviour deterministic and testable.
* **Why notes** record intent where the code alone cannot; overuse becomes noise, so they are limited to non-obvious cases.
* **YAGNI hygiene** keeps the code base small enough to review.

### 2.2 Syntax Firewalls
```yaml
NamespaceFirewall:
  Invariant: "File-scoped namespaces only."
  Enforcement: ".editorconfig csharp_style_namespace_declarations=file_scoped:error"

AsyncFirewall:
  Invariant: "No sync-over-async (.Result, .Wait(), GetAwaiter().GetResult()), no async void, ct required and forwarded."
  Enforcement: "VSTHRD002, VSTHRD100, CA2016, CA2007 not required (ASP.NET Core has no SynchronizationContext)"

ClockFirewall:
  Invariant: "No DateTime.Now/UtcNow, DateTimeOffset.Now/UtcNow, Environment.TickCount in Application/Domain."
  Enforcement: "Microsoft.CodeAnalysis.BannedApiAnalyzers BannedSymbols.txt"

HygieneFirewall:
  Invariant: "No unreferenced private members, unused parameters or commented-out code after a change."
  Enforcement: "IDE0051, IDE0052, IDE0060 as errors in CI"
```

---

## 3. Level 3: Universal Linear Cadences (Canonical Recipes)

> MANDATORY CANONICAL BLUEPRINT [RULE-CONV-001]:
* Each component follows its cadence. Examples use architectural metasyntax (`{Verb}{Aggregate}`) and omit `using` boilerplate.
* Classes are `sealed` by default.
* Naming of verbs, suffixes and variables follows [04_naming_standards.md](04_naming_standards.md). HTTP status mapping follows [06_api_contract_standards.md §3.3](06_api_contract_standards.md).

### 3.1 Cadence 1: Controller Dispatcher
```csharp
[HttpPost]
[Idempotent]                                                       // required for creating operations that are not naturally idempotent (see 06)
[ProducesResponseType<ApiResponse<{Aggregate}ResultDto>>(StatusCodes.Status201Created)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public async Task<IActionResult> {Verb}([FromBody] {Verb}{Aggregate}Request request, CancellationToken ct)
{
    // Step 1: Request -> positional command
    var command = new {Verb}{Aggregate}Command(request.Name, request.Slug, request.Category);

    // Step 2: Single UseCase invocation
    var result = await {verb}{Aggregate}UseCase.ExecuteAsync(command, ct);

    // Step 3: Success envelope; 201 + Location (CreatedAtAction builds an absolute-path Location)
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result));
}
```
Errors are never constructed in controllers; exceptions reach `IExceptionHandler` which emits RFC 9457 `ProblemDetails` (table in 06). Rate limiting is applied by a global limiter with named policies for opt-in overrides (03), not by per-action attributes that can be forgotten. The local variable name `result` is a house convention for consistency, not a technical requirement.

### 3.2 Cadence 2: UseCase 4-Step PEMR (single canonical listing)

**Archetype A: entity-creating / key-consuming** (needs the project id for entity creation, storage paths or cache keys):
```csharp
public sealed class Create{Aggregate}UseCase(
    I{Aggregate}Repository {aggregate}Repository,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<Create{Aggregate}Command, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync(Create{Aggregate}Command command, CancellationToken ct)
    {
        // Step 1: Parse (resolve tenant and build value objects first; invalid input fails before any I/O)
        var projectId = projectContext.GetRequiredProjectId();   // programming error if unresolved (01 §1.1.2)
        var name = {Aggregate}Name.Create(command.Name);
        var slug = {Aggregate}Slug.Create(command.Slug);

        // Step 2: Ensure (fast-path uniqueness check on the NORMALIZED value object, scoped by project)
        if (await {aggregate}Repository.SlugExistsAsync(projectId, slug, ct))
            throw ConflictException.DuplicateSlug(slug.Value);

        // Step 3: Mutate (domain factory with injected time)
        var entity = {Aggregate}.Create(projectId, name, slug, command.Category, timeProvider.GetUtcNow());
        await {aggregate}Repository.AddAsync(entity, ct);

        // Step 4: Return (commit entity + outbox atomically, then map)
        await unitOfWork.CommitAsync(ct);                        // translates a unique-index violation (PostgreSQL 23505) into ConflictException
        return entity.ToResultDto();
    }
}
```
Rules behind the sample:
* The uniqueness check is a fast path for a good error message; **the database unique index `(project_id, slug)` is the actual guarantee**, and `IUnitOfWork` maps its violation to `ConflictException`.
* Mapping lives in one `ToResultDto()` extension per aggregate (Application), reused by Create/Update/Get.
* The validator (`{Verb}{Aggregate}CommandValidator`) runs in `ValidationDecorator` before `ExecuteAsync`; UseCases do not call validators.

**Archetype B: by-ID or collection UseCase** (no `ICurrentProjectContext`; tenant scoping comes from the layered isolation in 01 §1.1.3):
```csharp
public sealed class Rename{Aggregate}UseCase(
    I{Aggregate}Repository {aggregate}Repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<Rename{Aggregate}Command, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync(Rename{Aggregate}Command command, CancellationToken ct)
    {
        // Step 1: Parse
        var id = new {Aggregate}Id(command.Id);
        var name = {Aggregate}Name.Create(command.NewName);

        // Step 2: Ensure (not found, then explicit optimistic-concurrency check before any mutation or I/O)
        var entity = await {aggregate}Repository.FindByIdAsync(id, ct)
            ?? throw NotFoundException.For<{Aggregate}>(id);          // other tenants' rows are indistinguishable from missing rows
        if (entity.Version != command.ExpectedVersion)
            throw PreconditionFailedException.VersionMismatch();      // pure Domain property check; EF .IsConcurrencyToken() also guards commit

        // Step 3: Mutate (intention-revealing domain verb, not a generic Update)
        entity.Rename(name, timeProvider.GetUtcNow());

        // Step 4: Return
        await unitOfWork.CommitAsync(ct);                             // translates DbUpdateConcurrencyException -> PreconditionFailedException (412)
        return entity.ToResultDto();
    }
}
```
**Query UseCases** read through a projection port and never load aggregates only to map them:
```csharp
public sealed class Get{Aggregate}UseCase(I{Aggregate}ReadModel readModel) : IUseCase<Get{Aggregate}Query, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync(Get{Aggregate}Query query, CancellationToken ct) =>
        await readModel.FindByIdAsync(query.Id, ct) ?? throw NotFoundException.For<{Aggregate}>(query.Id);
}
```
A UseCase may throw `NotFoundException`, `ConflictException`, `PreconditionFailedException`, `SchemaValidationException`, `ForbiddenException` (resource-level permission) and Domain exceptions. It never throws `UnauthorizedException` (authentication is Tier 1).

### 3.3 Cadence 3: Domain Entity State Mutation Verb
```csharp
public sealed class {Aggregate} : AggregateRoot<{Aggregate}Id>, IMustHaveProject
{
    public {Aggregate}Name Name { get; private set; }
    public {Aggregate}Status Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Rename({Aggregate}Name newName, DateTimeOffset now)
    {
        // Step 1: Precondition guard (aggregate state invariant)
        if (Status == {Aggregate}Status.Archived)
            throw BusinessRuleViolationException.ArchivedAggregate(Id.Value);   // carries an ErrorCode, no free text

        // Step 2: State mutation (SetUpdated advances UpdatedAt and increments Version)
        Name = newName;
        SetUpdated(now);

        // Step 3: Record the domain event (persisted atomically by the outbox in IUnitOfWork.CommitAsync)
        AddDomainEvent(new {Aggregate}RenamedDomainEvent(Id, Name, now));
    }
}
```
Value-object parameters are non-nullable (nullable reference types are enabled), so no `Guard.AgainstNull` is needed for them. Event names use `{Aggregate}{PastTenseVerb}DomainEvent`.

### 3.4 Cadence 4: Value Object (immutable, self-validating)
```csharp
public sealed partial record {Aggregate}Slug
{
    public const int MaxLength = 100;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SlugPattern();

    private {Aggregate}Slug(string value) => Value = value;

    public string Value { get; }

    // Step 1: Throwing factory used by UseCases (fail-fast)
    public static {Aggregate}Slug Create(string raw) =>
        TryCreate(raw, out var slug) ? slug : throw DomainValidationException.InvalidFormat(nameof({Aggregate}Slug));  // never echoes raw input

    // Step 2: Non-throwing factory (validators, parsing)
    public static bool TryCreate(string? raw, [NotNullWhen(true)] out {Aggregate}Slug? slug)
    {
        slug = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !SlugPattern().IsMatch(normalized)) return false;

        slug = new {Aggregate}Slug(normalized);
        return true;
    }

    // Step 3: Value equality comes from the record; explicit conversion only (no implicit conversion to string)
    public override string ToString() => Value;
    public static explicit operator string({Aggregate}Slug slug) => slug.Value;
}
```
Why a `sealed record` class and not a `record struct`: a struct can be `default`-constructed (Value = null) without passing the factory, which breaks the "self-validating" guarantee. Use a struct only for small value objects whose `default` is itself valid (e.g. a strongly typed `Guid` id). Persistence uses an EF value converter.

### 3.5 Cadence 5: Outbound Infrastructure Adapter
```csharp
public sealed class {ExternalService}Adapter(HttpClient httpClient, ILogger<{ExternalService}Adapter> logger) : I{ExternalService}Port
{
    public async Task<Stream> ExecuteOperationAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        // Step 1: Build the request (payload is actually used; relative URI against the configured base address)
        using var request = new HttpRequestMessage(HttpMethod.Post, "operation") { Content = new ReadOnlyMemoryContent(payload) };

        // Step 2: Send (timeout, retry, circuit breaker and bulkhead come from the resilience handler registered below)
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException)
        {
            logger.LogWarning(exception, "External service call failed.");
            throw new ExternalServiceUnavailableException("External service is unavailable.", exception);
        }

        // Step 3: Map failures (dispose the response on every failure path)
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw ExternalServiceException.FromStatus(status);
        }

        // Step 4: Hand over ownership: disposing the returned stream disposes the response and its connection
        var body = await response.Content.ReadAsStreamAsync(ct);
        return new ResponseDisposingStream(body, response);
    }
}
```
```csharp
// Composition root (Infrastructure module): resilience and network policy live here, not in the adapter
services.AddHttpClient<I{ExternalService}Port, {ExternalService}Adapter>(client => client.BaseAddress = options.BaseAddress)
        .AddStandardResilienceHandler();                    // Microsoft.Extensions.Http.Resilience
```
Rules: no per-instance `SemaphoreSlim` (a typed client is transient; use the resilience bulkhead or a singleton limiter). The **Outbound Guard** (SSRF validation and IP pinning, see 03) applies only when the target host is tenant-supplied; fixed internal endpoints (Gotenberg, MinIO) use configured addresses.

### 3.6 Cadence 6: Unit Test (DAMP Arrange-Act-Assert)
```csharp
public sealed class Create{Aggregate}UseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_PersistsEntityAndCommits()
    {
        // Step 1: Arrange (explicit stubs, fake clock, captured entity)
        var projectId = new ProjectId(Guid.NewGuid());
        var repository = Substitute.For<I{Aggregate}Repository>();
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.GetRequiredProjectId().Returns(projectId);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        {Aggregate}? saved = null;
        _ = repository.AddAsync(Arg.Do<{Aggregate}>(entity => saved = entity), Arg.Any<CancellationToken>());   // capture argument (NSubstitute)

        var sut = new Create{Aggregate}UseCase(repository, projectContext, unitOfWork, timeProvider);
        var command = new Create{Aggregate}Command("Standard Name", "Standard-Slug", "Default");

        // Step 2: Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Step 3: Assert (state, not only interactions)
        result.Slug.Should().Be("standard-slug");                       // normalized
        saved.Should().NotBeNull();
        saved!.ProjectId.Should().Be(projectId);
        saved.CreatedAt.Should().Be(timeProvider.GetUtcNow());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugExists_ThrowsConflictAndDoesNotCommit()
    {
        // Arrange
        var repository = Substitute.For<I{Aggregate}Repository>();
        repository.SlugExistsAsync(Arg.Any<ProjectId>(), Arg.Any<{Aggregate}Slug>(), Arg.Any<CancellationToken>()).Returns(true);
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.GetRequiredProjectId().Returns(new ProjectId(Guid.NewGuid()));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var sut = new Create{Aggregate}UseCase(repository, projectContext, unitOfWork, new FakeTimeProvider());

        // Act
        var act = () => sut.ExecuteAsync(new Create{Aggregate}Command("Name", "taken", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
```
Archetype B tests omit the `projectContext` stub. Behaviour that depends on the database (unique index, query filters, RLS, version conflicts) is covered by Testcontainers integration tests, not by mocks. Reference: [05_testing_archetypes.md](../01_clean_arch_patterns/05_testing_archetypes.md).

### 3.7 Cadence 7: Next.js Async Server Component
> **SSoT Delegation:** The canonical Next.js 15 Async Server Component (`TemplateDetailsPage` using `Promise.all([params, auth()])`, `redirect('/login')`, and the `Project-Id` header) is governed in [01_app_router_and_data_fetching.md §3.2](../02_frontend_patterns/01_app_router_and_data_fetching.md#32-nextjs-15-async-route-page-archetype-srcappapptemplatesidpagetsx).

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-CONV-001]: Legacy Block Namespace & Constructor Assignment Ceremony
```diff
- namespace SmkDoc.Application.Modules.Authoring {
-     public class {Verb}Service {
-         private readonly I{Aggregate}Repository _repo;
-         public {Verb}Service(I{Aggregate}Repository repo) { _repo = repo; }
-     }
- }
+ namespace SmkDoc.Application.Modules.Authoring;
+
+ public sealed class {Verb}{Aggregate}UseCase(I{Aggregate}Repository {aggregate}Repository) { }
```
*Rationale: file-scoped namespaces and primary constructors remove ceremony.*

### [TRAP-CONV-002]: Sync-over-Async and Optional Cancellation Token
```diff
- public {Aggregate}ResultDto Get(Guid id, CancellationToken token = default) {
-     var entity = {aggregate}Repository.FindByIdAsync(id).Result;
+ public async Task<{Aggregate}ResultDto> ExecuteAsync(Get{Aggregate}Query query, CancellationToken ct) {
+     var entity = await {aggregate}Repository.FindByIdAsync(query.Id, ct) ?? throw NotFoundException.For<{Aggregate}>(query.Id);
      return entity.ToResultDto();
  }
```
*Rationale: `.Result` blocks thread-pool threads (starvation). A defaulted `ct` lets callers silently skip propagation.*

### [TRAP-CONV-003]: Legacy Collection Initializers
```diff
- List<string> tags = new List<string>();
- int[] codes = new int[] { 200, 404 };
+ List<string> tags = [];
+ int[] codes = [200, 404];
```

### [TRAP-CONV-004]: Deeply Nested Construction Hiding the Boundary
```diff
- var result = await {verb}UseCase.ExecuteAsync(new {Verb}Query(Parse(request.Filter, new Paging(request.Page, request.Size, new Sort(request.By)))), ct);
+ var paging = new Paging(request.Page, request.Size, new Sort(request.By));
+ var query = new {Verb}Query(Parse(request.Filter, paging));
+ var result = await {verb}UseCase.ExecuteAsync(query, ct);
```
*Rationale: simple `new X(a)` arguments are fine; extract when construction nests deeper than one level or has many arguments.*

### [TRAP-CONV-005]: Nested Conditionals vs Linear Guard Clauses (and the right exceptions)
```diff
- if (user != null) { if (user.IsActive) { if (user.HasPermission("edit")) { return ExecuteInternal(); } } }
- throw new ForbiddenException();
+ if (!user.IsActive) throw BusinessRuleViolationException.InactiveUser(user.Id);
+ if (!user.HasPermission("edit")) throw new ForbiddenException();
+ return ExecuteInternal();
```
*Rationale: early guards keep the flow flat. Nullability is handled by nullable reference types. UseCases never throw `UnauthorizedException` (Tier 1 owns authentication).*

### [TRAP-CONV-006]: Speculative Parameters and Dead Code
```diff
- public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, bool? dryRun, CancellationToken ct) { ... }
- private void ObsoleteHelper() { }
+ public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct) { ... }
```

### [TRAP-CONV-007]: Value Object That Allows `default` or Implicit String Conversion
```diff
- public readonly record struct {Aggregate}Slug { public string Value { get; } ... public static implicit operator string({Aggregate}Slug s) => s.Value; }
+ public sealed partial record {Aggregate}Slug { private {Aggregate}Slug(string value) => Value = value; ... }   // no default instance, no implicit conversion
```

### [TRAP-CONV-008]: Per-Instance Throttle in a Transient Adapter
```diff
- public sealed class {ExternalService}Adapter(HttpClient httpClient) { private readonly SemaphoreSlim _throttler = new(10, 10); ... }
+ services.AddHttpClient<I{ExternalService}Port, {ExternalService}Adapter>().AddStandardResilienceHandler();   // global, per-client policy
```

---

## 5. Level 5: Baseline Tooling & Language Rules

| Area | Rule |
|---|---|
| Compiler | `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-all</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`; central `Directory.Build.props` and `Directory.Packages.props` |
| Analyzers | Roslyn analyzers, SonarAnalyzer.CSharp, VSTHRD, BannedApiAnalyzers; severities in `.editorconfig`; `dotnet format --verify-no-changes` in CI |
| Logging | Message templates (no string interpolation), `[LoggerMessage]` source generator on hot paths, never log secrets, connection strings or PII, include correlation id, log once at the boundary (no log-and-rethrow) |
| Exceptions | Catch specific types; `throw;` not `throw ex;`; handle `OperationCanceledException` explicitly (client abort is not an error); exceptions carry an `ErrorCode` and no free-text user input |
| Async | `Async` suffix, no `async void`, `ValueTask` only when measured, `IAsyncEnumerable<T>` for streaming |
| API surface | `internal` by default, `required` members, XML docs on public API, `sealed` classes, records for immutable data |
| EF Core | `AsNoTracking` and projections for reads, explicit `Include`, no lazy loading, migrations reviewed, `Add`/`AddRange` (not `AddAsync`) unless a value generator needs it |
| Security | No secrets in code or config files, parameterized SQL only, constant-time compare for secrets (see 03) |
| Testing | `FakeTimeProvider`, test data builders, Testcontainers PostgreSQL, architecture tests in CI |
