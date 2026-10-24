# [PATTERN-005]: 05_testing_archetypes.md — Testing Design Patterns & SSoT Test Archetypes

<context>
Scope: Testing Patterns, SSoT UseCase Tests, Aggregate Invariant Tests, FakeTimeProvider, Hybrid Builders & Anti-Pattern Firewall
Target: `SmkDoc.Tests/` (`Domain/`, `Application/`, `Infrastructure/`, `Api/`, `Common/`)
Authority: Absolute SSoT Invariant (Zero disk/network I/O in unit test project, 100% deterministic, 1 behavior per test, Zero God Fixtures)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_TEST_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## Architectural Wiring & Testing Boundaries

| SUT Layer | Inbound Test Arranger | Mocking Policy | Assertion Verification |
|---|---|---|---|
| **Domain Entities** | Real instantiation via `Template.Create(...)` | ZERO MOCK (100% Real POCO) | Invariants, State mutations, Equality |
| **Application UseCases** | Positional Command + SSoT `CreateSut()` | NSubstitute for Repos/UoW/Engines | FluentAssertions on DTO, `.Received(1)` on ports |
| **Infrastructure Adapters**| In-Memory SQLite / Mock HttpMessageHandler | Mock external network & storage | Streaming fidelity, zero-buffer piping |
| **Api Middleware/Filters** | ActionExecutingContext / HttpContext mock | Direct unit test or WebApplicationFactory | RFC 9457 ProblemDetails, HTTP status codes |
| **Architectural Boundaries**| NetArchTest on compiled assemblies | Pure Reflection | Inward layer dependencies, sealed classes |

### Engineering Rationale & Architectural Decisions
* **Why Class-Scoped SSoT `CreateSut()` over God Fixtures:** Prevents mock bleed across unrelated tests, ensuring each test class explicitly documents its dependencies.
* **Why 100% Real POCOs in Domain Tests:** Mocking domain entities tests the mock framework rather than actual business invariants, hiding domain calculation bugs.
* **Why Fluent Hybrid Builders (`*Builder` with implicit cast):** Provides clean Object Mother defaults while allowing single-property overrides without verbose constructor calls.
* **Why `FakeTimeProvider` over Ambient Clock:** Guarantees absolute repeatability of time-dependent tests across local developer machines and CI pipelines.

---

## 1. Level 1: Unit Testing Invariants & Standards Matrix

| Testing Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Module Test Fixtures** | God Fixture (`*TestFixture`) bundling multiple UseCase mocks | Class-scoped SSoT `CreateSut()` with explicit local mocks |
| **Mocking Framework** | Verbose Moq `.Setup(...)` / `.ReturnsAsync(...)` ceremony | High-readability NSubstitute (`Substitute.For<T>()`, `.Returns()`, `.Received(1)`) |
| **Domain Entity Testing** | Mocking Domain Entities: `Substitute.For<Template>()` | Real Domain POCOs via Domain Factory or Hybrid `*Builder` |
| **Precondition Leaks** | Hidden mock setups inside fixture constructors | Explicit Arrange phase inside each test method (Strict AAA) |
| **System Clock** | Unpredictable `DateTime.UtcNow` in assertions | Deterministic `FakeTimeProvider` (`TestConstants.CreateFakeClock()`) |
| **Disk & Network I/O** | Writing files to disk or hitting real DB in `SmkDoc.Tests` | 100% in-memory; move integration tests to `SmkDoc.IntegrationTests` |
| **Assertion Clarity** | Primitive `Assert.True(x == y)` | Expressive FluentAssertions: `result.Should().Be(...)` |

---

## 2. Level 2: Core Testing SSoT ASTs & Layer Rules

### Layer Testing Rules Matrix
| Layer | Target Under Test | Mocking Rule | Mandatory Assertions |
|---|---|---|---|
| **Domain** | Entities, ValueObjects, Domain Services | ZERO MOCK (100% Real POCO) | State mutation, Invariant exceptions, Equality |
| **Application** | CQRS Commands & Queries (`IUseCase`) | NSubstitute for Repos/UoW/Services | Fail-Fast guard, Persistence calls, Result DTO |
| **Infrastructure** | Render Engines, Gotenberg, Storage | NSubstitute / FakeHttpMessageHandler | Zero LOH streams, HTTP contract, Error mapping |
| **Presentation** | Filters, Middlewares, Exception Handlers | Mock HttpContext / ActionContext | RFC 9457 ProblemDetails, Status code, Headers |
| **Architecture** | Assemblies & Dependency Graph | Pure Reflection / NetArchTest | Boundary isolation, Layer direction, Sealed POCOs |

### Test Project Folder Parity
```
SmkDoc.Tests/
├── Domain/Entities/              # Aggregate & Entity Invariant Tests (Zero Mock 100%)
├── Application/Modules/{Context}/ # UseCase SSoT Tests & Validator Tests
├── Infrastructure/               # Engine, Adapter & Stream Tests (Zero LOH)
├── Api/                          # Middleware, Filter & RFC 9457 ProblemDetails Tests
├── Architecture/                 # Clean Architecture Boundary Invariant Tests
└── Common/Builders/              # Hybrid Builders (Mother + Builder + Implicit Cast)
```

---

## 3. Level 3: Canonical Testing Archetypes

### SSoT UseCase Test Archetype (NSubstitute Canonical)
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-001]:
* [MUST] Use class-scoped private mocks (`Substitute.For<T>()`) and a single `private TUseCase CreateSut()` factory method as SSoT.
* [MUST] Prohibit shared God Fixtures (`*TestFixture`) bundling mocks across multiple UseCases.
* [MUST] Test exactly 1 behavior per test method with strict 3-phase AAA naming (`Method_WhenCondition_ShouldExpectedResult`).
* [MUST] Assert domain entity interactions using `.Received(1)` and `.DidNotReceive()`.
* [MUST] Use `FakeTimeProvider` (`TestConstants.CreateFakeClock()`) for deterministic time assertions.
```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<CreateTemplateCommand> _validator = Substitute.For<IValidator<CreateTemplateCommand>>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private CreateTemplateUseCase CreateSut() => new(
        _templateRepo,
        _executionContext,
        _unitOfWork,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        var command = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required.")]));

        var act = () => CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Tax Invoice", "tax-invoice", "Finance");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>()).Returns(new ValidationResult());
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<ConflictException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_ShouldPersistAndReturnDto()
    {
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>()).Returns(new ValidationResult());
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateSut().ExecuteAsync(command);

        result.Should().NotBeNull();
        result.Name.Should().Be("Tax Invoice");
        result.Slug.Should().Be("tax-invoice");
        await _templateRepo.Received(1).AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

### Hybrid Builder Archetype (Mother + Builder + Implicit Cast)
>> MANDATORY CANONICAL TEMPLATE [RULE-TEST-002]:
* [MUST] Define builders under `SmkDoc.Tests.Common.Builders`.
* [MUST] Implement fluent mutator methods (`With*`) returning `this`.
* [MUST] Implement `public static implicit operator TEntity(TEntityBuilder builder) => builder.Build();` for zero-boilerplate consumption in tests.
* [MUST] Expose static Object Mother helpers (`aTemplate.AsActive()`, `aTemplate.AsInactive()`).
```csharp
namespace SmkDoc.Tests.Common.Builders;

public sealed class TemplateBuilder
{
    private Guid _projectId = Guid.NewGuid();
    private string _name = "Default Template";
    private string _slug = "default-template";
    private string? _category = "General";
    private bool _isActive = true;
    private DateTimeOffset _createdAt = TestConstants.BaselineTime;

    public static TemplateBuilder Default => new();
    public TemplateBuilder WithProjectId(Guid projectId) { _projectId = projectId; return this; }
    public TemplateBuilder WithName(string name) { _name = name; return this; }
    public TemplateBuilder WithSlug(string slug) { _slug = slug; return this; }
    public TemplateBuilder WithCategory(string? category) { _category = category; return this; }
    public TemplateBuilder AsInactive() { _isActive = false; return this; }
    public TemplateBuilder AsActive() { _isActive = true; return this; }

    public Template Build()
    {
        var template = Template.Create(_projectId, TemplateName.Create(_name), TemplateSlug.Create(_slug), _category, _createdAt);
        if (!_isActive) template.Deactivate(_createdAt);
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

---

## 4. Level 4: High-Salience Testing Traps & Interceptors

### [TRAP-TEST-001]: God Test Fixtures & Hidden Mock Pollution
```csharp
// ! BANNED: God Test Fixtures bundling unrelated mocks and pre-configuring returns in constructors
public class ListProjectUsersUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new(); // Hidden shared preconditions!
    private ListProjectUsersUseCase CreateSut() => _fixture.BuildListProjectUsersUseCase();
}

// >> CANONICAL: Class-Scoped SSoT with Transparent Mocks & Explicit Arrange Block
public sealed class ListProjectUsersUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IValidator<ListProjectUsersQuery> _validator = Substitute.For<IValidator<ListProjectUsersQuery>>();

    private ListProjectUsersUseCase CreateSut() => new(_roleRepo, _userRepo, _validator);

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldReturnUsers()
    {
        // Explicit preconditions configured in Arrange block per test
        _roleRepo.ListByProjectIdAsync(Arg.Any<ProjectId>(), Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();
        ...
    }
}
```

### [TRAP-TEST-002]: Over-Mocking Domain Entities & Value Objects
```csharp
// ! BANNED: Mocking Domain Entity or Value Object hides real domain bugs and state transitions
var mockTemplate = Substitute.For<Template>(); // FATAL: State holder mocked!

// >> CANONICAL: Real Domain POCO via Hybrid Builder or SSoT Factory
Template template = aTemplate.AsActive().WithName("Tax Invoice");
```

### [TRAP-TEST-003]: Validation Logic Duplication in UseCase Slices
```csharp
// ! BANNED: Re-testing validator regex/length edge cases inside UseCase test suite
[Fact] public async Task Execute_WhenSlugHasUppercase_Throws() { ... } // REJECTED: Duplication!

// >> CANONICAL: Exactly 1 Orchestration Fail-Fast Test in UseCase; Field details tested via TestValidate()
[Fact]
public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
{
    _validator.ValidateAsync(invalidCommand, Arg.Any<CancellationToken>())
        .Returns(new ValidationResult([new ValidationFailure("Slug", "Invalid slug")]));

    var act = () => CreateSut().ExecuteAsync(invalidCommand);

    await act.Should().ThrowAsync<ValidationException>();
    await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
}
```
