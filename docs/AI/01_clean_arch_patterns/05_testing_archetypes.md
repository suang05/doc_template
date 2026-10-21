# [PATTERN-005]: 05_testing_archetypes.md — Testing Design Patterns & SSoT Test Archetypes

<context>
Scope: Testing Patterns, SSoT UseCase Tests, Aggregate Invariant Tests, FakeTimeProvider & Fluent Builders
Target: `SmkDoc.Tests/` (`Application/`, `Domain/`, `Common/Fixtures/`, `Common/Builders/`)
Authority: Absolute SSoT Invariant (Zero disk/network I/O in unit test project, 100% deterministic, 1 behavior per test)
</context>

---

## 1. Unit Testing Invariants & Standards

| Testing Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Test Fixture Instantiation** | Ad-hoc service construction in every `[Fact]` | SSoT `CreateSut()` factory method in each test class |
| **Domain Entity Testing** | Mocking Domain Entities with Moq: `new Mock<Template>()` | Use real Domain POCOs and Fluent `*Builder` |
| **System Clock** | Unpredictable `DateTime.UtcNow` in assertions | Deterministic `FakeTimeProvider` or fixed `DateTimeOffset` |
| **Disk & Network I/O** | Writing files to disk or hitting real DB in `SmkDoc.Tests` | 100% in-memory; move integration tests to `SmkDoc.IntegrationTests` |
| **Test Data Creation** | Scattered static `*TestFactory` with rigid parameters | Fluent `*Builder` with sensible defaults and chaining methods |
| **Assertion Clarity** | Primitive `Assert.True(x == y)` | Expressive FluentAssertions: `result.Should().Be(...)` |

---

## 2. SSoT UseCase Unit Test Archetype

```csharp
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IExecutionContext> _executionContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateTemplateCommand>> _validator = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    // SSoT System Under Test Factory
    private CreateTemplateUseCase CreateSut() =>
        new(
            _templateRepo.Object,
            _executionContext.Object,
            _unitOfWork.Object,
            _validator.Object,
            _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        // Arrange
        var command = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);
        var validationFailures = new List<ValidationFailure> { new("Name", "Name is required.") };
        _validator.Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _templateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");
        _validator.Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _templateRepo.Setup(r => r.SlugExistsAsync("tax-invoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _templateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ShouldPersistAndReturnDto()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");
        _validator.Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _templateRepo.Setup(r => r.SlugExistsAsync("tax-invoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Tax Invoice");
        result.Slug.Should().Be("tax-invoice");
        _templateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## 3. Aggregate Invariant Test Archetype

Test state mutations, invariant rules, and business verbs directly on Domain Entities without mocking:

```csharp
using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class TemplateTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeActiveAggregate()
    {
        // Arrange & Act
        var template = Template.Create(
            ProjectId.New(),
            TemplateName.Create("Invoice"),
            new TemplateSlug("invoice"),
            "Finance",
            FixedNow);

        // Assert
        template.Id.Should().NotBe(default(TemplateId));
        template.IsActive.Should().BeTrue();
        template.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldSetIsActiveToFalseAndRecordTimestamp()
    {
        // Arrange
        var template = Template.Create(
            ProjectId.New(),
            TemplateName.Create("Invoice"),
            new TemplateSlug("invoice"),
            "Finance",
            FixedNow);
        var deactivationTime = FixedNow.AddHours(1);

        // Act
        template.Deactivate(deactivationTime);

        // Assert
        template.IsActive.Should().BeFalse();
        template.UpdatedAt.Should().Be(deactivationTime);
    }
}
```

---

## 4. Fluent Test Builder Pattern Archetype

Avoid hardcoded static test factories; use fluent builders with sensible defaults:

```csharp
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public sealed class TemplateBuilder
{
    private ProjectId _projectId = ProjectId.New();
    private TemplateName _name = TemplateName.Create("Default Template");
    private TemplateSlug _slug = new("default-template");
    private string? _category = "General";
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    public TemplateBuilder WithProjectId(ProjectId projectId) { _projectId = projectId; return this; }
    public TemplateBuilder WithName(string name) { _name = TemplateName.Create(name); return this; }
    public TemplateBuilder WithSlug(string slug) { _slug = new TemplateSlug(slug); return this; }
    public TemplateBuilder WithCategory(string? category) { _category = category; return this; }
    public TemplateBuilder WithCreatedAt(DateTimeOffset createdAt) { _createdAt = createdAt; return this; }

    public Template Build() =>
        Template.Create(_projectId, _name, _slug, _category, _createdAt);
}
```
