# [CONV-001]: coding_conventions.md — Universal Lexicon, Syntax Grammar & Clean Architecture Conventions SSoT

<context>
Scope: Universal C# 13 and TypeScript Coding Conventions, Lexical Verbs & Payload Variables, Anti-Abbreviation Policy, Top-Level Using Directives, Explicit Local Variable Assignments, Formatting Layout, DRY vs DAMP Boundaries & RFC 9457 Error Handling
Target: Full Stack (`backend-v2/` & `frontend-v2/`)
Authority: Absolute SSoT Invariant for Cross-Cutting Syntax, Grammar & Structure
Invariants: Strict Payload Variables (command vs query vs result), Anti-Abbreviation (Zero Cryptic Acronyms), Top-Level using Directives, Explicit Local Variable Assignments, File-Scoped Namespaces, Primary Constructors, DAMP Testing, Zero Empty Catch Blocks, Collection Expressions []
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_STRUCTURE_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Universal Conventions Invariants Matrix

### Architectural Wiring & Conventions Governance Flow
| Direction | Upstream Origin | Current Role (Conventions & Lexicon Guard) | Downstream Destination |
|---|---|---|---|
| **Syntax Inbound** | LLM Code Generation / Dev PR | Enforces SSoT casing, suffix, verb lexicon & full variable names | AST Syntax Tree / Compiler |
| **File Scoping** | C# Source Compilation | Mandates file-scoped namespaces & top-level `using` directives | Roslyn Analyzer / Clean Architecture |
| **Local Variables** | Object Instantiation | Mandates explicit local variable assignment before consumption | Method Invocation / Execution Pipeline |
| **Cross-Stack Wire**| C# DTO PascalCase Egress | Enforces camelCase JSON wire and TypeScript match | Presentation / Next.js Client Proxy |
| **Exception Flow** | Domain / App / Infra Errors | Maps strongly-typed exceptions to RFC 9457 status | `GlobalExceptionHandler` (ProblemDetails) |
| **Testing Boundary**| Test Suite Execution | Enforces DAMP Arrange-Act-Assert readability | `SmkDoc.Tests/` (Zero God Fixtures) |

### Universal Conventions Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Payload Variable Names**| Fluctuating between `request`, `req`, `cmd`, `q`, `data` | Strict CQRS payload naming: `command` for Commands, `query` for Queries, `result` for outputs |
| **Variable Abbreviations**| Cryptic single-letter or clipped names: `t`, `v`, `repo`, `uow`, `ctx`, `svc` | Full expressive semantic names: `template`, `version`, `templateRepository`, `unitOfWork`, `projectContext` |
| **Inline Class Qualifiers**| Inlining full namespace paths: `SmkDoc.Domain.Entities.Template` | Mandatory top-level `using` statements at file head; clean short class names in body |
| **Chained Instantiations**| Nesting instantiation inside method calls: `repo.Add(Template.Create(...))` | Explicit local variable assignment before passing into method or return |
| **Query Verb Synonyms** | Using synonyms: `Find`, `Fetch`, `Query`, `Search`, `Retrieve` | Strict Lexical Verbs: `Get` (single/nullable), `List` (paged/multiple), `Exists` (bool), `Count` (numeric) |
| **Command Verb Synonyms**| Using synonyms: `Add`, `Insert`, `Generate`, `Modify`, `Edit`, `Delete`, `Remove` | Strict Lexical Verbs: `Create`, `Update`, `Deactivate` (soft-delete), `Publish`, `Assign`, `Revoke` |
| **Namespace Layout** | Curly brace block namespaces: `namespace Foo { ... }` | File-scoped namespaces: `namespace SmkDoc.Domain.Entities;` |
| **Constructor DI** | Block constructors with manual field assignments (`_foo = foo;`) | C# 13 Primary Constructors: `public sealed class Foo(IBar bar)` |
| **Collection Allocation**| Legacy initializers: `new List<T>()`, `new()` | C# 12+ Collection Expressions: `[]` |
| **Cancellation Tokens** | Inconsistent names: `cancellationToken`, `cancelToken`, `token` | Parameter name strictly `ct`: `CancellationToken ct = default` |
| **Temporal Injection** | Direct clock queries (`DateTime.UtcNow`, `DateTime.Now`) | Explicit parameter `DateTimeOffset now` or injected `TimeProvider` |
| **Exception Handling** | Swallowed/empty catch blocks: `catch (Exception) {}` | Let bubble to `GlobalExceptionHandler` OR enrich and rethrow strongly-typed domain exception |
| **Test DRY vs DAMP** | Over-abstracting test setup into opaque helper chains | DAMP (Descriptive And Meaningful Phrases) over DRY in tests; explicit 3-phase AAA |

---

## 2. Level 2: Core Lexicon, Syntax Catalogs & Cognitive Conventions Engine

### Engineering Rationale & Architectural Decisions
* **Why Strict CQRS Payload Naming (`command` vs `query` vs `result`):** Using generic `request` obscures whether an operation mutates state or reads data. Naming parameters `command` and `query` primes the LLM with CQRS semantics instantly.
* **Why Zero Cryptic Abbreviations:** Abbreviations (`repo`, `ctx`, `uow`, `t`, `v`) carry ambiguous semantic weight across different domains. Full descriptive names (`templateRepository`, `projectContext`, `unitOfWork`) eliminate token confusion and make codebase ASTs self-documenting.
* **Why Mandatory Top-Level `using` Directives:** Inlining namespaces like `SmkDoc.Domain.Entities.Template` clutters code lines, degrades readability, and triggers line wraps that fracture LLM attention blocks.
* **Why Explicit Local Variable Assignment Before Consumption:** Nesting constructors inside arguments (`AddAsync(Template.Create(...))`) creates dense AST nests where parameter type mismatches are difficult to pinpoint during compilation and debugging.
* **Why Strict Lexical Verbs over Synonyms:** Eliminates LLM token entropy. When multiple valid synonyms exist (`Get` vs `Find` vs `Fetch`), transformer softmax distribution flattens, resulting in non-deterministic naming drift across turns.
* **Why File-Scoped Namespaces in C#:** Eliminates one unnecessary indentation level across entire files, reducing whitespace token bloat.

### Cognitive Conventions Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Zero Syntactic Discordance, Deterministic Naming & Total Cross-Stack Symmetry
  TradeOffPriority: SyntacticConsistency > LexicalStrictness > ReadabilityTransparency > PersonalCodingStyle

ProactiveConventionsGuards:
  - Scope: SmkDoc.Application
    Trigger: Command UseCase parameter named request or cmd instead of command
    CanonicalAction: Rename parameter strictly to command >> Ref: [RULE-CONV-001]

  - Scope: SmkDoc.Application
    Trigger: Query UseCase parameter named request or q instead of query
    CanonicalAction: Rename parameter strictly to query >> Ref: [RULE-CONV-001]

  - Scope: FullStack
    Trigger: Variable declared with abbreviation (repo, uow, ctx, svc, t, v, d)
    CanonicalAction: Expand to full semantic name (templateRepository, unitOfWork, projectContext, template) >> Ref: [RULE-CONV-002]

  - Scope: SmkDoc
    Trigger: Fully-qualified namespace path inlined in method body (e.g. SmkDoc.Domain.*)
    CanonicalAction: Add top-level using directive at file head and use short class name >> Ref: [RULE-CONV-003]

  - Scope: SmkDoc
    Trigger: Instantiating object directly as inline method argument without local variable
    CanonicalAction: Assign to typed or var local variable before method invocation >> Ref: [RULE-CONV-004]

CoreExtensionSlots:
  NamingLexiconSlot: LexicalVerbMatrix (SSoT verb, noun, and payload variable contract)
  VariableNamingSlot: AntiAbbreviationMatrix (Full semantic name mapping)
  CodeLayoutSlot: FileScopedStructure (Top-to-bottom member layout standard)
  ExceptionHierarchySlot: DomainException / ApplicationException (RFC 9457 mapping)
```

### Standard Recurring Payload Variables Matrix
Every method, handler, and UseCase MUST adhere strictly to this payload variable standard:

| Scope / Role | Mandatory Variable Name | Banned Synonyms (! REJECTED) | Example Method Signature |
|---|---|---|---|
| **Command Payload** | `command` | `request`, `cmd`, `req`, `c`, `payload` | `ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)` |
| **Query Payload** | `query` | `request`, `q`, `qry`, `req`, `payload` | `ExecuteAsync(ListTemplatesQuery query, CancellationToken ct)` |
| **UseCase Execution Output**| `result` | `res`, `output`, `data`, `dto`, `response` | `var result = await useCase.ExecuteAsync(command, ct);` |
| **Controller Action Output**| `response` | `resp`, `res`, `result` | `return Ok(new ApiResponse<TemplateResultDto>(result));` |
| **Temporal Parameter** | `now` | `currentTime`, `utc`, `time`, `dt`, `dateTime` | `Template.Create(projectId, name, slug, category, now)` |
| **Cancellation Token** | `ct` | `cancellationToken`, `cancelToken`, `token` | `Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct)` |

### Semantic Variable Naming & Anti-Abbreviation Dictionary
The codebase strictly prohibits lazy, clipped, or single-letter variable names. The ONLY permitted short identifiers are `ct` (CancellationToken) and `id` (Identifier):

| Banned Abbreviation (! REJECTED) | Mandatory Semantic Full Name | Context Scope |
|---|---|---|
| `repo`, `rep` | `templateRepository`, `documentRepository`, etc. | Dependency Injection / Repositories |
| `uow` | `unitOfWork` | Transaction Management |
| `ctx` | `projectContext`, `executionContext`, `dbContext` | Ambient Context & DB |
| `svc` | `storageService`, `renderService`, etc. | Domain / Infrastructure Services |
| `t`, `tmpl` | `template` | Domain Entity |
| `v`, `ver` | `version`, `templateVersion` | Child Entity |
| `d`, `doc` | `document` | Domain Entity |
| `m`, `map` | `fieldMapping` | Child Entity |
| `cfg` | `configuration` | Options / Settings |
| `mgr` | `manager` | Infrastructure Component |
| `tmp` | `temporary` | Local Variables |
| `msg` | `message` | Communication / Events |
| `ex` | `exception`, `validationException` | Catch Blocks (use descriptive name) |
| `req` | `command` or `query` (per operation type) | Method Parameters |
| `res` | `result` or `response` | Return Values |

### Strict Lexical Verb & Noun Dictionary
All codebase interfaces, methods, commands, and queries MUST select names exclusively from this authoritative dictionary:

| Functional Intent | Mandatory Verb | Banned Synonyms (! REJECTED) | Canonical Code Pattern |
|---|---|---|---|
| **Single Entity Read** | `Get` | `Find`, `Fetch`, `Query`, `Search`, `Retrieve`, `Read` | `GetByIdAsync(id, ct)`, `GetBySlugAsync(slug, ct)` |
| **Collection Read** | `List` | `GetAll`, `FetchAll`, `Filter`, `Browse`, `Search` | `ListByProjectIdAsync(projectId, ct)`, `ListTemplatesQuery` |
| **Existence Check** | `Exists` | `Check`, `Has`, `IsPresent`, `Validate` | `SlugExistsAsync(slug, ct)`, `IsActive` |
| **Count Calculation** | `Count` | `GetTotal`, `Size`, `Length`, `TotalCount` | `CountByProjectIdAsync(projectId, ct)` |
| **Resource Creation** | `Create` | `Add`, `Insert`, `Generate`, `New`, `Register` | `CreateTemplateCommand`, `Template.Create(...)` |
| **Resource Update** | `Update` | `Modify`, `Edit`, `Change`, `Patch`, `Mutate` | `UpdateTemplateCommand`, `template.Update(...)` |
| **Soft Deletion** | `Deactivate` | `Delete`, `Remove`, `Disable`, `Archive`, `Drop` | `DeactivateTemplateCommand`, `template.Deactivate(...)` |
| **Child Collection Add**| `Add` | `Attach`, `Append`, `Insert`, `Push` | `template.AddFieldMapping(...)`, `template.AddDataset(...)` |
| **Role / Perm Binding** | `Assign` | `Grant`, `Bind`, `Attach`, `SetRole` | `AssignRoleCommand`, `user.AssignRole(...)` |
| **Token / Key Revocation**| `Revoke` | `Cancel`, `Invalidate`, `Kill`, `Expire` | `RevokeApiKeyCommand`, `apiKey.Revoke(...)` |
| **State Publication** | `Publish` | `Deploy`, `Release`, `Activate`, `Promote` | `PublishVersionCommand`, `template.PublishVersion(...)` |

### Symmetrical Casing & Wire Format Matrix
| Layer & Language | Construct Category | Casing Convention | Concrete Code Example |
|---|---|---|---|
| **C# (Backend)** | Class, Record, Struct, Interface | `PascalCase` | `CreateTemplateCommand`, `ITemplateRepository` |
| **C# (Backend)** | Method, Property, Smart Enum | `PascalCase` | `ExecuteAsync`, `CurrentVersionId`, `OutputFormat.Pdf` |
| **C# (Backend)** | Primary Constructor Parameter | `camelCase` (Zero prefix) | `templateRepository`, `unitOfWork`, `timeProvider` |
| **C# (Backend)** | Private Backing Field | `_camelCase` (Underscore prefix) | `private readonly List<FieldMapping> _fieldMappings = [];` |
| **C# (Backend)** | Cancellation Token Param | strictly `ct` | `CancellationToken ct = default` |
| **HTTP Wire (JSON)** | Object Property Key | `camelCase` | `{"projectId": "...", "templateName": "..."}` |
| **HTTP Wire (REST)** | Route Path Resource | `kebab-case` plural | `/api/v1/templates`, `/api/v1/data-connections` |
| **PostgreSQL (DB)** | Table Name & Column Name | `snake_case` | `template_versions`, `current_version_id` |
| **TypeScript (FE)** | React Component & File | `PascalCase` | `TemplateEditorSplitLayout.tsx`, `Button.tsx` |
| **TypeScript (FE)** | Hook & Utility File | `kebab-case` | `use-live-preview.ts`, `mapping-mapper.ts` |
| **TypeScript (FE)** | Zod Schema File | `kebab-case.schema.ts` | `template.schema.ts`, `data-source.schema.ts` |
| **TypeScript (FE)** | Function, Hook, Variable | `camelCase` | `useLivePreview`, `handleSearch`, `templateData` |

### Suffix & Prefix Type Taxonomy
```
SmkDoc Bounded Suffix Taxonomy:
  Commands:       *Command          (e.g. CreateTemplateCommand)
  Queries:        *Query            (e.g. ListTemplatesQuery)
  UseCases:       *UseCase          (e.g. CreateTemplateUseCase)
  Validators:     *Validator        (e.g. CreateTemplateCommandValidator)
  Result DTOs:    *Dto / *ResultDto (e.g. TemplateResultDto, PagedResultDto<T>)
  Repositories:   *Repository       (e.g. TemplateRepository, Interface: ITemplateRepository)
  Configurations: *Configuration    (e.g. TemplateConfiguration)
  Domain Events:  *DomainEvent      (e.g. TemplatePublishedDomainEvent)
  Unit Tests:     *Tests            (e.g. CreateTemplateUseCaseTests, TemplateAggregateTests)
  Test Builders:  *Builder          (e.g. TemplateBuilder)
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### Pillar 1: SUT Class & Method Archetype (`CreateTemplateUseCase` with Explicit Variables)
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-001]:
* [MUST] Name parameter `command` for Commands and `query` for Queries (prohibit `request`).
* [MUST] Use full semantic variable names (prohibit `repo`, `uow`, `t`).
* [MUST] Import all external namespaces via top-level `using` directives (prohibit inline namespace paths).
* [MUST] Assign all instantiated objects and intermediate expressions to explicit local variables before consumption.
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

// 1. Immutable Request Command
public sealed record CreateTemplateCommand(
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category);

// 2. Single-Responsibility UseCase with Explicit Local Variable Assignments
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateTemplateCommand> validator,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Fail-Fast Validation
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        // 2. Precondition Query
        var slugExists = await templateRepository.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
        {
            throw ConflictException.DuplicateSlug(command.Slug);
        }

        // 3. Explicit Local Variable Assignments (Zero Nested Constructor Inlining)
        var projectId = new ProjectId(command.ProjectId);
        var templateName = TemplateName.Create(command.Name);
        var templateSlug = new TemplateSlug(command.Slug);
        var now = timeProvider.GetUtcNow();

        var template = Template.Create(projectId, templateName, templateSlug, command.Category, now);

        // 4. Persistence Operations with Full Variable Names
        await templateRepository.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);

        // 5. Explicit Result Assignment
        var result = new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CreatedAt,
            template.UpdatedAt);

        return result;
    }
}
```

### Pillar 2: Formatting, Top-Level Using & Layout Architecture Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-002]:
* [MUST] Use file-scoped namespace in all C# files (prohibit curly brace namespace blocks).
* [MUST] Place all `using` directives at top of file, ordered: 1) System => 2) Microsoft => 3) Third-party => 4) Internal (`SmkDoc.*`).
* [MUST] Use collection expressions `[]` for all empty or collection initializers.
* [MUST] Order TypeScript imports: 1) Framework => 2) External libraries => 3) Internal (`@/*`) => 4) Relative (`./`).
```csharp
namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

public sealed class ListTemplatesUseCase(
    ITemplateRepository templateRepository,
    ILogger<ListTemplatesUseCase> logger) : IUseCase<ListTemplatesQuery, IReadOnlyList<TemplateResultDto>>
{
    public async Task<IReadOnlyList<TemplateResultDto>> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        logger.LogInformation("Listing templates for Project {ProjectId}", query.ProjectId);
        var projectId = new ProjectId(query.ProjectId);
        var templates = await templateRepository.ListByProjectIdAsync(projectId, ct);

        if (templates.Count == 0)
        {
            return []; // C# 12+ Collection expression
        }

        var resultList = templates.Select(template => new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CreatedAt,
            template.UpdatedAt)).ToList();

        return resultList;
    }
}
```

### Pillar 3: Code Structure & Member Ordering Archetype (Top-to-Bottom Layout)
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-003]:
* [MUST] Order class members top-to-bottom:
  1. Constants & Static Members
  2. Public Properties (state encapsulation via `private set`)
  3. Private Backing Fields
  4. Parameterless Constructor (for EF Core)
  5. Internal / Factory Constructor
  6. SSoT Static `Create(...)` Factory
  7. Public Business Verbs (mutations & invariant enforcement)
  8. Private Helper Methods / Guards
```csharp
namespace SmkDoc.Domain.Entities;

using System;
using System.Collections.Generic;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

public sealed class Document : BaseEntity<DocumentId>, IMustHaveProject
{
    // 1. Constants
    public const int MaxTitleLength = 200;

    // 2. Encapsulated Properties
    public ProjectId ProjectId { get; private set; }
    public TemplateId TemplateId { get; private set; }
    public string Title { get; private set; } = null!;
    public int CurrentVersionNumber { get; private set; }

    // 3. Private Backing Fields
    private readonly List<DocumentVersion> _versions = [];
    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    // 4. Parameterless Constructor (ORM)
    private Document() { }

    // 5. Internal Constructor
    internal Document(
        DocumentId id,
        ProjectId projectId,
        TemplateId templateId,
        string title,
        DateTimeOffset now) : base(id, now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        TemplateId = Guard.NotEmpty(templateId, nameof(TemplateId));
        Title = Guard.NotBlank(title, nameof(Title), MaxTitleLength);
        CurrentVersionNumber = 1;
    }

    // 6. SSoT Public Factory Method
    public static Document Create(
        ProjectId projectId,
        TemplateId templateId,
        string title,
        DateTimeOffset now)
    {
        return new Document(DocumentId.New(), projectId, templateId, title, now);
    }

    // 7. Public Business Verbs
    public DocumentVersion CreateNextVersion(Stream contentStream, DateTimeOffset now)
    {
        CurrentVersionNumber++;
        var nextVersion = DocumentVersion.Create(Id, CurrentVersionNumber, contentStream, now);
        _versions.Add(nextVersion);
        SetUpdated(now);
        return nextVersion;
    }
}
```

### Pillar 3.1: DAMP Testing Layout Archetype (Explicit AAA)
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-004]:
* [MUST] Follow DAMP (Descriptive And Meaningful Phrases) over DRY in unit tests.
* [MUST] Explicitly declare Arrange, Act, and Assert in every test method without hiding stubs in God Fixtures.
```csharp
namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.GetTemplateById;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

public sealed class GetTemplateByIdUseCaseTests
{
    private readonly ITemplateRepository _templateRepository = Substitute.For<ITemplateRepository>();

    private GetTemplateByIdUseCase CreateSut() => new(_templateRepository);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange (DAMP: Explicit local setup)
        var templateId = Guid.NewGuid();
        var query = new GetTemplateByIdQuery(templateId);
        _templateRepository.GetByIdAsync(Arg.Any<TemplateId>(), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{templateId}*");
    }
}
```

### Pillar 4: Error Handling & RFC 9457 Translation Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-CONV-005]:
* [MUST] Throw strongly-typed domain or application exceptions; strictly prohibit throwing raw `System.Exception`.
* [MUST NOT] Catch and swallow exceptions silently; catch only to enrich context and rethrow or let bubble to `GlobalExceptionHandler`.
* [MUST] Map every domain/application exception to an exact RFC 9457 HTTP Status Code.
```csharp
namespace SmkDoc.Application.Common.Exceptions;

using System;
using System.Collections.Generic;

// 1. Strongly-Typed Exception Hierarchy
public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message)
{
    public static ConflictException DuplicateSlug(string slug) =>
        new($"A template with slug '{slug}' already exists in this project.");
}

public sealed class ForbiddenException(string message) : AppException(message);

public sealed class ValidationException(IDictionary<string, string[]> errors)
    : AppException("One or more validation failures have occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

// 2. Catch-and-Enrich Infrastructure Pattern (Never Swallow)
namespace SmkDoc.Infrastructure.Engines.Gotenberg;

using System.Net.Http;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmkDoc.Application.Common.Exceptions;

public sealed class GotenbergHttpAdapter(HttpClient httpClient)
{
    public async Task<Stream> ExecuteConvertAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(ct);
        }
        catch (HttpRequestException exception)
        {
            // Enrich with upstream Gotenberg context and rethrow strongly-typed exception
            throw new DocumentRenderException($"Gotenberg cluster unreachable or returned error: {exception.Message}", exception);
        }
    }
}
```

---

## 4. Level 4: Condensed Traps & High-Salience Micro-Diffs

### [TRAP-CONV-001]: Inconsistent Payload Parameter Naming
```diff
- public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand request, CancellationToken ct)
+ public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
```
*Rationale: In CQRS, naming command parameters `request` obscures intent; enforce `command` for Commands and `query` for Queries.*

### [TRAP-CONV-002]: Cryptic Variable Abbreviations
```diff
- var t = await repo.GetByIdAsync(id, ct); await uow.CommitAsync(ct);
+ var template = await templateRepository.GetByIdAsync(id, ct); await unitOfWork.CommitAsync(ct);
```
*Rationale: Single-letter or truncated abbreviations (`t`, `repo`, `uow`) degrade code readability and introduce cross-turn token ambiguity.*

### [TRAP-CONV-003]: Inline Namespace Paths vs Top-Level using
```diff
- var template = SmkDoc.Domain.Entities.Template.Create(projectId, name, slug, cat, now);
+ using SmkDoc.Domain.Entities; ... var template = Template.Create(projectId, name, slug, cat, now);
```
*Rationale: Inlining full namespace paths clutters code lines and triggers attention fragmentation; enforce top-level `using` directives.*

### [TRAP-CONV-004]: Chained Instantiation without Local Variables
```diff
- await templateRepository.AddAsync(Template.Create(new ProjectId(command.ProjectId), TemplateName.Create(command.Name), new TemplateSlug(command.Slug), command.Category, now), ct);
+ var projectId = new ProjectId(command.ProjectId); var name = TemplateName.Create(command.Name); var slug = new TemplateSlug(command.Slug); var template = Template.Create(projectId, name, slug, command.Category, now); await templateRepository.AddAsync(template, ct);
```
*Rationale: Chaining nested constructors into arguments obscures compilation failures and degrades debugging readability.*

### [TRAP-CONV-005]: Verb Synonym Drift in Method Names
```diff
- public async Task<Template?> FindByIdAsync(TemplateId id, CancellationToken ct)
+ public async Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct)
```
*Rationale: Using `Find`, `Fetch`, or `Retrieve` causes entropy in LLM naming; enforce strict lexical verb `Get` for single-item lookups.*

### [TRAP-CONV-006]: Empty / Swallowed Catch Block
```diff
- try { await uow.CommitAsync(ct); } catch (Exception) { /* ignore */ }
+ try { await unitOfWork.CommitAsync(ct); } catch (DbUpdateConcurrencyException exception) { throw new ConflictException("Concurrent edit detected.", exception); }
```
*Rationale: Swallowing catch blocks causes silent data corruption, hides deadlocks, and prevents RFC 9457 error translation.*
