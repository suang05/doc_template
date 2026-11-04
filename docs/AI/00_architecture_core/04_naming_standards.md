# [CORE-004]: 04_naming_standards.md — Universal Lexicon, Canonical Verbs & Identifier Standards

<context>
Scope: Lexical Verbs, Payload Variable Naming, Anti-Abbreviation Policy, RESTful Route Syntax, and C# PascalCase to JSON camelCase Wire Matching.
SSoT_For: Method Verb Conventions, DTO Casing, Route Segment Formatting, and CQRS Variable Standard.
Out_Of_Scope: C# 13 language syntax (see: 02_coding_conventions.md), System Architecture tiers (see: 01_system_architecture.md).
Target: `backend-v2/` and `frontend-v2/`.
TriggerSignals: [SIGNAL_API_MUTATION], [SIGNAL_STRUCTURE_MUTATION]
</context>

---

## 1. Level 1: Universal Lexicon & Identifier Invariants Matrix

### 1.1 Strict Lexical Verb & Noun Dictionary
All codebase interfaces, methods, commands, and queries MUST select names exclusively from this authoritative dictionary:

| Functional Intent | Mandatory Verb (>> MANDATORY) | Banned Synonyms (! REJECTED) | Canonical Code Pattern |
|---|---|---|---|
| **Single Entity Read** | `Get` | `Find`, `Fetch`, `Query`, `Search`, `Retrieve`, `Read` | `GetByIdAsync(id, ct)`, `GetBySlugAsync(slug, ct)` |
| **Collection Read** | `List` | `GetAll`, `FetchAll`, `Filter`, `Browse`, `Search` | `ListByProjectIdAsync(projectId, ct)`, `ListTemplatesQuery` |
| **Existence Check** | `Exists` | `Check`, `Has`, `IsPresent`, `Validate` | `SlugExistsAsync(slug, ct)`, `IsActive` |
| **Count Calculation** | `Count` | `GetTotal`, `Size`, `Length`, `TotalCount` | `CountByProjectIdAsync(projectId, ct)` |
| **Resource Creation** | `Create` | `Add`, `Insert`, `New`, `Register` (Use `Generate` exclusively for Document synthesis) | `CreateTemplateCommand`, `Template.Create(...)` |
| **Resource Update** | `Update` | `Modify`, `Edit`, `Change`, `Patch`, `Mutate` | `UpdateTemplateCommand`, `template.Update(...)` |
| **Soft Deletion** | `Deactivate` | `Delete`, `Remove`, `Disable`, `Archive`, `Drop` | `DeactivateTemplateCommand`, `template.Deactivate(...)` |
| **Child Collection Add**| `Add` | `Attach`, `Append`, `Insert`, `Push` | `template.AddFieldMapping(...)`, `template.AddDataset(...)` |
| **Role / Perm Binding** | `Assign` | `Grant`, `Bind`, `Attach`, `SetRole` | `AssignRoleCommand`, `user.AssignRole(...)` |
| **Token / Key Revocation**| `Revoke` | `Cancel`, `Invalidate`, `Kill`, `Expire` | `RevokeApiKeyCommand`, `apiKey.Revoke(...)` |
| **State Publication** | `Publish` | `Deploy`, `Release`, `Activate`, `Promote` | `PublishVersionCommand`, `template.PublishVersion(...)` |

#### 1.1.1 Domain-Specific Pipeline Allowlist (Specialized Operations)
Specialized document synthesis and infrastructure engines are strictly permitted to use the following domain-specific verbs:
* **`Render` / `Generate`**: Document synthesis pipelines (`RenderDocumentCommand`, `GenerateDocumentUseCase`, `IRenderEngine.RenderStreamAsync`).
* **`Preview`**: Interactive draft rendering (`PreviewDocumentUseCase`, `PreviewMappingUseCase`).
* **`Extract`**: External data extraction (`IDatasetExtractor.ExtractAsync`, `ExecuteDatasetQueryUseCase`).
* **`Spool` / `Stream`**: High-throughput zero-LOH I/O streams (`HybridSpoolStream`, `StreamDocumentQuery`).
* **`Parse` / `Scan`**: Template placeholders and syntax validation (`ScanTemplatePlaceholdersUseCase`, `ParseTemplateDraftUseCase`).
* **`Test`**: Network connectivity and diagnostic health verification (`TestDataConnectionUseCase`, `TestConnectionCommand`).

### 1.2 Standard Recurring Payload Variables Matrix
| Scope / Role | Mandatory Variable Name (>> MANDATORY) | Banned Synonyms (! REJECTED) | Example Method Signature |
|---|---|---|---|
| **Controller HTTP Body Payload** | `request` | `body`, `payload`, `data`, `dto`, `req` | `Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)` |
| **Application Command Payload** | `command` | `request` (inside UseCase), `cmd`, `req`, `c`, `payload` | `ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)` |
| **Application Query Payload** | `query` | `request` (inside UseCase), `q`, `qry`, `req`, `payload` | `ExecuteAsync(ListTemplatesQuery query, CancellationToken ct)` |
| **UseCase Execution Output**| `result` | `res`, `output`, `data`, `dto`, `response` | `var result = await useCase.ExecuteAsync(command, ct);` |
| **Controller Action Output**| `response` | `resp`, `res`, `result` | `return Ok(new ApiResponse<TemplateResultDto>(result));` |
| **Temporal Parameter** | `now` | `currentTime`, `utc`, `time`, `dt`, `dateTime` | `Template.Create(projectId, name, slug, category, now)` |
| **Cancellation Token** | `ct` | `cancellationToken`, `cancelToken`, `token` | `Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct)` |

### 1.3 Semantic Variable Naming & Anti-Abbreviation Dictionary
The codebase strictly prohibits lazy, clipped, or single-letter variable names. The ONLY permitted short identifiers are `ct` (CancellationToken) and `id` (Identifier):

| Banned Abbreviation (! REJECTED) | Mandatory Semantic Full Name (>> MANDATORY) | Context Scope |
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
| `req` | `request` (in Controller `[FromBody]`) or `command`/`query` (in Application UseCases) | Method Parameters |
| `res` | `result` or `response` | Return Values |

### 1.4 Symmetrical Casing & Wire Format Matrix
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

---

## 2. Level 2: Core Lexicon AST & Cognitive Governance Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why Strict Verbs over Synonyms**: When multiple valid synonyms exist (`Get` vs `Find` vs `Fetch`), the LLM's transformer softmax probability distribution flattens, resulting in non-deterministic naming drift across turns. Restricting verbs to strict SSoTs guarantees deterministic code generation.
* **Why Anti-Abbreviation Policy**: Clipped abbreviations (`repo`, `ctx`, `uow`, `t`, `v`) carry ambiguous semantic weight across different domains. Full expressive names (`templateRepository`, `unitOfWork`) eliminate token confusion and make codebase ASTs self-documenting.
* **Why Layer-Specific Variable Names**: Using generic `request` everywhere obscures whether an operation mutates state or reads data. Naming parameters `command` and `query` primes the AI with CQRS semantics instantly.

### 2.2 Cognitive Lexicon Governance & Identifier Interceptors
```yaml
OptimizationObjectives:
  PrimaryObjective: Lexical Determinism, Token Entropy Minimization & Cross-Stack Casing Parity
  TradeOffPriority: LexiconDeterminism > LayerRoleTransparency > AntiAbbreviationDiscipline > WirePredictability

ProactiveNamingGuards:
  - Scope: Global
    Trigger: Synonym usage for query verbs (Find, Fetch, Lookup, Search)
    CanonicalAction: Enforce canonical verbs: Get (single), List (multiple), Exists (bool), Count (numeric) >> Ref: [RULE-NAME-001]

  - Scope: Global
    Trigger: Synonym usage for command verbs (Add, Insert, New, Modify, Edit)
    CanonicalAction: Enforce canonical verbs: Create, Update, Deactivate, Publish, Assign, Revoke >> Ref: [RULE-NAME-002]

  - Scope: SmkDoc.Api
    Trigger: Shorthand or clipped variable names (req, cmd, q, res, repo, ctx, uow)
    CanonicalAction: Enforce expressive layer variable names (request, command, query, result, templateRepository, projectContext) >> Ref: [RULE-NAME-003]

  - Scope: SmkDoc.Api
    Trigger: Singular, camelCase, or unversioned route declaration
    CanonicalAction: Enforce plural kebab-case starting with /api/v1/ >> Ref: [RULE-NAME-004]
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Canonical CQRS Layered Action Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-NAME-003]:
* [MUST] Use `request` for `[FromBody]`, `command` for Application Command, and `result` for Application Output.
* [MUST NOT] Use clipped abbreviations (`req`, `cmd`, `dto`).

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Api.Controllers.Authoring;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplateController(
    IUseCase<CreateTemplateCommand, TemplateResultDto> createTemplateUseCase) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TemplateResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct)
    {
        // 1. Layer boundary translation: request -> command
        var command = new CreateTemplateCommand(request.Name, request.Slug);

        // 2. Execution dispatch: command -> result
        var result = await createTemplateUseCase.ExecuteAsync(command, ct);

        // 3. Layer boundary translation: result -> HTTP envelope
        return CreatedAtAction(nameof(Create), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-NAME-001]: Verb Synonym Collisions (`Find` instead of `Get`, `Add` instead of `Create`)
```diff
- public async Task<Template?> FindByIdAsync(Guid id) => await repo.FindAsync(id);
- public async Task AddTemplateAsync(Template t) => await repo.AddAsync(t);
+ public async Task<Template?> GetByIdAsync(Guid id) => await templateRepository.GetByIdAsync(id);
+ public async Task CreateTemplateAsync(Template template) => await templateRepository.AddAsync(template);
```
*Rationale: Synonym usage increases token entropy and leads to fragmented repository interfaces.*

### [TRAP-NAME-002]: Cryptic Abbreviations Across Parameters and Dependencies
```diff
- public sealed class TemplateHandler(ITemplateRepository repo, IUnitOfWork uow, ICurrentProjectContext ctx) {
-     public async Task<Dto> Handle(Cmd c, CancellationToken token) { ... }
- }
+ public sealed class CreateTemplateUseCase(
+     ITemplateRepository templateRepository,
+     IUnitOfWork unitOfWork,
+     ICurrentProjectContext projectContext) {
+     public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) { ... }
+ }
```
*Rationale: Abbreviations degrade codebase AST readability and confuse LLM context tracking.*

### [TRAP-NAME-003]: Non-Standard Route Casing and Singular Naming
```diff
- [Route("api/template")]
- [Route("api/v1/dataConnection")]
+ [Route("api/v1/templates")]
+ [Route("api/v1/data-connections")]
```
*Rationale: RESTful APIs require plural kebab-case with canonical `/api/v1/` prefixing.*
