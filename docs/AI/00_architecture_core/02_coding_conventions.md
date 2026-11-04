# [CORE-002]: 02_coding_conventions.md — Language Idioms, Syntax Grammar & Clean Code Standards

<context>
Scope: C# 13 & TypeScript Coding Idioms, Primary Constructors, Collection Expressions, File-Scoped Namespaces, 1-Type-Per-File Rule, Async/Await, and DAMP Testing.
SSoT_For: Language Grammar, Syntax Layout, Method Signatures, Constructor Dependency Injection, and Test Structure.
Out_Of_Scope: Variable naming and Lexicon verbs (see: 04_naming_standards.md), System Architecture (see: 01_system_architecture.md), Security & Performance (see: 03_security_and_performance.md).
Target: `backend-v2/` and `frontend-v2/`.
TriggerSignals: [SIGNAL_STRUCTURE_MUTATION]
</context>

---

## 1. Level 1: Universal Conventions Invariants Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) | Boundary Vector |
|---|---|---|---|
| **Namespace Layout** | ! Curly brace block namespaces: `namespace Foo { ... }` | >> File-scoped namespaces: `namespace SmkDoc.Domain.Entities;` | Top of file |
| **Constructor DI** | ! Block constructors with manual field assignments (`_repo = repo;`) | >> C# 13 Primary Constructors: `public sealed class Foo(IBar bar)` | Class declaration |
| **Collection Allocation**| ! Legacy initializers: `new List<T>()`, `Array.Empty<T>()`, `new()` | >> C# 12+ Collection Expressions: `[]` | Everywhere |
| **One Type Per File** | ! Bundling `*Command` and `*Validator` inside `*UseCase.cs` | >> Strict 1-Primary-Type-Per-File (`{TypeName}.cs`) per CQRS slice | Filesystem |
| **Async Operations** | ! Blocking calls: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`| >> 100% `async`/`await` pipeline with forwarded `ct` | Method body |
| **Cancellation Token** | ! Inconsistent names: `cancellationToken`, `cancelToken`, `token` | >> Parameter name strictly `ct`: `CancellationToken ct = default` | Method signature |
| **Local Variables** | ! Chained instantiation: `repo.Add(Template.Create(...))` | >> Explicit local variable assignment before passing into method | Method body |
| **Clock Queries** | ! Direct system clock: `DateTime.UtcNow`, `DateTime.Now` | >> Parameterized `TimeProvider` or explicit `DateTimeOffset now` | Temporal logic |
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
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.PublishTemplate;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Unit.Authoring.Templates;

public sealed class PublishTemplateUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly PublishTemplateUseCase _sut;

    public PublishTemplateUseCaseTests()
    {
        _sut = new PublishTemplateUseCase(
            _templateRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_PublishesVersionAndCommits()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = Template.Create(Guid.NewGuid(), "Invoice", "inv", "html", DateTimeOffset.UtcNow);
        _templateRepositoryMock.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        var command = new PublishTemplateCommand(templateId, "Initial Release");

        // Act
        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.VersionNumber.Should().Be(1);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
