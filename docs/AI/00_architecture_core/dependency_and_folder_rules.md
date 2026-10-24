# [CORE-001]: dependency_and_folder_rules.md — Clean Architecture Boundaries & Project Taxonomy

<context>
Scope: Clean Architecture Boundaries, Directional Dependencies, Bounded Context Folder Taxonomy & C# 13 System Invariants
Target: `backend-v2/` (`SmkDoc.Domain`, `SmkDoc.Application`, `SmkDoc.Infrastructure`, `SmkDoc.Api`)
Authority: Absolute SSoT Invariant (Zero architectural regression permitted)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_STRUCTURE_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Layer Responsibilities & Isolation Invariants Matrix

| Layer Project | Allowed Inbound References | Prohibited Patterns (! BANNED) | Canonical Mandates (>> MANDATORY) |
|---|---|---|---|
| **`SmkDoc.Domain`** | `SmkDoc.Application`, `SmkDoc.Infrastructure`, `SmkDoc.Api` | ! EF Core / ASP.NET / MinIO references<br/>! Public property setters (`set;`)<br/>! Direct system clock queries (`DateTime.UtcNow`) | >> Pure C# 13 POCOs<br/>>> Encapsulated state (`private/protected set`)<br/>>> Exactly 1 canonical factory method per aggregate (`Create`)<br/>>> TimeProvider injected as parameter |
| **`SmkDoc.Application`** | `SmkDoc.Infrastructure`, `SmkDoc.Api` | ! Returning Domain Entities across layer boundary<br/>! Direct SQL or EF Core `DbContext` references<br/>! Synchronous blocking (`.Result`, `.Wait()`) | >> Positional `record` for all Commands/Queries/DTOs<br/>>> Orchestrate domain via `IUseCase<TReq, TRes>`<br/>>> Return Application DTO records (exceptions bubble to RFC 9457 GlobalExceptionHandler)<br/>>> Transaction boundary via `IUnitOfWork.CommitAsync(ct)` |
| **`SmkDoc.Infrastructure`** | `SmkDoc.Api` (DI composition root only) | ! Exposing EF `DbContext` or raw entities outside<br/>! Bypassing ambient tenant global query filters<br/>! Unbounded in-memory buffering (`byte[]`) | >> Hide all persistence/IO behind interfaces<br/>>> Stream Gotenberg/MinIO directly to consumer<br/>>> Implement EF Core `IEntityTypeConfiguration<T>`<br/>>> Enforce `xmin` optimistic concurrency |
| **`SmkDoc.Api`** | None (Entry Point) | ! Business logic or validation inside controllers<br/>! Controller actions exceeding 5 lines of code<br/>! Direct Entity consumption or return | >> Thin orchestrators with C# 13 Primary Constructors<br/>>> Route: `api/v1/{resource}`<br/>>> RFC 9457 ProblemDetails via `IExceptionHandler`<br/>>> `ApiResponse<T>` / `PagedApiResponse<T>` envelopes |

---

## 2. Level 2: Core Architecture AST & Topology

### Concentric Layers & Dependency Inversion
```mermaid
graph TD
    Api["SmkDoc.Api (Presentation)"] --> Application["SmkDoc.Application (UseCases)"]
    Infrastructure["SmkDoc.Infrastructure"] --> Application
    Application --> Domain["SmkDoc.Domain (POCOs)"]
    Infrastructure --> Domain
```

### Bounded Context Taxonomy
```
backend-v2/src/SmkDoc.Application/Modules/
├── Authoring/                  # Template lifecycle & design assets
│   ├── Commands/               # CreateTemplate, UpdateTemplate, PublishVersion, MapFields
│   ├── Queries/                # GetTemplateById, ListTemplates, GetTemplateVersion
│   └── DTOs/                   # TemplateResultDto, TemplateVersionResultDto, FieldMappingDto
│
├── Rendering/                  # Document production & output pipelines
│   ├── Commands/               # RenderDocument, RenderPreview, CancelGeneration
│   ├── Queries/                # GetDocumentById, GetGenerationLog, StreamDocument, GetLogPayload
│   └── DTOs/                   # RenderDocumentResultDto, PreviewResultDto, GenerationLogDto, LogPayloadResultDto
│
├── Integration/                # External data connections & query datasets
│   ├── Commands/               # CreateDataConnection, TestConnection, SaveDataset
│   ├── Queries/                # GetDataConnectionById, ExecuteDatasetQuery, ListDatasets
│   └── DTOs/                   # DataConnectionResultDto, DatasetResultDto, QueryPreviewDto
│
└── IdentityAccess/             # Multi-tenancy, authentication & authorization
    ├── Commands/               # LoginUser, RefreshToken, CreateApiKey, SwitchProject
    ├── Queries/                # GetCurrentUser, ListProjects, ValidateApiKey
    └── DTOs/                   # AuthTokenResultDto, UserProfileResultDto, ApiKeyResultDto
```

### The 5-File CQRS Slice Pattern
Every Application capability follows the 5-File Slice pattern for maximum testability and decoupling:
```
Modules/{Context}/
├── Commands/{Action}{Entity}Command.cs      # 1. Positional record request
├── Commands/{Action}{Entity}UseCase.cs      # 2. Single-responsibility orchestrator
├── Commands/{Action}{Entity}Validator.cs    # 3. FluentValidation / Guard rules
├── DTOs/{Entity}ResultDto.cs                # 4. Immutable positional record DTO
└── (In SmkDoc.Api) Controllers/V1/          # 5. Thin presentation action
```

### Strongly-Typed ID Explicit Operator Standard
Every strongly-typed ID struct MUST be declared as `public readonly record struct Name(Guid Value) : IComparable<Name>` ($\le 16$ bytes, stack-allocated):
```csharp
public readonly record struct TemplateId(Guid Value) : IComparable<TemplateId>
{
    public static TemplateId New() => new(Guid.CreateVersion7());
    public static TemplateId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public int CompareTo(TemplateId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();

    // Explicit conversions preventing accidental cross-domain ID pollution
    public static explicit operator Guid(TemplateId id) => id.Value;
    public static explicit operator TemplateId(Guid value) => new(value);

    // Cross-type equality operators for direct comparison with Guid
    public static bool operator ==(TemplateId left, Guid right) => left.Value == right;
    public static bool operator !=(TemplateId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, TemplateId right) => left == right.Value;
    public static bool operator !=(Guid left, TemplateId right) => left != right.Value;
}
```

---

## 3. Level 3: Canonical Implementation Archetypes

### Positional Records for Requests & Responses
>> MANDATORY CANONICAL TEMPLATE [RULE-STRUCT-001]:
* [MUST] Use C# `sealed record` with positional parameters for all Commands, Queries, and DTOs.
* [MUST NOT] Leak domain entities (`Template`, `User`) directly into DTOs or public contracts.
* [MUST] Enforce immutability across layer boundaries.
```csharp
// >> CANONICAL: Immutable positional records (Zero framework/MediatR coupling)
public sealed record CreateTemplateCommand(
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category);

public sealed record TemplateResultDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    Guid? CurrentVersionId,
    string? FileFormat,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
```

### Primary Constructor UseCase Orchestrator
>> MANDATORY CANONICAL TEMPLATE [RULE-STRUCT-002]:
* [MUST] Declare dependencies via C# 13 Primary Constructor with pure camelCase parameter names (e.g. `templateRepository`, `unitOfWork`).
* [MUST NOT] Use symbol prefixes (`_templateRepository`, `m_templateRepository`).
* [MUST] Implement `IUseCase<TRequest, TResponse>` with single-responsibility method `ExecuteAsync(TRequest request, CancellationToken ct)`.
```csharp
// >> CANONICAL: Pure camelCase, zero prefix, directly captured by members
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CreateTemplateUseCase> logger) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(request.ProjectId, request.Name, request.Slug, request.Category, now);
        await templateRepository.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);
        return template.ToResultDto();
    }
}
```

---

## 4. Level 4: Anti-Pattern Interceptors & Micro-Diffs

### [TRAP-STRUCT-001]: Primary Constructor Parameter Naming
```csharp
// ! BANNED: Underscore or symbol prefix
public sealed class BadUseCase(ITemplateRepository _templateRepository) { } // REJECTED

// >> CANONICAL: Pure camelCase parameter naming (Regex: ^[a-z][a-zA-Z0-9]+$)
public sealed class GoodUseCase(ITemplateRepository templateRepository) { }
```

### [TRAP-STRUCT-002]: Structured Logging vs Interpolation
```csharp
// ! BANNED: String interpolation causes memory allocations and defeats semantic filtering
logger.LogInformation($"Processing {templateId}"); // REJECTED

// >> CANONICAL: Semantic structured logging tokens
logger.LogInformation("Processing template render {TemplateId} for tenant {ProjectId}", templateId, projectId);
```

### [TRAP-STRUCT-003]: Collection Expressions
```csharp
// ! BANNED: Verbose constructor instantiations
private readonly List<FieldMapping> _fieldMappings = new List<FieldMapping>();

// >> CANONICAL: C# 13 collection expression syntax
private readonly List<FieldMapping> _fieldMappings = [];
IReadOnlyList<string> tags = ["invoice", "finance"];
```

### [TRAP-STRUCT-004]: Intermediate Variable Instantiation
```csharp
// ! BANNED: Inlined instantiation obscures debugging and stack traces
var result = await useCase.ExecuteAsync(new CreateTemplateCommand(request.Name, request.Slug), ct);

// >> CANONICAL: Explicit intermediate variable assignment
var command = new CreateTemplateCommand(request.Name, request.Slug);
var result = await useCase.ExecuteAsync(command, ct);
```

### Zero Decorative Emojis
Zero decorative emojis permitted in source code, comments, test assertions, or documentation.
Canonical symbolic dictionary: `=>`, `!`, `::`, `>>`, `&`, `|`, `>`, `<`.
