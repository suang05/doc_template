# [PATTERN-005]: 05_testing_archetypes.md — Testing Design Patterns & SSoT Test Archetypes

<context>
Scope: Testing Patterns, SSoT UseCase Tests, Aggregate Invariant Tests, FakeTimeProvider, Hybrid Builders & Anti-Pattern Firewall
Target: `SmkDoc.Tests/` (`Domain/`, `Application/`, `Infrastructure/`, `Api/`, `Common/`)
Authority: Absolute SSoT Invariant (Zero disk/network I/O in unit test project, 100% deterministic, 1 behavior per test, Zero God Fixtures)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
</context>

---

## 1. Unit Testing Invariants & Standards

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

## 2. Fatal Testing Traps & Anti-Pattern Interceptors

### [TRAP-TEST-001]: God Test Fixtures & Shared State Leakage
Bundling multiple UseCases and mock instances into a shared `*TestFixture` class obscures SUT dependencies and causes test coupling.

```csharp
// ! BANNED: God Test Fixture bundling unrelated mocks
public class ListProjectUsersUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();
    private ListProjectUsersUseCase CreateSut() => _fixture.BuildListProjectUsersUseCase();
}

// >> CANONICAL: Class-Scoped SSoT with Transparent Mocks
public sealed class ListProjectUsersUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IValidator<ListProjectUsersQuery> _validator = Substitute.For<IValidator<ListProjectUsersQuery>>();

    private ListProjectUsersUseCase CreateSut() => new(_roleRepo, _userRepo, _validator);
}
```

### [TRAP-TEST-002]: Hidden Constructor Mock Pollution
Configuring mock returns inside test constructors or fixture constructors causes hidden preconditions and attention leakage across test cases.

```csharp
// ! BANNED: Pre-configuring mock returns in constructor
public UserManagementTestFixture()
{
    Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); // FATAL: Hidden precondition
}

// >> CANONICAL: Explicit Preconditions in Test Method Arrange Block
[Fact]
public async Task ExecuteAsync_WhenValid_ShouldCommit()
{
    // Arrange
    _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    ...
}
```

### [TRAP-TEST-003]: Over-Mocking Domain Entities, Value Objects & DTOs
Invoking `Substitute.For<T>` on POCOs, Entities, Value Objects, or DTOs bypasses domain invariants and generates false-positive tests.

```csharp
// ! BANNED: Mocking Domain Entity or Value Object
var mockTemplate = Substitute.For<Template>(); // FATAL: State holder mocked!
var mockName = Substitute.For<TemplateName>();

// >> CANONICAL: Real Domain POCO via Hybrid Builder or Factory
Template template = aTemplate.AsActive().WithName("Tax Invoice");
```

### [TRAP-TEST-004]: Assert Sprawl & Cross-Concern Verification
Asserting all aggregate properties during mutation verification creates brittle tests that break upon unrelated schema changes.

```csharp
// ! BANNED: Asserting unrelated properties during action test
template.Deactivate(now);
template.Id.Should().NotBeEmpty(); // FATAL: Sprawl! Unrelated to Deactivate
template.Name.Value.Should().Be("Invoice");
template.IsActive.Should().BeFalse();

// >> CANONICAL: Focused Invariant Assertion (Mutated State Only)
template.Deactivate(now);
template.IsActive.Should().BeFalse();
template.UpdatedAt.Should().Be(now);
```

### [TRAP-TEST-005]: Validation Logic Duplication in UseCase Slices
Testing individual input validation edge cases (regex, length, nullability) inside UseCase tests explodes test complexity and duplicates validator testing.

```csharp
// ! BANNED: Re-testing validator edge cases inside UseCase test
[Fact] public async Task Execute_WhenSlugHasUppercase_Throws() { ... }
[Fact] public async Task Execute_WhenSlugTooLong_Throws() { ... }

// >> CANONICAL: 1 Orchestration Fail-Fast Test in UseCase; Details in ValidatorTest
// In UseCaseTests.cs:
[Fact]
public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
{
    _validator.ValidateAsync(invalidCommand, Arg.Any<CancellationToken>())
        .Returns(new ValidationResult([new ValidationFailure("Slug", "Invalid slug")]));

    var act = () => CreateSut().ExecuteAsync(invalidCommand);

    await act.Should().ThrowAsync<ValidationException>();
    await _repository.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
}
```

### [TRAP-TEST-006]: System Clock Non-Determinism (`DateTime.UtcNow`)
Querying `DateTime.UtcNow` or using `.BeCloseTo()` in assertions causes flakiness during CI/CD concurrency.

```csharp
// ! BANNED: Unpredictable system time in assertions
result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 1.Seconds()); // FATAL: Non-deterministic!

// >> CANONICAL: Deterministic Time via FakeTimeProvider
private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
...
result.CreatedAt.Should().Be(TestConstants.BaselineTime);
```

---

## 3. Layer-by-Layer Standards & Folder Parity

```text
SmkDoc.Tests/
├── Domain/
│   ├── Entities/                 # Aggregate & Entity Invariant Tests (Zero Mock 100%)
│   └── ValueObjects/             # Value Object Equality & Guard Tests
├── Application/
│   └── Modules/{Context}/
│       └── {Feature}/
│           ├── Commands/{Action}/
│           │   ├── {Action}UseCaseTests.cs
│           │   └── {Action}CommandValidatorTests.cs
│           └── Queries/{Action}/
│               ├── {Action}UseCaseTests.cs
│               └── {Action}QueryValidatorTests.cs
├── Infrastructure/               # Engine, Adapter & Stream Tests (Zero LOH)
├── Api/                          # Middleware, Filter & RFC 9457 ProblemDetails Tests
├── Architecture/                 # NetArchTest Clean Architecture Invariant Tests
└── Common/
    ├── Builders/                 # Hybrid Builders (Mother + Builder + Implicit Cast)
    └── TestConstants.cs          # Fixed Time, Guids, and Baseline Constants
```

### Layer Testing Rules Matrix
| Layer | Target Under Test | Mocking Rule | Mandatory Assertions |
|---|---|---|---|
| **Domain** | Entities, ValueObjects, Domain Services | ZERO MOCK (100% Real POCO) | State mutation, Invariant exceptions, Equality |
| **Application** | CQRS Commands & Queries (`IUseCase`) | NSubstitute for Repos/UoW/Services | Fail-Fast guard, Persistence calls, Result DTO |
| **Infrastructure** | Render Engines, Gotenberg, Storage | NSubstitute / FakeHttpMessageHandler | Zero LOH streams, HTTP contract, Error mapping |
| **Presentation** | Filters, Middlewares, Exception Handlers | Mock HttpContext / ActionContext | RFC 9457 ProblemDetails, Status code, Headers |
| **Architecture** | Assemblies & Dependency Graph | Pure Reflection / NetArchTest | Boundary isolation, Layer direction, Sealed POCOs |

---

## 4. SSoT UseCase Test Archetype (NSubstitute Canonical)

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

    // SSoT System Under Test Factory
    private CreateTemplateUseCase CreateSut() => new(
        _templateRepo,
        _executionContext,
        _unitOfWork,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        var command = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required.")]));

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

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
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_ShouldPersistAndReturnDto()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Tax Invoice");
        result.Slug.Should().Be("tax-invoice");

        await _templateRepo.Received(1).AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
```

---

## 5. Hybrid Builder Pattern (Mother + Builder + Implicit Operator)

Eliminates Arrange boilerplate and `.Build()` syntactic noise:

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

    // 1. Object Mother Entry Point
    public static TemplateBuilder Default => new();

    // 2. Fluent Chaining Methods
    public TemplateBuilder WithProjectId(Guid projectId) { _projectId = projectId; return this; }
    public TemplateBuilder WithName(string name) { _name = name; return this; }
    public TemplateBuilder WithSlug(string slug) { _slug = slug; return this; }
    public TemplateBuilder WithCategory(string? category) { _category = category; return this; }
    public TemplateBuilder AsInactive() { _isActive = false; return this; }
    public TemplateBuilder AsActive() { _isActive = true; return this; }

    public Template Build()
    {
        var template = Template.Create(
            _projectId,
            TemplateName.Create(_name),
            TemplateSlug.Create(_slug),
            _category,
            _createdAt);

        if (!_isActive)
        {
            template.Deactivate(_createdAt);
        }

        return template;
    }

    // 3. Implicit Cast Operator (Eliminates .Build() call)
    public static implicit operator Template(TemplateBuilder builder) => builder.Build();
}

public static class aTemplate
{
    public static TemplateBuilder AsActive() => TemplateBuilder.Default.AsActive();
    public static TemplateBuilder AsInactive() => TemplateBuilder.Default.AsInactive();
}
```

### Usage in Arrange Phase
```csharp
// Zero .Build() noise:
Template activeTemplate = aTemplate.AsActive().WithSlug("invoice");
Template inactiveTemplate = aTemplate.AsInactive();
```

---

## 6. Isolated FluentValidation Archetype

Validate rules in isolation via `TestValidate()` without invoking UseCase handlers:

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

