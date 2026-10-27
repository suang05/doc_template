# [PATTERN-005]: 05_testing_archetypes.md — Testing Design Patterns & SSoT Test Archetypes

<context>
Scope: Testing Patterns, SSoT UseCase Tests, Aggregate Invariant Tests, Isolated FluentValidation Tests, FakeTimeProvider, Hybrid Builders, Integration Tests & Anti-Pattern Firewall
Target: `SmkDoc.Tests/` & `SmkDoc.IntegrationTests/`
Authority: Absolute SSoT Invariant (Zero disk/network I/O in unit test project, 100% deterministic, 1 behavior per test, Zero God Fixtures)
Invariants: Class-Scoped CreateSut(), 100% Real POCOs in Domain, Deterministic FakeTimeProvider, Strict AAA Naming, Isolated Validator Testing
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_TEST_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Unit Testing Invariants & Standards Matrix

### Architectural Wiring & Testing Boundaries
| SUT Layer | Inbound Test Arranger | Mocking Policy | Assertion Verification |
|---|---|---|---|
| **Domain Entities** | Real instantiation via `Template.Create(...)` | ZERO MOCK (100% Real POCO) | Invariants, State mutations, Equality |
| **Application UseCases** | Positional Command + SSoT `CreateSut()` | NSubstitute for Repos/UoW/Engines | FluentAssertions on DTO, `.Received(1)` on ports |
| **FluentValidation Validators**| Direct instantiation of `*Validator` | ZERO MOCK (100% Real Validator) | `TestValidate()`, `ShouldHaveValidationErrorFor` |
| **Infrastructure Adapters**| In-Memory SQLite / Mock HttpMessageHandler | Mock external network & storage | Streaming fidelity, zero-buffer piping |
| **Api Middleware/Filters** | ActionExecutingContext / HttpContext mock | Direct unit test or WebApplicationFactory | RFC 9457 ProblemDetails, HTTP status codes |
| **Integration Web API** | `SmkDocApiFactory` (`WebApplicationFactory`) | Real Testcontainers Postgres & MinIO | HTTP status, JSON envelopes, Database commits |
| **Architectural Boundaries**| NetArchTest on compiled assemblies | Pure Reflection | Inward layer dependencies, sealed classes |

### Testing Invariants & Standards Matrix
| Testing Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Module Test Fixtures** | God Fixture (`*TestFixture`) bundling multiple UseCase mocks | Class-scoped SSoT `CreateSut()` with explicit local mocks |
| **Mocking Framework** | Verbose Moq `.Setup(...)` / `.ReturnsAsync(...)` ceremony | High-readability NSubstitute (`Substitute.For<T>()`, `.Returns()`, `.Received(1)`) |
| **Domain Entity Testing** | Mocking Domain Entities: `Substitute.For<Template>()` | Real Domain POCOs via Domain Factory or Hybrid `*Builder` |
| **Validation Testing** | Duplicating property regex/length checks inside UseCase tests | Isolated validator tests via `TestValidate()`; exactly 1 fail-fast test in UseCase |
| **Precondition Leaks** | Hidden mock setups inside fixture constructors | Explicit Arrange phase inside each test method (Strict AAA) |
| **System Clock** | Unpredictable `DateTime.UtcNow` in assertions | Deterministic `FakeTimeProvider` (`TestConstants.CreateFakeClock()`) |
| **Disk & Network I/O** | Writing files to disk or hitting real DB in `SmkDoc.Tests` | 100% in-memory; move integration tests to `SmkDoc.IntegrationTests` |
| **Assertion Clarity** | Primitive `Assert.True(x == y)` | Expressive FluentAssertions: `result.Should().Be(...)` |

---

## 2. Level 2: Core Testing SSoT ASTs & Cognitive Test Engine

### Engineering Rationale & Architectural Decisions
* **Why Class-Scoped SSoT `CreateSut()` over God Fixtures:** Prevents mock bleed across unrelated tests, ensuring each test class explicitly documents its dependencies.
* **Why 100% Real POCOs in Domain Tests:** Mocking domain entities tests the mock framework rather than actual business invariants, hiding domain calculation bugs.
* **Why Isolated Validator Tests (`TestValidate()`):** `FluentValidation.TestHelper` tests boundary rules, regex, and message keys in isolation with microsecond execution time. UseCase tests only verify the fail-fast orchestration link.
* **Why Fluent Hybrid Builders (`*Builder` with implicit cast):** Provides clean Object Mother defaults while allowing single-property overrides without verbose constructor calls.
* **Why `FakeTimeProvider` over Ambient Clock:** Guarantees absolute repeatability of time-dependent tests across local developer machines and CI pipelines.
* **Why Testcontainers in Integration Tests:** Executes real PostgreSQL queries and EF Core migrations in isolated throwaway Docker containers, catching true SQL syntax and constraint bugs.

### Cognitive Test Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Absolute Determinism, Complete Isolation & Sub-Second Test Execution
  TradeOffPriority: Determinism > TestIsolation > TestReadability > SharedFixtureConvenience

ProactiveTestGuards:
  - Scope: SmkDoc.Tests.Application
    Trigger: Shared fixture class capturing mocks across multiple test classes
    CanonicalAction: Enforce class-scoped private mocks and private CreateSut() factory method >> Ref: [RULE-TEST-001]

  - Scope: SmkDoc.Tests.Domain
    Trigger: Call to Substitute.For<TAggregate>() on Domain entity
    CanonicalAction: Reject immediately; Domain tests must use 100% real POCO instances >> Ref: [RULE-TEST-002]

  - Scope: SmkDoc.Tests.Application
    Trigger: UseCase test asserting multiple field-level regex or length validation permutations
    CanonicalAction: Move property permutations to Isolated Validator test; keep exactly 1 fail-fast orchestration test >> Ref: [RULE-TEST-003]

  - Scope: SmkDoc.Tests
    Trigger: File creation on disk or PostgreSQL connection attempt in unit test project
    CanonicalAction: Move test to SmkDoc.IntegrationTests; SmkDoc.Tests must remain 100% in-memory

  - Scope: SmkDoc.Tests
    Trigger: Direct query to DateTime.UtcNow or DateTime.Now in test arrangement
    CanonicalAction: Substitute with FakeTimeProvider initialized via TestConstants.CreateFakeClock()

CoreExtensionSlots:
  TestBuilderSlot: HybridBuilder<TEntity> (Mother + Builder + Implicit Cast port)
  TimeFixtureSlot: FakeTimeProvider (Deterministic clock control)
  SutFactorySlot: CreateSut() (Class-scoped SUT instantiator)
  ValidatorTestSlot: TestValidate() (Isolated property rule assertion)
  ApiFactorySlot: WebApplicationFactory<Program> (Integration test harness)
```

### Layer Testing Rules Matrix
| Layer | Target Under Test | Mocking Rule | Mandatory Assertions |
|---|---|---|---|
| **Domain** | Entities, ValueObjects, Domain Services | ZERO MOCK (100% Real POCO) | State mutation, Invariant exceptions, Equality |
| **Application** | CQRS Commands & Queries (`IUseCase`) | NSubstitute for Repos/UoW/Services | Fail-Fast guard, Persistence calls, Result DTO |
| **Validation** | FluentValidation `AbstractValidator<T>` | ZERO MOCK (100% Real Validator) | `ShouldHaveValidationErrorFor`, `ShouldNotHaveAnyValidationErrors` |
| **Infrastructure** | Render Engines, Gotenberg, Storage | NSubstitute / FakeHttpMessageHandler | Zero LOH streams, HTTP contract, Error mapping |
| **Presentation** | Filters, Middlewares, Exception Handlers | Mock HttpContext / ActionContext | RFC 9457 ProblemDetails, Status code, Headers |
| **Integration** | HTTP Endpoints & DB Migrations | Real Testcontainers PostgreSQL & MinIO | HTTP Status, JSON wire contract, DB Row assertion |
| **Architecture** | Assemblies & Dependency Graph | Pure Reflection / NetArchTest | Boundary isolation, Layer direction, Sealed POCOs |

### Test Project Folder Parity
```
SmkDoc.Tests/
  Domain/
    Entities/ # Aggregate & Entity Invariant Tests (Zero Mock 100%)
  Application/
    Modules/
      Authoring/ # UseCase SSoT Tests & Validator Tests
      Rendering/
      Integration/
      IdentityAccess/
  Infrastructure/ # Engine, Adapter & Stream Tests (Zero LOH)
  Api/ # Middleware, Filter & RFC 9457 ProblemDetails Tests
  Architecture/ # Clean Architecture Boundary Invariant Tests
  Common/
    Builders/ # Hybrid Builders (Mother + Builder + Implicit Cast)

SmkDoc.IntegrationTests/
  Fixtures/ # SmkDocApiFactory, PostgreSqlContainerFixture, MinioContainerFixture
  Persistence/ # Real EF Core migrations & query performance
  Repositories/ # Real database repository query tests
  Storage/ # Real MinIO S3 bucket upload/download tests
```

---

## 3. Level 3: Canonical Testing Archetypes

### SSoT UseCase Test Archetype (NSubstitute Canonical)
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-001]:
* [MUST] Use class-scoped private mocks (`Substitute.For<T>()`) and a single `private TUseCase CreateSut()` factory method as SSoT.
* [MUST] Prohibit shared God Fixtures (`*TestFixture`) bundling mocks across multiple UseCases.
* [MUST] Test exactly 1 behavior per test method with strict 3-phase AAA naming (`Method_WhenCondition_ShouldExpectedResult`).
* [MUST] Include exactly 1 Fail-Fast orchestration test asserting that an invalid command throws `ValidationException` without touching repositories or Unit of Work.
* [MUST] Assert domain entity interactions using `.Received(1)` and `.DidNotReceive()`.
* [MUST] Use `FakeTimeProvider` (`TestConstants.CreateFakeClock()`) for deterministic time assertions.
```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<CreateTemplateCommand> _validator = Substitute.For<IValidator<CreateTemplateCommand>>();
    private readonly ILogger<CreateTemplateUseCase> _logger = Substitute.For<ILogger<CreateTemplateUseCase>>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private CreateTemplateUseCase CreateSut() =>
        new(_templateRepo, _unitOfWork, _validator, _timeProvider, _logger);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        var command = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required.")]));

        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Tax Invoice", "tax-invoice", "Finance");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _templateRepo.SlugExistsAsync(command.Slug, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_ShouldPersistTemplateAndReturnDto()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Invoice Template", "invoice-template", "Billing");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _templateRepo.SlugExistsAsync(command.Slug, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Invoice Template");
        result.Slug.Should().Be("invoice-template");
        result.Category.Should().Be("Billing");

        await _templateRepo.Received(1).AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

### Domain Aggregate Invariant Test Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-002]:
* [MUST] Use 100% real domain entities (prohibit mocking domain entities via NSubstitute).
* [MUST] Assert invariant enforcement by testing state transitions and expected domain exceptions.
```csharp
using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class TemplateAggregateTests
{
    private readonly DateTimeOffset _fixedNow = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenArgumentsAreValid_ShouldInstantiateActiveTemplateWithEmptyCollections()
    {
        // Arrange & Act
        var projectId = new ProjectId(Guid.NewGuid());
        var name = TemplateName.Create("Tax Report");
        var slug = new TemplateSlug("tax-report");

        var template = Template.Create(projectId, name, slug, "Finance", _fixedNow);

        // Assert
        template.Should().NotBeNull();
        template.ProjectId.Should().Be(projectId);
        template.Name.Value.Should().Be("Tax Report");
        template.IsActive.Should().BeTrue();
        template.FieldMappings.Should().BeEmpty();
        template.Versions.Should().BeEmpty();
        template.CreatedAt.Should().Be(_fixedNow);
    }

    [Fact]
    public void Deactivate_WhenTemplateIsActive_ShouldSetIsActiveToFalseAndTrackUpdate()
    {
        // Arrange
        var template = Template.Create(new ProjectId(Guid.NewGuid()), TemplateName.Create("Test"), new TemplateSlug("test"), null, _fixedNow);
        var laterNow = _fixedNow.AddHours(2);

        // Act
        template.Deactivate(laterNow);

        // Assert
        template.IsActive.Should().BeFalse();
        template.UpdatedAt.Should().Be(laterNow);
    }
}
```

### Isolated FluentValidation Archetype (`TestValidate()`)
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-003]:
* [MUST] Test FluentValidation validators in isolation from UseCase orchestrators using `FluentValidation.TestHelper`.
* [MUST] Use `_sut.TestValidate(command)` and assert via `result.ShouldHaveValidationErrorFor(...)` or `result.ShouldNotHaveAnyValidationErrors()`.
* [MUST NOT] Duplicate validator property assertions inside UseCase test suites.
```csharp
using FluentValidation.TestHelper;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateCommandValidatorTests
{
    private readonly CreateTemplateCommandValidator _sut = new();

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldHaveValidationError()
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "", "invoice-slug", null);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_WhenSlugContainsUppercase_ShouldHaveValidationError()
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Invoice", "INVALID_SLUG", null);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Slug);
    }

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Invoice", "valid-slug", "Finance");
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

### Hybrid Object Mother / Fluent Builder Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-004]:
* [MUST] Implement builder pattern under `SmkDoc.Tests.Common.Builders` with pre-populated, valid defaults (Object Mother).
* [MUST] Provide fluent chaining methods (`With*`) returning `this` for single-property customization.
* [MUST] Implement implicit cast operator `public static implicit operator TEntity(TBuilder builder) => builder.Build();`.
* [MUST] Expose static Object Mother helper wrappers (`aTemplate.AsActive()`, `aTemplate.AsInactive()`).
```csharp
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public sealed class TemplateBuilder
{
    private ProjectId _projectId = new(Guid.NewGuid());
    private TemplateName _name = TemplateName.Create("Default Template");
    private TemplateSlug _slug = new("default-template");
    private string? _category = "General";
    private bool _isActive = true;
    private DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    public static TemplateBuilder Default => new();

    public TemplateBuilder WithProjectId(ProjectId projectId)
    {
        _projectId = projectId;
        return this;
    }

    public TemplateBuilder WithName(string name)
    {
        _name = TemplateName.Create(name);
        return this;
    }

    public TemplateBuilder WithSlug(string slug)
    {
        _slug = new TemplateSlug(slug);
        return this;
    }

    public TemplateBuilder WithCategory(string? category)
    {
        _category = category;
        return this;
    }

    public TemplateBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public TemplateBuilder AsActive()
    {
        _isActive = true;
        return this;
    }

    public Template Build()
    {
        var template = Template.Create(_projectId, _name, _slug, _category, _now);
        if (!_isActive) template.Deactivate(_now);
        return template;
    }

    public static implicit operator Template(TemplateBuilder builder) => builder.Build();
}

public static class aTemplate
{
    public static TemplateBuilder AsActive() => TemplateBuilder.Default.AsActive();
    public static TemplateBuilder AsInactive() => TemplateBuilder.Default.AsInactive();
}
```

### Integration Test Archetype (`SmkDocApiFactory` & Testcontainers)
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-005]:
* [MUST] Place integration tests strictly under `SmkDoc.IntegrationTests`.
* [MUST] Use `WebApplicationFactory<Program>` with PostgreSQL and MinIO container fixtures.
* [MUST] Assert HTTP status codes, RFC 9457 ProblemDetails envelopes, and database commits end-to-end.
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.IntegrationTests.Fixtures;
using Xunit;

namespace SmkDoc.IntegrationTests.Controllers;

[Collection("Integration")]
public sealed class TemplateEndpointIntegrationTests(SmkDocApiFactory factory) : IClassFixture<SmkDocApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateTemplate_WhenPayloadIsValid_ShouldReturn201CreatedAndPersistToPostgres()
    {
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Integration Template", "integration-template", "Testing");

        var response = await _client.PostAsJsonAsync("api/v1/templates", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<TemplateResultDto>>();
        envelope.Should().NotBeNull();
        envelope!.Data.Name.Should().Be("Integration Template");
        envelope.Data.Slug.Should().Be("integration-template");
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-TEST-001]: Shared God Fixture Mock Bleed
```diff
- public class UseCaseTests : IClassFixture<SharedGodFixture> { public UseCaseTests(SharedGodFixture f) => _f = f; }
+ public sealed class UseCaseTests { private readonly IRepo _repo = Substitute.For<IRepo>(); private Sut CreateSut() => new(_repo); }
```
*Rationale: Shared fixtures carry forward mock state configurations and stub setups across tests, causing intermittent test order coupling.*

### [TRAP-TEST-002]: Over-Mocking Pure Domain Entities
```diff
- var template = Substitute.For<Template>(); template.Name.Returns(name);
+ var template = Template.Create(projectId, name, slug, cat, now); // 100% Real POCO
```
*Rationale: Mocking domain entities validates the mock engine instead of actual domain invariant enforcement and validation guards.*

### [TRAP-TEST-003]: Validation Logic Duplication in UseCase Slices
```diff
- [Fact] public async Task Execute_WhenSlugHasUppercase_Throws() { ... } // Duplicate field permutation in UseCase test
+ [Fact] public void Validate_WhenSlugHasUppercase_ShouldHaveError() => validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Slug);
```
*Rationale: Duplicating field-level regex/boundary permutations inside UseCase tests slows test execution; test property rules in isolation via `TestValidate()` and keep exactly 1 fail-fast orchestration test in the UseCase suite.*

### [TRAP-TEST-004]: Ambient Clock Flakiness
```diff
- var result = await sut.ExecuteAsync(cmd, ct); result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
+ var result = await sut.ExecuteAsync(cmd, ct); result.CreatedAt.Should().Be(fakeTimeProvider.GetUtcNow());
```
*Rationale: Asserting against non-deterministic system clocks causes flaky CI failures under CPU scheduling delays.*

### [TRAP-TEST-005]: Disk/Network I/O in Unit Test Project
```diff
- using var stream = File.OpenRead("test.pdf"); await dbContext.Database.MigrateAsync();
+ using var stream = new MemoryStream(pdfBytes); // SmkDoc.Tests must remain 100% in-memory; move I/O to SmkDoc.IntegrationTests
```
*Rationale: Performing disk writes or real database operations inside unit test suites degrades test velocity and causes dirty workspace states.*

### [TRAP-TEST-006]: Hidden Preconditions in Fixture Constructor
```diff
- public TestFixture() { _repo.GetByIdAsync(Arg.Any<Guid>()).Returns(new Template(...)); }
+ [Fact] public async Task Test_Behavior() { _repo.GetByIdAsync(id).Returns(template); var sut = CreateSut(); }
```
*Rationale: Hidden arrange setups in constructors make individual test cases opaque and violate strict 3-phase Arrange-Act-Assert clarity.*
