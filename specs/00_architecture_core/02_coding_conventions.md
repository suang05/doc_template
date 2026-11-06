# [CORE-002]: 02_coding_conventions.md — Language Idioms, Syntax Grammar & Clean Code Standards

<context>
Scope: C# 13 & TypeScript Coding Idioms, 6 Universal Linear Cadences, Primary Constructors, Collection Expressions, File-Scoped Namespaces, 1-Type-Per-File Rule, and Async/Await Invariants.
SSoT_For: Language Grammar, Syntax Layout, Method Signatures, Constructor Dependency Injection, and Linear Execution Cadences.
Out_Of_Scope: Variable naming and Lexicon verbs (see: 04_naming_standards.md), System Architecture tiers (see: 01_system_architecture.md), Security & Performance bounds (see: 03_security_and_performance.md), Comprehensive Testing Archetypes & Testcontainers (see: ../01_clean_arch_patterns/05_testing_archetypes.md).
Target: `backend-v2/` and `frontend-v2/`.
ArchitecturalSignals: [ARCH_EVOLUTION_APPLICATION]
</context>

---

## 1. Level 1: Universal Conventions Invariants Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) | Boundary Vector |
|---|---|---|---|
| **Namespace Layout** | ! Curly brace block namespaces: `namespace Foo { ... }` | >> File-scoped namespaces: `namespace SmkDoc.Domain.Entities;` | Top of file |
| **Constructor DI** | ! Block constructors with manual field assignments (`_repo = repo;`) | >> C# 13 Primary Constructors: `public sealed class Foo(IBar bar)` | Class declaration |
| **Collection Allocation**| ! Legacy initializers: `new List<T>()`, `Array.Empty<T>()`, `new()` | >> C# 12+ Collection Expressions: `[]` | Everywhere |
| **One Type Per File** | ! Bundling `*Command` and `*Validator` inside `*UseCase.cs` | >> Strict 1-Primary-Type-Per-File (`{TypeName}.cs`) per CQRS slice | Filesystem |
| **Architecture Partitioning**| ! Layered folder sprawl (global `Services/`, `Validators/`) | >> Vertical Slice Architecture: feature folders under Bounded Contexts (`Modules/{Context}/{Aggregate}/{Commands\|Queries}`) | Architecture |
| **Execution Flow** | ! Deeply nested if-else ladders ($\ge 3$ levels) | >> Linear Cadence: flat guard clauses with early returns (nesting $\le 2$ levels) | Method Body |
| **Controller Cadence** | ! Complex multi-step actions or business checks (> 5 LOC) | >> Pure 3-Step Dispatcher (<= 5 LOC): Explicit Input -> UseCase -> Envelope | Presentation |
| **UseCase Cadence** | ! Ad-hoc sequence or missing invariant checks | >> Pure 4-Step PEMR Cadence: 1. Parse/Guard -> 2. Ensure -> 3. Mutate -> 4. Return | Application |
| **Abstraction Timing** | ! Speculative generic base classes ("เผื่ออนาคต") | >> Rule of Three: concrete classes until $\ge 2$ real implementations exist | Design |
| **Async Operations** | ! Blocking calls: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`| >> 100% `async`/`await` pipeline with forwarded `ct` | Method body |
| **Cancellation Token** | ! Inconsistent names: `cancellationToken`, `cancelToken`, `token` | >> Parameter name strictly `ct`: `CancellationToken ct = default` | Method signature |
| **Local Variables** | ! Inline/chained parameter instantiation: `method(new Foo(...))` | >> Explicit intermediate local variable assignment before passing into method | Everywhere (Global) |
| **Clock Queries** | ! Direct system clock: `DateTime.UtcNow`, `DateTime.Now` | >> Parameterized `TimeProvider` or explicit `DateTimeOffset now` | Temporal logic |
| **PUT/DELETE Concurrency** | ! Missing concurrency token or using Redis lock on PUT/DELETE | >> Enforce optimistic concurrency via `[FromHeader(Name = "If-Match")] string? ifMatch` returning `412 Precondition Failed` | Controller Action |
| **Declarative Rate Limiting** | ! Unthrottled public or high-cost controllers/actions | >> Protect with `[EnableRateLimiting("...")]` matching designated policy (`api`, `rendering`, `auth-strict`, `test-conn`) | Controller / Action |
| **Test Structure** | ! God fixtures, opaque shared state, over-abstracted DRY helper chains | >> DAMP (Descriptive And Meaningful Phrases) Arrange-Act-Assert | Unit / Integration |

---

## 2. Level 2: Core Conventions AST & Governance Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why C# 13 Primary Constructors with Pure camelCase**: Eliminates boilerplate field assignment ceremony (`this._repo = repo`), reduces file size by 15-20%, and aligns parameter names with DI registration.
* **Why Collection Expressions (`[]`)**: Standardizes collection instantiation across spans, arrays, and lists in a single, high-performance C# 12+ idiom optimized by Roslyn.
* **Why File-Scoped Namespaces**: Removes one indentation level (4 whitespace spaces) across entire files, maximizing horizontal code readability and reducing token waste.
* **Why DAMP Over DRY in Tests**: Over-abstracted helper chains hide test intent. Explicit 3-phase Arrange-Act-Assert (AAA) makes test failures self-evident and self-documenting.

### 2.2 Cognitive Conventions Governance & Syntax Interceptors
```yaml
OptimizationObjectives:
  PrimaryObjective: Syntax Determinism, Cognitive Token Density & Clean Roslyn Optimization
  TradeOffPriority: SyntaxDeterminism > CompileTimeSafety > InformationDensity > StackTraceDebuggability

ProactiveConventionsGuards:
  - Scope: Global
    Trigger: Legacy namespace block with curly braces
    CanonicalAction: Enforce file-scoped namespace >> Ref: [RULE-CONV-001]

  - Scope: Global
    Trigger: Primary constructor replaced by manual assignment
    CanonicalAction: Enforce C# 13 primary constructor >> Ref: [RULE-CONV-002]

  - Scope: Global
    Trigger: Legacy collection initializers (new List<T>(), Array.Empty<T>())
    CanonicalAction: Enforce C# 12+ collection expressions [] >> Ref: [RULE-CONV-003]

  - Scope: Global
    Trigger: Synchronous blocking call (.Result, .Wait())
    CanonicalAction: Enforce 100% async/await pipeline with ct >> Ref: [RULE-CONV-004]

  - Scope: Global
    Trigger: Inline/chained object instantiation inside method call argument (e.g. ExecuteAsync(new ...), method(new ...))
    CanonicalAction: Enforce explicit local variable assignment before passing into method >> Ref: [RULE-CONV-006]

  - Scope: SmkDoc.Api.Controllers
    Trigger: State-mutating PUT/DELETE action missing [FromHeader(Name = "If-Match")] string? ifMatch
    CanonicalAction: Add [FromHeader(Name = "If-Match")] string? ifMatch to method signature and pass to command >> Ref: [RULE-CONV-006]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Controller or endpoint missing declarative [EnableRateLimiting("...")] attribute
    CanonicalAction: Apply [EnableRateLimiting("...")] per designated policy >> Ref: [RULE-CONV-006]

  - Scope: SmkDoc.Tests
    Trigger: Direct clock query via DateTime.UtcNow in test setup
    CanonicalAction: Inject FakeTimeProvider or explicit DateTimeOffset >> Ref: [RULE-CONV-005]
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Canonical C# 13 UseCase Slice Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-001]:
* [MUST] Use C# 13 Primary Constructor for dependency injection.
* [MUST] Declare single file-scoped namespace at the file header.
* [MUST] Name cancellation token parameter strictly as `ct`.

```csharp
using System.Threading;
using System.Threading.Tasks;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.PublishTemplate;

public sealed class PublishTemplateUseCase(
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<PublishTemplateCommand, PublishTemplateResultDto>
{
    public async Task<PublishTemplateResultDto> ExecuteAsync(PublishTemplateCommand command, CancellationToken ct = default)
    {
        var template = await templateRepository.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        var now = timeProvider.GetUtcNow();
        var publishedVersion = template.PublishVersion(command.ChangeNote, now);

        await unitOfWork.CommitAsync(ct);

        return new PublishTemplateResultDto(
            template.Id.Value,
            publishedVersion.VersionNumber,
            publishedVersion.PublishedAt);
    }
}
```

### 3.2 Canonical DAMP Unit Test Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-005]:
* [MUST] Implement clear 3-phase Arrange-Act-Assert (AAA) layout.
* [MUST NOT] Share mutable mock state between independent test runs.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.PublishTemplate;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Unit.Authoring.Templates;

public sealed class PublishTemplateUseCaseTests
{
    private readonly ITemplateRepository _templateRepository = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private PublishTemplateUseCase CreateSut() =>
        new(_templateRepository, _unitOfWork, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_PublishesVersionAndCommits()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = Template.Create(Guid.NewGuid(), "Invoice", "inv", "html", DateTimeOffset.UtcNow);
        _templateRepository.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(template);
        var command = new PublishTemplateCommand(templateId, "Initial Release");

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.VersionNumber.Should().Be(1);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

### 3.3 Canonical Explicit Local Variable Instantiation Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-006]:
* [MUST] Instantiate Command, Query, Entity, or complex arguments into an explicit local variable before passing to any method or UseCase invocation.
* [MUST NOT] Inline instantiation directly into method arguments (e.g. `useCase.ExecuteAsync(new Query(...), ct)`).
* [MUST] Name the execution output and return payload strictly as `result` (strictly PROHIBIT declaring local variables named `response`).
* [MUST] Standardize controller envelope returns: `return Ok(new ApiResponse<TDto>(result))` for queries/updates, `CreatedAtAction` for resource creation (201 Created), `AcceptedAtAction` for async operations (202 Accepted), and `NoContent()` for deletions; strictly PROHIBIT raw `StatusCode(201, ...)`, `StatusCode(202, ...)`, or manual `Location` header assignments.
* [MUST NOT] Inline nested constructor instantiations inside return statements (e.g. prohibit `return Ok(new ApiResponse<TDto>(new TDto(...)));`; instantiate `var result = new TDto(...);` first).
* [MUST] Fully hydrate and return `*ResultDto` directly from the Application UseCase; strictly PROHIBIT returning raw primitives (`string`, `int`, `bool`) that force Controllers to assemble presentation DTOs.
* [MUST] Suffix all UseCase return DTO records with `ResultDto` (e.g. `TemplateResultDto`, `CreateApiKeyResultDto`, `ScanFieldsResultDto`, `SaveHtmlResultDto`, `RollbackVersionResultDto`, `TestDataConnectionResultDto`, `DownloadUrlResultDto`); strictly PROHIBIT creating or referencing `*Response` records in Application and Presentation contracts.
* [MUST] Enforce pure 3-step Controller dispatching: (1) Translate HTTP request to Command/Query, (2) Execute UseCase (`var result = await useCase.ExecuteAsync(command, ct);`), (3) Return envelope (`return Ok(new ApiResponse<TResultDto>(result));`).
* [MUST] Enforce Optimistic Concurrency on state-mutating PUT and DELETE actions using `[FromHeader(Name = "If-Match")] string? ifMatch`, passing `ifMatch` to the command and returning `412 Precondition Failed` on concurrency mismatch.
* [MUST] Protect API controllers and sensitive endpoints with declarative rate limiting using `[EnableRateLimiting("...")]` matching designated traffic policies (`api`, `rendering`, `auth-strict`, `test-conn`).

```csharp
// 1. Explicit variable declaration
var query = new GetDocumentJobQuery(jobId);

// 2. Clean method invocation passing named reference
var result = await getJobUseCase.ExecuteAsync(query, ct);

// 3. Clean standard envelope return
return Ok(new ApiResponse<DocumentJobDto>(result));
```

### 3.4 Canonical Linear Execution Cadence Archetype (Controller 3-Step & UseCase PEMR)
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-007]:
* [MUST] Follow the 6 Universal Linear Cadences across all components:
  1. Controller: 3-Step Dispatcher ($\le 5$ LOC)
  2. UseCase: 4-Step PEMR (Parse & Guard => Ensure => Mutate => Return)
  3. Domain Entity: 4-Step State Mutation Verb (Invariant Guard => Precondition Guard => State Mutation => Event Harvesting)
  4. Value Object: 3-Step Self-Validating Type (Private Constructor => Static Factory `Create` => Operators/ToString)
  5. Infrastructure Adapter: 4-Step Safe Bounded I/O (Perimeter Guard => Bounded Concurrency/Buffer => Safe I/O & Exception Mapping => Deterministic Cleanup)
  6. Unit Test: 3-Step DAMP AAA (Arrange => Act => Assert)
* [MUST] Mark classes `sealed` by default; prohibit inheritance hierarchies exceeding 1 level.
* [MUST] Restrict method nesting depth to $\le 2$ levels using early exit guard clauses.

```csharp
// --- Part A: Controller 3-Step Dispatcher Cadence (<= 5 LOC) ---
[HttpPost]
[Idempotent]
[ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status201Created)]
public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
{
    // Step 1: Explicit Input Mapping (Request -> Command)
    var command = new CreateTemplateCommand(request.Name, request.Slug, request.Category);

    // Step 2: UseCase Invocation (Variable named 'result', strictly no 'response')
    var result = await createTemplateUseCase.ExecuteAsync(command, ct);

    // Step 3: Envelope Return (Location header + ApiResponse<T>)
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
}

// --- Part B: UseCase 4-Step PEMR Execution Cadence ---
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepository,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Parse & Guard (Fail-fast ambient tenant check)
        if (!projectContext.IsResolved)
            throw new ForbiddenException("Active project context is required.");

        // 2. Ensure (Precondition query & business key conflict check)
        if (await templateRepository.SlugExistsAsync(command.Slug, ct))
            throw ConflictException.DuplicateSlug(command.Slug);

        // 3. Mutate (Domain state transition via expressive verb with injected time)
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(projectContext.ProjectId, TemplateName.Create(command.Name), new TemplateSlug(command.Slug), command.Category, now);
        await templateRepository.AddAsync(template, ct);

        // 4. Return (Atomic commit & immutable ResultDto return)
        await unitOfWork.CommitAsync(ct);
        return new TemplateResultDto(template.Id.Value, template.Name.Value, template.Slug.Value, template.Category);
    }
}

// --- Part C: Domain Entity 4-Step State Mutation Verb Cadence ---
public sealed class Template : AggregateRoot<TemplateId>
{
    public TemplateName Name { get; private set; }
    public TemplateStatus Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Rename(TemplateName newName, DateTimeOffset now)
    {
        // 1. Invariant Guard
        Guard.AgainstNull(newName, nameof(newName));

        // 2. Precondition Guard
        if (Status == TemplateStatus.Archived)
            throw new DomainRuleViolationException("Cannot rename an archived template.");

        // 3. State Mutation
        Name = newName;
        UpdatedAt = now;

        // 4. Event Harvesting
        AddDomainEvent(new TemplateRenamedDomainEvent(Id, Name, now));
    }
}

// --- Part D: Value Object 3-Step Self-Validating Cadence ---
public readonly record struct TemplateSlug
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    // 1. Private Parameterized Constructor
    private TemplateSlug(string value) => Value = value;

    public string Value { get; }

    // 2. Pure Static Factory with Fail-Fast Validation
    public static TemplateSlug Create(string raw)
    {
        Guard.AgainstNullOrWhiteSpace(raw, nameof(raw));
        var trimmed = raw.Trim().ToLowerInvariant();
        if (!SlugRegex.IsMatch(trimmed))
            throw new DomainValidationException($"Invalid slug format: '{raw}'.");
        return new TemplateSlug(trimmed);
    }

    // 3. Value Equality & Explicit String Conversions
    public override string ToString() => Value;
    public static implicit operator string(TemplateSlug slug) => slug.Value;
}

// --- Part E: Infrastructure Adapter 4-Step Safe Bounded I/O Cadence ---
public sealed class GotenbergPdfClient(
    HttpClient httpClient,
    IHostValidator hostValidator,
    ILogger<GotenbergPdfClient> logger) : IPdfClient
{
    private readonly SemaphoreSlim _throttler = new(10, 10);

    public async Task<Stream> RenderHtmlAsync(string html, CancellationToken ct = default)
    {
        // 1. Perimeter Guard
        hostValidator.ValidateHost(httpClient.BaseAddress);

        // 2. Bounded Concurrency Acquisition
        await _throttler.WaitAsync(ct);
        try
        {
            // 3. Safe I/O & Exception Mapping
            using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html");
            var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!httpResponse.IsSuccessStatusCode)
                throw new ExternalServiceException($"Gotenberg returned HTTP {httpResponse.StatusCode}");

            // 4. Bounded Stream Pass-Through & Deterministic Cleanup
            return await httpResponse.Content.ReadAsStreamAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Gotenberg connection failed.");
            throw new ExternalServiceUnavailableException("Gotenberg rendering service is unavailable.", ex);
        }
        finally
        {
            _throttler.Release();
        }
    }
}

// --- Part F: Unit Test 3-Step DAMP AAA Cadence ---
public sealed class CreateTemplateUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReturnsResultDtoAndCommits()
    {
        // 1. Arrange (Stubs + SUT + Explicit Command)
        var repository = Substitute.For<ITemplateRepository>();
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.IsResolved.Returns(true);
        projectContext.ProjectId.Returns(Guid.NewGuid());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var fakeClock = TestConstants.CreateFakeClock();

        var sut = new CreateTemplateUseCase(repository, projectContext, unitOfWork, fakeClock);
        var command = new CreateTemplateCommand("Invoice Template", "invoice-template", TemplateCategory.Standard);

        // 2. Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // 3. Assert (State assertions + Mock verification)
        result.Should().NotBeNull();
        result.Slug.Should().Be("invoice-template");
        await repository.Received(1).AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-CONV-001]: Legacy Block Namespace & Constructor Assignment Ceremony
```diff
- namespace SmkDoc.Application.Modules.Authoring {
-     public class PublishService {
-         private readonly ITemplateRepository _repo;
-         public PublishService(ITemplateRepository repo) {
-             _repo = repo;
-         }
-     }
- }
+ namespace SmkDoc.Application.Modules.Authoring;
+ 
+ public sealed class PublishTemplateUseCase(ITemplateRepository templateRepository) {
+     // Dependencies consumed directly via parameter
+ }
```
*Rationale: File-scoped namespaces and C# 13 primary constructors eliminate ceremony, redundant private fields, and unnecessary indentation levels.*

### [TRAP-CONV-002]: Synchronous Blocking `.Result` and Inconsistent Cancellation Token
```diff
- public TemplateResultDto Get(Guid id, CancellationToken token) {
-     var template = _repository.GetByIdAsync(id).Result;
-     return Map(template);
- }
+ public async Task<TemplateResultDto> GetAsync(Guid id, CancellationToken ct = default) {
+     var template = await _repository.GetByIdAsync(id, ct);
+     return Map(template);
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
- var result = await getJobUseCase.ExecuteAsync(new GetDocumentJobQuery(jobId), ct);
+ var query = new GetDocumentJobQuery(jobId);
+ var result = await getJobUseCase.ExecuteAsync(query, ct);
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
