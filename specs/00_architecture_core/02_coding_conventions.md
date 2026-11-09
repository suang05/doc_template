# [CORE-002]: 02_coding_conventions.md — Language Idioms, Syntax Grammar & Clean Code Standards

<context>
Category: 00_architecture_core
Domain: Language Idioms, Syntax Grammar & Clean Code Standards
SoleResponsibilities:
  - Modern .NET 10 / C# 13 language idioms (Primary Constructors, Collection Expressions, TimeProvider)
  - 7 Universal Linear Cadences (Controller, UseCase, Entity, ValueObject, Adapter, UnitTest, Page)
  - Step comment anchors (`// Step N: ...`) and dense inline rationale (`// <Why: ...>`)
  - Single-type-per-file and file-scoped namespace invariants
Target: Full Stack (`backend-v2/` and `frontend-v2/`)
Signal: [ARCH_EVOLUTION_CONVENTIONS]
</context>

---

## 1. Level 1: Affirmative Language & Syntax Invariants Matrix

| Architectural & Syntax Concern | Affirmative Idiomatic Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Namespace Declaration** | File-scoped namespaces at file head (`namespace SmkDoc.Domain.Entities;`). | Head of file |
| **Constructor DI** | C# 13 Primary Constructors with pure camelCase parameters (`public sealed class Foo(IBar bar)`). | Class declaration |
| **Collection Allocation** | C# 12+ Collection Expressions (`[]`) for spans, arrays, and lists. | Universal |
| **Modularity & Layout** | Strict 1-Primary-Type-Per-File (`{TypeName}.cs`) co-located within feature slices. | Filesystem |
| **Execution Flow** | Flat linear execution with early return guards. Maximum nesting depth $\le 2$ levels. | Method body |
| **Controller Rhythm** | Pure 3-Step Dispatcher ($\le 5$ LOC): (1) Map Input => (2) Execute UseCase => (3) Return Envelope. | Presentation |
| **UseCase Rhythm** | Pure 4-Step PEMR Pipeline: (1) Parse/Guard => (2) Ensure => (3) Mutate => (4) Return ResultDto. | Application |
| **Asynchronous I/O** | 100% pure `async`/`await` pipeline forwarding standardized `CancellationToken ct = default`. | Method body |
| **Variable Assignment** | Explicit intermediate local variable assignment before passing into method invocations. | Universal |
| **Temporal Queries** | Parameterized `TimeProvider` or explicit `DateTimeOffset now`. | Temporal logic |
| **Concurrency Guard** | Ambient optimistic concurrency via global `ETagFilter`, `IConcurrencyContext`, and `ConcurrencySaveChangesInterceptor`. | Cross-Cutting Pipeline |
| **Traffic Protection** | Declarative endpoint rate limiting via `[EnableRateLimiting("...")]` policies. | Controller Action |
| **Test Rhythm** | 3-Phase DAMP Arrange-Act-Assert (AAA) layout with explicit test setups. | Test suites |
| **Inline Attention Anchor** | Universal `// <Why: {causal-intent}>` annotations for non-obvious guards, invariants, limits, and steps. | Universal |

---

## 2. Level 2: Engineering Rationales & Cognitive Interceptors

### 2.1 Engineering Rationale & Causal Decisions
* **Why C# 13 Primary Constructors**: Eliminates boilerplate field assignment ceremony (`this._repo = repo`), reduces token count by 15-20%, and aligns parameter names with DI registration.
* **Why Collection Expressions (`[]`)**: Standardizes collection instantiation across spans, arrays, and lists in a single, high-performance idiom optimized by Roslyn.
* **Why File-Scoped Namespaces**: Removes 4 whitespace characters of horizontal indentation across all files, maximizing line-length readability and eliminating token waste.
* **Why DAMP Over DRY in Tests**: Over-abstracted helper chains hide test failure rationale. Explicit Arrange-Act-Assert (AAA) ensures failures are self-evident and self-documenting.
* **Why Universal Inline Why-Anchors (`// <Why: ...>`)**: Eliminates attention leakage in downstream LLMs by colocating causal rationale directly on the target syntax token (Attention Proximity). Avoids multi-paragraph prose while clarifying non-obvious invariants and preventing accidental regression.

### 2.2 Proactive Syntax Firewalls
```yaml
NamespaceStructureFirewall:
  Invariant: "All C# files must use file-scoped namespaces exclusively."
  ActionOnViolation: "Strip curly brace namespace wrappers and declare top-level namespace."

PrimaryConstructorFirewall:
  Invariant: "All dependency-injected classes must use C# 13 Primary Constructors."
  ActionOnViolation: "Eliminate manual private readonly fields and constructor assignment blocks."

AsyncPurityFirewall:
  Invariant: "All I/O operations must be fully asynchronous."
  ActionOnViolation: "Eliminate synchronous blocking (.Result, .Wait()) and forward CancellationToken ct."

InstantiationClarityFirewall:
  Invariant: "Disallow inlined argument instantiation inside method calls."
  ActionOnViolation: "Extract nested instantiation into explicit intermediate local variable."
```

---

## 3. Level 3: The 6 Universal Linear Cadences (Surgical AST Recipes)

>> MANDATORY CANONICAL BLUEPRINT [RULE-CONV-001]:
* All components must follow their designated linear cadence.
* All code examples strictly use Architectural Metasyntax (`{Verb}{Aggregate}`) with zero `using` boilerplate.
* Mark classes `sealed` by default.

### 3.1 Cadence 1: Controller 3-Step Dispatcher ($\le 5$ LOC)
```csharp
[HttpPost]
[ProducesResponseType(typeof(ApiResponse<{Aggregate}ResultDto>), StatusCodes.Status201Created)]
public async Task<IActionResult> {Verb}([FromBody] {Verb}{Aggregate}Request request, CancellationToken ct)
{
    // Step 1: Explicit Input Translation (Request -> Positional Command)
    var command = new {Verb}{Aggregate}Command(request.Name, request.Slug, request.Category);

    // Step 2: UseCase Invocation (Variable named 'result', strictly no 'response')
    var result = await {verb}{Aggregate}UseCase.ExecuteAsync(command, ct);

    // Step 3: Envelope Return (Location header + RFC 7807/ApiResponse envelope)
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result));
}
```

### 3.2 Cadence 2: UseCase 4-Step PEMR Pipeline
```csharp
public sealed class {Verb}{Aggregate}UseCase(
    I{Aggregate}Repository {aggregate}Repository,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto>
{
    public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct = default)
    {
        // Step 1: Parse & Guard (Fail-fast ambient tenant & parameter invariants)
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // Step 2: Ensure Preconditions (Business rules & slug collision queries)
        if (await {aggregate}Repository.SlugExistsAsync(command.Slug, ct))
            throw ConflictException.DuplicateSlug(command.Slug);

        // Step 3: Mutate State (Domain state transition via expressive verb with injected time)
        var now = timeProvider.GetUtcNow(); // Injected clock: deterministic testability
        var entity = {Aggregate}.Create(projectContext.ProjectId, {Aggregate}Name.Create(command.Name), new {Aggregate}Slug(command.Slug), command.Category, now);
        await {aggregate}Repository.AddAsync(entity, ct);

        // Step 4: Return ResultDto (Atomic transaction commit & immutable output record)
        await unitOfWork.CommitAsync(ct);
        return new {Aggregate}ResultDto(entity.Id.Value, entity.Name.Value, entity.Slug.Value, entity.Category);
    }
}
```

### 3.3 Cadence 3: Domain Entity 4-Step State Mutation Verb
```csharp
public sealed class {Aggregate} : AggregateRoot<{Aggregate}Id>
{
    public {Aggregate}Name Name { get; private set; }
    public {Aggregate}Status Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void {Verb}({Aggregate}Name newName, DateTimeOffset now)
    {
        // Step 1: Invariant Guard (Parameter validity)
        Guard.AgainstNull(newName, nameof(newName));

        // Step 2: Precondition Guard (Aggregate state invariants)
        if (Status == {Aggregate}Status.Archived)
            throw new DomainRuleViolationException("Cannot mutate an archived aggregate.");

        // Step 3: State Mutation (Encapsulated scalar transitions)
        Name = newName;
        UpdatedAt = now;

        // Step 4: Event Harvesting (Record domain event for outbox dispatch)
        AddDomainEvent(new {Aggregate}{Verb}edDomainEvent(Id, Name, now));
    }
}
```

### 3.4 Cadence 4: Value Object 3-Step Self-Validating Record Struct
```csharp
public readonly record struct {Aggregate}Slug
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    // Step 1: Private Parameterized Constructor (Encapsulated internal state)
    private {Aggregate}Slug(string value) => Value = value;

    public string Value { get; }

    // Step 2: Pure Static Factory with Fail-Fast Validation
    public static {Aggregate}Slug Create(string raw)
    {
        Guard.AgainstNullOrWhiteSpace(raw, nameof(raw));
        var trimmed = raw.Trim().ToLowerInvariant();
        if (!SlugRegex.IsMatch(trimmed))
            throw new DomainValidationException($"Invalid slug format: '{raw}'.");
        return new {Aggregate}Slug(trimmed);
    }

    // Step 3: Value Equality & String Conversions
    public override string ToString() => Value;
    public static implicit operator string({Aggregate}Slug slug) => slug.Value;
}
```

### 3.5 Cadence 5: Infrastructure Adapter 4-Step Safe Bounded I/O
```csharp
public sealed class {ExternalService}Adapter(
    HttpClient httpClient,
    IHostValidator hostValidator,
    ILogger<{ExternalService}Adapter> logger) : I{ExternalService}Port
{
    private readonly SemaphoreSlim _throttler = new(10, 10); // Bounded concurrency

    public async Task<Stream> ExecuteOperationAsync(string payload, CancellationToken ct = default)
    {
        // Step 1: Perimeter Guard (Validate endpoint URI safety & tenant host)
        hostValidator.ValidateHost(httpClient.BaseAddress);

        // Step 2: Bounded Concurrency Acquisition
        await _throttler.WaitAsync(ct);
        try
        {
            // Step 3: Safe Wire I/O & Exception Mapping
            using var request = new HttpRequestMessage(HttpMethod.Post, "/operation");
            var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!httpResponse.IsSuccessStatusCode)
                throw new ExternalServiceException($"Service returned HTTP {httpResponse.StatusCode}");

            // Step 4: Bounded Stream Pass-Through & Deterministic Cleanup
            return await httpResponse.Content.ReadAsStreamAsync(ct); // Direct bounded pass-through
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "External service communication failed.");
            throw new ExternalServiceUnavailableException("External service is unavailable.", ex);
        }
        finally
        {
            _throttler.Release();
        }
    }
}
```

### 3.6 Cadence 6: Unit Test 3-Step DAMP AAA Cadence
```csharp
public sealed class {Verb}{Aggregate}UseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReturnsResultDtoAndCommits()
    {
        // Step 1: Arrange (Stubs + SUT Factory + Explicit Command)
        var repository = Substitute.For<I{Aggregate}Repository>();
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.IsResolved.Returns(true);
        projectContext.ProjectId.Returns(Guid.NewGuid());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var fakeClock = TestConstants.CreateFakeClock();

        var sut = new {Verb}{Aggregate}UseCase(repository, projectContext, unitOfWork, fakeClock);
        var command = new {Verb}{Aggregate}Command("Standard Name", "standard-slug", "Default");

        // Step 2: Act (Target method execution)
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Step 3: Assert (State assertions + Mock side-effect verification)
        result.Should().NotBeNull();
        result.Slug.Should().Be("standard-slug");
        await repository.Received(1).AddAsync(Arg.Any<{Aggregate}>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-CONV-001]: Legacy Block Namespace & Constructor Assignment Ceremony
```diff
- namespace SmkDoc.Application.Modules.Authoring {
-     public class {Verb}Service {
-         private readonly I{Aggregate}Repository _repo;
-         public {Verb}Service(I{Aggregate}Repository repo) {
-             _repo = repo;
-         }
-     }
- }
+ namespace SmkDoc.Application.Modules.Authoring;
+ 
+ public sealed class {Verb}{Aggregate}UseCase(I{Aggregate}Repository {aggregate}Repository) {
+     // Dependencies consumed directly via parameter
+ }
```
*Rationale: File-scoped namespaces and C# 13 primary constructors eliminate ceremony, redundant private fields, and unnecessary indentation levels.*

### [TRAP-CONV-002]: Synchronous Blocking `.Result` and Inconsistent Cancellation Token
```diff
- public {Aggregate}ResultDto Get(Guid id, CancellationToken token) {
-     var entity = _repository.GetByIdAsync(id).Result;
-     return Map(entity);
- }
+ public async Task<{Aggregate}ResultDto> GetAsync(Guid id, CancellationToken ct = default) {
+     var entity = await _repository.GetByIdAsync(id, ct);
+     return Map(entity);
+ }
```
*Rationale: Synchronous `.Result` blocks thread-pool worker threads causing thread starvation; standardized `ct` naming enforces uniform async contract passing.*

### [TRAP-CONV-003]: Legacy Collection Initializers vs Modern Collection Expressions
```diff
- List<string> tags = new List<string>();
- int[] codes = new int[] { 200, 404 };
+ List<string> tags = [];
+ int[] codes = [200, 404];
```
*Rationale: Collection expressions `[]` provide a unified, highly optimized C# 12+ allocation syntax recognized by Roslyn.*

### [TRAP-CONV-004]: Inlined Argument Instantiation Obscuring Debugging and Execution Boundaries
```diff
- var result = await {verb}UseCase.ExecuteAsync(new {Verb}Query(id), ct);
+ var query = new {Verb}Query(id);
+ var result = await {verb}UseCase.ExecuteAsync(query, ct);
```
*Rationale: Inlining object instantiation inside method arguments obscures layer boundaries, hinders breakpoint inspection, and degrades stack trace debuggability.*

### [TRAP-CONV-005]: Deeply Nested Conditionals vs Linear Guard Cadence
```diff
- if (user != null) {
-     if (user.IsActive) {
-         if (user.HasPermission("edit")) {
-             return ExecuteInternal();
-         }
-     }
- }
- throw new ForbiddenException();
+ if (user == null || !user.IsActive) throw new UnauthorizedException();
+ if (!user.HasPermission("edit")) throw new ForbiddenException();
+ return ExecuteInternal();
```
*Rationale: Deeply nested indentation structures increase cyclomatic complexity, waste context tokens, and hinder linear readability. Early guard clauses guarantee flat, fail-fast execution.*
