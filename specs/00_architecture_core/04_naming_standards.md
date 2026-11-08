# [CORE-004]: 04_naming_standards.md — Universal Lexicon, Canonical Verbs & Identifier Standards

<context>
Scope: Lexical Verbs, Payload Variable Naming, Anti-Abbreviation Policy, RESTful Route Syntax, and C# PascalCase to JSON camelCase Wire Matching.
SSoT_For: Method Verb Conventions, DTO Casing, Route Segment Formatting, and CQRS Variable Standard in Source Code.
Out_Of_Scope: Documentation prose, Markdown content/headings, architectural narratives, and chat dialogue. Governs STRICTLY Source Code Identifiers (Class, Record, Method, Property, and Variable AST tokens) inside backend-v2/ and frontend-v2/. Also out of scope: C# 13 language syntax (see: 02_coding_conventions.md), System Architecture tiers (see: 01_system_architecture.md).
Target: Source Code Identifiers in `backend-v2/` and `frontend-v2/` (Rules govern code only; strictly prohibited from restricting markdown documentation text or chat dialogue).
ArchitecturalSignals: [ARCH_EVOLUTION_NAMING]
</context>

---

## 1. Level 1: Universal Lexicon & Identifier Invariants Matrix

### 1.1 Authoritative Lexical Verb Dictionary
All interfaces, repository methods, commands, and queries must select verbs exclusively from this canonical dictionary:

| Functional Intent | Mandatory SSoT Verb | Banned Synonyms | Canonical Identifier Signature |
|---|---|---|---|
| **Single Entity Read** | `Get` | Find, Fetch, Query, Search, Retrieve, Read | `GetByIdAsync(id, ct)`, `GetBySlugAsync(slug, ct)` |
| **Collection Read** | `List` | GetAll, FetchAll, Filter, Browse, Search | `ListByProjectIdAsync(projectId, ct)`, `List{Aggregate}sQuery` |
| **Existence Check** | `Exists` | Check, Has, IsPresent, Validate | `SlugExistsAsync(slug, ct)`, `IsActive` |
| **Count Calculation** | `Count` | GetTotal, Size, Length, TotalCount | `CountByProjectIdAsync(projectId, ct)` |
| **Resource Creation** | `Create` | Add, Insert, New, Register | `Create{Aggregate}Command`, `{Aggregate}.Create(...)` |
| **Resource Update** | `Update` | Modify, Edit, Change, Patch, Mutate | `Update{Aggregate}Command`, `entity.Update(...)` |
| **State Deactivation**| `Deactivate` | Delete, Remove, Disable, Archive, Drop | `Deactivate{Aggregate}Command`, `entity.Deactivate(...)` |
| **Child Entity Append**| `Add` | Attach, Append, Insert, Push | `entity.Add{ChildEntity}(...)` |
| **Role / Perm Binding**| `Assign` | Grant, Bind, Attach, SetRole | `AssignRoleCommand`, `user.AssignRole(...)` |
| **Secret / Key Revocation**| `Revoke` | Cancel, Invalidate, Kill, Expire | `RevokeApiKeyCommand`, `apiKey.Revoke(...)` |
| **State Publication** | `Publish` | Deploy, Release, Activate, Promote | `PublishVersionCommand`, `entity.PublishVersion(...)` |
| **Authority Transfer** | `Transfer` | Reassign, Handover, ChangeOwner | `TransferProjectOwnerCommand` |

#### 1.1.1 Specialized Document Synthesis Pipeline Verbs
* **`Render` / `Generate`**: Document synthesis pipelines (`RenderDocumentCommand`, `IRenderEngine.RenderStreamAsync`).
* **`Preview`**: Interactive in-memory draft rendering (`PreviewDocumentUseCase`).
* **`Extract`**: External data extraction (`IDatasetExtractor.ExtractAsync`).
* **`Stream`**: High-throughput zero-LOH response streaming (`StreamDocumentQuery`).
* **`Scan` / `Parse`**: Template placeholders and schema parsing (`ScanPlaceholdersUseCase`).
* **`Test`**: Network connectivity and health diagnostics (`TestDataConnectionUseCase`).

### 1.2 Standard Recurring Payload Variables
All local variables and parameters across layers must strictly conform to these standardized tokens:

| Architectural Layer / Role | Mandatory Variable Token | Prohibited Synonyms | Scope Boundary |
|---|---|---|---|
| **Controller HTTP Body** | `request` | body, payload, data, dto, req | `[FromBody] {Verb}{Aggregate}Request request` |
| **Application Command** | `command` | request, cmd, req, c, payload | `ExecuteAsync({Verb}{Aggregate}Command command, ct)` |
| **Application Query** | `query` | request, q, qry, req, payload | `ExecuteAsync(Get{Aggregate}Query query, ct)` |
| **UseCase Output / Controller Return** | `result` | response, res, output, data, dto, entity | `var result = await useCase.ExecuteAsync(command, ct);` |
| **Temporal Injection** | `now` | currentTime, utc, time, dt, dateTime | `{Aggregate}.Create(..., now)` |
| **Ambient Concurrency Context** | `concurrencyContext` | etag, ifMatchHeader, tokenChecker | `IConcurrencyContext concurrencyContext` |
| **Cancellation Token** | `ct` | cancellationToken, cancelToken, token | `CancellationToken ct = default` |

### 1.3 Anti-Abbreviation Standards
The codebase strictly prohibits clipped identifiers. The only permitted short tokens are `ct` (`CancellationToken`) and `id` (`Identifier`):
* `repository` (prohibit: `repo`, `rep`)
* `unitOfWork` (prohibit: `uow`)
* `projectContext` / `dbContext` (prohibit: `ctx`)
* `service` (prohibit: `svc`)
* `configuration` (prohibit: `cfg`)
* `exception` (prohibit: `ex`)

### 1.4 Symmetrical Casing, Zero Project Prefix & Wire Format
* **C# Types & Methods**: `PascalCase` (`Create{Aggregate}Command`, `ExecuteAsync`).
* **Zero Project/Brand Type Prefix**: Strictly prohibit prefixing C# or TypeScript type identifiers (classes, interfaces, records, structs, enums) with project or organization names (`SmkDoc*`, `Sammakorn*`). The `SmkDoc.*` namespace already provides scope isolation.
* **C# Primary Constructor Parameters**: pure `camelCase` without prefixes (`{aggregate}Repository`, `unitOfWork`, `timeProvider`).
* **HTTP REST Routes**: Plural `kebab-case` prefixed with version (`/api/v1/{resources}`, `/api/v1/data-connections`).
* **Controller Class Names**: Plural `PascalCase` (`{Aggregate}sController`). Companion diagnostic endpoints: `{Resource}DiagnosticsController`.
* **Database Columns & Tables**: `snake_case` (`template_versions`, `current_version_id`).
* **TypeScript Files**: React Components (`PascalCase.tsx`), Hooks/Utils (`kebab-case.ts`), Schemas (`kebab-case.schema.ts`).

### 1.5 Architectural Role-Based Suffix Taxonomy
Every non-entity class must terminate with a deterministic suffix declaring its exact architectural responsibility. Vague catch-all suffixes (`*Helper`, `*Util`, `*Utils`, `*Manager`, `*Info`, `*Data`) are strictly prohibited:

| Architectural Responsibility | Mandatory Type Suffix | Banned Vague Suffixes | Canonical Example |
|---|---|---|---|
| **Application Workflow Orchestrator** | `*UseCase` | `*Service`, `*Manager`, `*Handler` | `Create{Aggregate}UseCase` |
| **Application Input / Output Contracts** | `*Command`, `*Query`, `*ResultDto` | `*Dto` (for UseCase root), `*Response`, `*Data` | `{Aggregate}ResultDto` |
| **Standardized Object / Schema Construction** | `*Builder`, `*Factory` | `*Helper`, `*Util`, `*Creator` | `ProblemDetailsBuilder`, `OpenApiOperationIdFactory` |
| **Pure Calculation / Resolution / Parsing** | `*Calculator`, `*Resolver`, `*Parser` | `*Helper`, `*Utils`, `*Processor` | `RequestFingerprintCalculator`, `RateLimitPartitionKeyResolver` |
| **Validation & Security Sanitization** | `*Validator`, `*Sanitizer` | `*Checker`, `*Helper`, `*Guard` (for services) | `ConnectionHostValidator`, `HtmlContentSanitizer` |
| **OpenAPI / Schema Transformation** | `*Transformer`, `*Deduplicator` | `*Modifier`, `*Helper`, `*Patcher` | `OpenApiDocumentTransformer`, `OpenApiOperationTransformer` |
| **HTTP Pipeline Interception** | `*Middleware`, `*Filter`, `*ExceptionHandler` | `*Hook`, `*Manager`, `*Handler` | `ProjectContextMiddleware`, `ETagFilter`, `IdempotencyFilter`, `GlobalExceptionHandler` |
| **EF Core Persistence Interception** | `*SaveChangesInterceptor` | `*Hook`, `*Helper`, `*Listener` | `ConcurrencySaveChangesInterceptor`, `TenantSecuritySaveChangesInterceptor` |
| **Static Policy & Protocol Tokens** | `*Policies`, `*Constants`, `*Names` | `*Config`, `*Strings`, `*Values` | `AuthorizationPolicies`, `RoleConstants`, `HttpHeaderConstants` |

---

## 2. Level 2: Engineering Rationales & Cognitive Interceptors

### 2.1 Engineering Rationale & Causal Decisions
* **Why Strict Verbs over Synonyms**: When multiple synonyms exist (`Get` vs `Find` vs `Fetch`), the LLM's transformer softmax probability distribution flattens, resulting in non-deterministic naming drift. Strict verbs enforce 100% token determinism.
* **Why Banning Variable Name 'response'**: Declaring `var response = ...` inside ASP.NET Core controllers shadows the built-in `ControllerBase.Response` property, creating latent compilation bugs and cognitive ambiguity. Standardizing on `result` guarantees safety.
* **Why Anti-Abbreviation Policy**: Clipped abbreviations carry ambiguous semantic weight across domains. Expressive full names (`{aggregate}Repository`, `unitOfWork`) eliminate token confusion and make ASTs self-documenting.
* **Why Zero Project Prefix & Banned Vague Suffixes**: Prefixing class names with `SmkDoc` duplicates the `SmkDoc.*` namespace and violates Microsoft .NET Framework Design Guidelines. Vague catch-all suffixes (`*Helper`, `*Util`, `*Manager`) hide single responsibility and attract unrelated static methods into God classes.

### 2.2 Proactive Naming Firewalls
```yaml
VerbDeterminismFirewall:
  Invariant: "Repository and service methods must use strict lexicon verbs exclusively."
  ActionOnViolation: "Replace synonyms (Find, Fetch, Add, Modify) with canonical verbs (Get, List, Create, Update)."

VariableTokenFirewall:
  Invariant: "Local execution payloads must conform strictly to layer variable standards."
  ActionOnViolation: "Enforce request (Controller body), command/query (UseCase input), result (UseCase output/return)."

RouteSyntaxFirewall:
  Invariant: "Public REST endpoints must be plural kebab-case under /api/v1/."
  ActionOnViolation: "Convert singular or camelCase routes to /api/v1/{resources}."

TypePrefixAndRoleSuffixFirewall:
  Invariant: "Type names must never carry project prefixes (SmkDoc*) or vague suffixes (*Helper, *Util, *Manager)."
  ActionOnViolation: "Strip SmkDoc prefix and rename vague suffix to exact role (*Builder, *Factory, *Resolver, *Calculator, *Validator, *Policies)."
```

---

## 3. Level 3: Canonical Working Archetypes (Surgical AST Recipes)

### 3.1 Canonical CQRS Layered Action Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-NAME-001]:
* Controller class names are strictly plural: `{Aggregate}sController`.
* Pure 3-step dispatcher using exact tokens: `request` => `command` => `result`.
* Strictly return `ApiResponse<{Aggregate}ResultDto>(result)`.

```csharp
[ApiController]
[Route("api/v1/{resources}")]
public sealed class {Aggregate}sController(
    IUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto> {verb}{Aggregate}UseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<{Aggregate}ResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> {Verb}(
        [FromBody] {Verb}{Aggregate}Request request,
        CancellationToken ct)
    {
        // Step 1: Layer boundary translation (request -> command)
        var command = new {Verb}{Aggregate}Command(request.Name, request.Slug);

        // Step 2: Execution dispatch (command -> result)
        var result = await {verb}{Aggregate}UseCase.ExecuteAsync(command, ct);

        // Step 3: Layer boundary translation (result -> HTTP envelope)
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Aggregate}ResultDto>(result));
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-NAME-001]: Verb Synonym Collisions (`Find` instead of `Get`, `Add` instead of `Create`)
```diff
- public async Task<{Aggregate}?> FindByIdAsync(Guid id) => await repo.FindAsync(id);
- public async Task Add{Aggregate}Async({Aggregate} t) => await repo.AddAsync(t);
+ public async Task<{Aggregate}?> GetByIdAsync(Guid id) => await {aggregate}Repository.GetByIdAsync(id);
+ public async Task Create{Aggregate}Async({Aggregate} entity) => await {aggregate}Repository.AddAsync(entity);
```
*Rationale: Synonym usage increases token entropy and leads to fragmented repository interfaces.*

### [TRAP-NAME-002]: Cryptic Abbreviations Across Parameters and Dependencies
```diff
- public sealed class {Aggregate}Handler(I{Aggregate}Repository repo, IUnitOfWork uow, ICurrentProjectContext ctx) {
-     public async Task<Dto> Handle(Cmd c, CancellationToken token) { ... }
- }
+ public sealed class {Verb}{Aggregate}UseCase(
+     I{Aggregate}Repository {aggregate}Repository,
+     IUnitOfWork unitOfWork,
+     ICurrentProjectContext projectContext) {
+     public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct) { ... }
+ }
```
*Rationale: Abbreviations degrade codebase AST readability and confuse LLM context tracking.*

### [TRAP-NAME-003]: Shadowing `ControllerBase.Response` with Local Variable
```diff
- var response = await useCase.ExecuteAsync(command, ct);
- return Ok(new ApiResponse<TemplateResultDto>(response));
+ var result = await useCase.ExecuteAsync(command, ct);
+ return Ok(new ApiResponse<TemplateResultDto>(result));
```
*Rationale: Naming local variables `response` shadows the built-in HTTP Response property in controllers and violates universal variable standards.*

### [TRAP-NAME-004]: Project/Brand Prefixes (`SmkDoc*`) and Vague Catch-All Suffixes (`*Helper`, `*Manager`)
```diff
- public static class SmkDocProblemDetailsHelper { ... }
- public static class SmkDocPolicies { ... }
- public sealed class SmkDocOperationTransformer : IOpenApiOperationTransformer { ... }
+ public static class ProblemDetailsBuilder { ... }
+ public static class AuthorizationPolicies { ... }
+ public sealed class OpenApiOperationTransformer : IOpenApiOperationTransformer { ... }
```
*Rationale: Prefixing types with `SmkDoc` duplicates the `SmkDoc.*` namespace and violates Microsoft .NET Framework Design Guidelines. Vague suffixes like `*Helper` or `*Manager` obscure single responsibility and invite God-class bloat.*
