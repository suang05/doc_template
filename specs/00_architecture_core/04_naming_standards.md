# [CORE-004]: 04_naming_standards.md — Universal Lexicon, Canonical Verbs, Suffix Taxonomy & Glossary

<context>
Category: 00_architecture_core
Domain: Universal Lexicon, Canonical Verbs, Role-Based Suffix Taxonomy & Ubiquitous Language Glossary
SoleResponsibilities:
  - Three-scope verb dictionary (UseCase/Command/Query, Repository/ReadModel, Domain methods)
  - Closed role-based suffix taxonomy across all layers and framework-abstraction precedence rule
  - Anti-abbreviation rules, approved short-token allow-list and acronym casing standards
  - Wire, REST route (including plural table, zone exceptions and `:verb` custom methods), PostgreSQL constraint and TypeScript naming standards
  - Thai ↔ English Ubiquitous Language Glossary
Out of scope (owned elsewhere): code cadences (02), wire status mapping and envelope schemas (06).
Target: Identifiers, routes, schemas and files in `backend-v2/` and `frontend-v2/`
Platform baseline: declared in `Directory.Build.props` (ADR-01).
Signal: [ARCH_EVOLUTION_NAMING]
</context>

---

## 1. Level 1: Universal Lexicon & Identifier Invariants Matrix

### 1.1 Authoritative Lexical Verb Dictionary
One verb per intent, partitioned into three scopes. The same intent never has two names; distinct domain intents never share one name.

**Scope 1: UseCase / Command / Query names (business intent)**

| Functional Intent | Verb | Banned Synonyms | Canonical Identifier |
|---|---|---|---|
| **Single read** | `Get` | Find, Fetch, Retrieve, Read | `Get{Aggregate}Query` (throws `NotFoundException` when missing) |
| **Collection read** | `List` | GetAll, FetchAll, Filter, Browse | `List{Aggregates}Query` (filter by parameters) |
| **Free-text search** | `Search` | Find, Query, Lookup | `Search{Aggregates}Query` (ranked / full-text) |
| **Create** | `Create` | Add, Insert, New | `Create{Aggregate}Command` |
| **Register / Provision** | `Register` (user sign-up), `Provision` (tenant/project) | Signup, Onboard | `ProvisionProjectCommand` |
| **Edit (simple attributes)** | `Update` | Modify, Edit, Change, Mutate | `Update{Aggregate}Command`; prefer an intention-revealing domain verb (`Rename`, `ChangeCategory`) |
| **Reversible disable / enable** | `Deactivate` / `Activate` | Disable, Enable, Suspend | `Deactivate{Aggregate}Command`, `Activate{Aggregate}Command` |
| **Lifecycle archive / restore** | `Archive` / `Restore` | Shelve, Unarchive | `Archive{Aggregate}Command`, `Restore{Aggregate}Command` |
| **Irreversible removal** | `Delete` (user-initiated), `Purge` (retention job) | Remove, Drop, Destroy | `Delete{Aggregate}Command`, `PurgeExpiredDocumentsCommand` |
| **Child append / remove** | `Add{Child}` / `Remove{Child}` | Attach, Append, Push, Detach | `AddFieldMappingCommand`, `RemoveFieldMappingCommand` |
| **Role binding** | `Assign` / `Unassign` | Grant, Bind, Revoke (for roles) | `AssignRoleCommand`, `UnassignRoleCommand` |
| **Secret / key lifecycle** | `Revoke` (manual), `Expire` (time-based, system), `Rotate` | Kill, Invalidate | `RevokeApiKeyCommand`, `RotateApiKeyCommand` |
| **Versioning** | `Save` (draft), `Commit` (draft to version), `Publish`, `Rollback` | Deploy, Release, Promote, Revert | `PublishTemplateVersionCommand`, `RollbackTemplateVersionCommand` |
| **Authority transfer** | `Transfer` | Reassign, Handover | `TransferProjectOwnerCommand` |
| **Document synthesis** | `Generate` (UseCase), `Preview`, `Download` | Create (for documents), Stream, Export | `GenerateDocumentCommand`, `PreviewDocumentQuery`, `DownloadDocumentQuery` |
| **Async jobs (Pipeline C)** | `Enqueue`, `Cancel`, `Retry` | Submit, Queue, Abort, Rerun | `EnqueueDocumentGenerationCommand`, `CancelDocumentJobCommand`, `RetryDocumentJobCommand` |
| **Parsing / validation** | `Parse`, `Scan`, `Validate`, `Verify` | Check, Inspect, Test | `ScanTemplatePlaceholdersQuery`, `VerifyDataConnectionCommand` |
| **External extraction** | `Extract`, `Execute` (run a stored query) | Pull, Fetch | `ExecuteDatasetQuery` |
| **Authentication** | `Login`, `Refresh`, `Switch` (project) | Signin, Renew | `LoginUserCommand`, `SwitchProjectCommand` |

**Scope 2: Repository and ReadModel port methods (collection & query semantics)**

| Intent | Method Pattern | Rule |
|---|---|---|
| Single entity / projection (nullable) | `FindBy{Key}Async` returning `T?` | Repositories (`I{Aggregate}Repository`) and read models (`I{Aggregate}ReadModel`) never throw `NotFoundException` (which is an Application exception); they return `T?` and the UseCase throws `NotFoundException.For<{Aggregate}>(id)`. |
| Collection | `ListBy{Criteria}Async` | Returns `IReadOnlyList<T>` or a paged result. |
| Existence / count | `{Criteria}ExistsAsync`, `CountBy{Criteria}Async` | Example: `SlugExistsAsync(ProjectId projectId, TemplateSlug slug, CancellationToken ct)`. |
| Add / remove (Repository only) | `Add`, `Remove` | `Add` and `Remove` are synchronous (`void`) because entities use client-generated `Guid.CreateVersion7()` IDs and EF Core only tracks them in memory (`CommitAsync` owns async I/O). A repository method named `Create{Aggregate}Async` that only calls `Add` is a forbidden pass-through. |

**Scope 3: Domain methods & events**
* Aggregate mutation methods use intention-revealing verbs of the ubiquitous language (`Rename`, `Publish`, `Archive`, `Restore`, `Deactivate`, `Activate`, `Claim`, `Complete`, `Fail`). `Update` is allowed only for trivial attribute bundles.
* Domain events use **`{Aggregate}{PastTenseVerb}DomainEvent`** (`TemplateCreatedDomainEvent`, `TemplateRenamedDomainEvent`, `TemplatePublishedDomainEvent`, `DocumentJobSucceededDomainEvent`). Never concatenate raw `-ed` onto verbs ending in `e` (`Createed` is prohibited).

#### 1.1.1 Rendering & Connectivity Vocabulary
* **`Generate`** names the application workflow (`GenerateDocumentUseCase`); **`Render`** names the low-level engine port (`IRenderEngine.RenderStreamAsync`) and nothing else.
* **`Download`** names the retrieval of a stored artifact (`DownloadDocumentQuery`); `Stream` is an I/O mechanism and is never used as a UseCase or route verb.
* **`Verify`** replaces `Test` for connectivity checks (`VerifyDataConnectionCommand`, route `connectivity-checks`) to avoid clashing with unit-test naming (`*Tests`).
* **`Scan`** extracts placeholders from a template; **`Parse`** converts raw source into a structured AST or manifest.

---

### 1.2 Standard Recurring Payload & Parameter Tokens
| Layer / Role | Mandatory Token | Prohibited Synonyms | Scope & Guidance |
|---|---|---|---|
| **Controller HTTP body** | `request` | `body`, `payload`, `data`, `dto`, `req` | `[FromBody] {Verb}{Aggregate}Request request` |
| **Application command** | `command` | `request`, `cmd`, `req`, `c`, `payload` | `ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct)` |
| **Application query** | `query` | `request`, `q`, `qry`, `req`, `payload` | `ExecuteAsync(Get{Aggregate}Query query, CancellationToken ct)` |
| **Controller return local** | `result` | `response`, `res`, `output`, `dto` | Required in the 3-step controller dispatcher (`var result = await useCase.ExecuteAsync(...)`). Inside UseCases and tests, descriptive domain nouns (`template`, `document`, `job`, `savedTemplate`) are preferred when multiple values are in scope. |
| **Temporal parameter** | `now` | `currentTime`, `utc`, `time`, `dt` | `{Aggregate}.Create(..., DateTimeOffset now)` |
| **Concurrency parameter** | `expectedVersion` | `etag`, `ifMatchHeader`, `token` | `Rename{Aggregate}Command(..., long ExpectedVersion)` |
| **Cancellation token** | `ct` | `cancellationToken`, `cancelToken`, `token` | `CancellationToken ct` (required parameter, no `= default`) |
| **Generic type parameters**| `TRequest`, `TResponse`, `TId`, `TEntity` | `TReq`, `TRes`, `K`, `V` | `IUseCase<in TRequest, TResponse>`, `AggregateRoot<TId>` |

---

### 1.3 Anti-Abbreviation Policy & Acronym Casing Standards
Clipped words are prohibited. Only the industry-standard abbreviations in the allow-list below are permitted.

| Category | Permitted Tokens | Prohibited Clipped Tokens |
|---|---|---|
| **Allowed short identifiers** | `id`, `ct`, `dto`, `uri`, `url`, `ip`, `sql`, `api`, `jwt`, `hmac`, `sha256`, `sut` (unit tests), `ex` or `exception` (in `catch` blocks) | `repo`, `rep`, `uow`, `ctx`, `svc`, `cfg`, `req`, `res`, `cmd`, `qry`, `mgr`, `util`, `val`, `doc`, `conn`, single-letter locals (`t`, `v`, `d`, `c`, `s`, `f`) |
| **Acronym casing (C# & TS)** | 3+ letter acronyms use `PascalCase` (`TemplateId`, `TemplateResultDto`, `HtmlContent`, `JsonSchema`, `SqlDriver`, `ApiKey`, `Uri`, `Url`). 2-letter acronyms follow BCL convention (`Io`, `DbContext`, `IPAddress`). | `TemplateID`, `TemplateResultDTO`, `HTMLContent`, `JSONSchema`, `SQLDriver`, `APIKey` |

* **Private fields** (when explicit readonly fields are used instead of primary constructor parameters): `_camelCase` (`_pepperBytes`).
* **Booleans**: prefix with `Is`, `Has`, `Can`, or `Should` (`IsActive`, `IsResolved`, `CanRetry`, `CancelRequested`).
* **Test methods**: `MethodUnderTest_WhenScenario_ExpectedOutcome` (`ExecuteAsync_WhenSlugExists_ThrowsConflictAndDoesNotCommit`).

---

### 1.4 Wire, Route, Database & TypeScript Naming Standards

#### 1.4.1 REST Route Grammar & Pluralization Table
* **Standard resource routes**: plural `kebab-case` nouns under `/api/v1/<resources>`.
* **Documented non-plural / zone exceptions**:
  * Public/Auth zone: `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/users/me`
  * Infrastructure probes: `/health/live`, `/health/ready`, `/openapi/v1.json`
  * Control Plane prefix: `/api/v1/management/<resources>`
* **State transitions & custom actions (Google AIP-136)**:
  * Irreversible deletion maps to `DELETE /api/v1/<resources>/{id}`.
  * Lifecycle transitions and non-CRUD verbs use custom methods `POST /api/v1/<resources>/{id}:<verb>` (`:publish`, `:deactivate`, `:activate`, `:archive`, `:restore`, `:rollback`, `:cancel`, `:retry`) or noun sub-resources (`POST /api/v1/data-connections/{id}/connectivity-checks`).
* **Canonical Pluralization Table (no naive `+s`):**

| Aggregate / Resource | Controller Name | Route Segment |
|---|---|---|
| `Company` | `CompaniesController` | `/api/v1/management/companies` |
| `Project` | `ProjectsController` | `/api/v1/management/projects` |
| `Font` (global) | `GlobalFontsController` | `/api/v1/management/fonts` |
| `Template` | `TemplatesController` | `/api/v1/templates` |
| `Document` | `DocumentsController` | `/api/v1/documents` |
| `DocumentJob` | `DocumentJobsController` | `/api/v1/document-jobs` |
| `DataConnection` | `DataConnectionsController` | `/api/v1/data-connections` |
| `Dataset` | `DatasetsController` | `/api/v1/datasets` |
| `ApiKey` | `ApiKeysController` | `/api/v1/api-keys` |

#### 1.4.2 Wire, Database & TypeScript Naming Matrix
| Surface | Standard | Examples | Prohibited |
|---|---|---|---|
| **Zero project prefix** | Never prefix C# or TypeScript types with project/brand names (`SmkDoc*`, `Sammakorn*`). Namespace (`SmkDoc.*`), package IDs and config sections provide scope. | `ProblemDetailsFactory`, `AuthorizationPolicies` | `SmkDocProblemDetailsHelper`, `SmkDocPolicies` |
| **JSON properties & query params** | `camelCase` | `projectId`, `createdAt`, `nextCursor`, `?includeTotal=true` | `Project_Id`, `CreatedAt`, `page_size` |
| **JSON enum values** | `camelCase` or lowercase strings | `"queued"`, `"running"`, `"succeeded"`, `"pdf"` | Integer enum codes (`0`, `1`, `2`) |
| **HTTP headers** | `Train-Case` without `X-` prefix (RFC 6648) | `Project-Id`, `Idempotency-Key`, `If-Match`, `Document-Id`, `Retry-After` | `x-project-id`, `X-Project-Id` |
| **Error codes** | `SCREAMING_SNAKE_CASE` | `VALIDATION_FAILED`, `DUPLICATE_SLUG`, `PRECONDITION_FAILED` | `validationError`, `ERR_001` |
| **PostgreSQL tables & columns**| Plural `snake_case` tables; singular `snake_case` columns (`UseSnakeCaseNamingConvention`). Booleans start with `is_`/`has_`/`can_`; timestamps end with `_at`/`_until`. | `template_versions`, `document_jobs`, `is_active`, `created_at`, `locked_until` | `TemplateVersion`, `IsActive`, `createDate` |
| **PostgreSQL constraints & indexes**| Deterministic prefixes | `pk_templates`, `fk_templates_projects`, `uq_templates_project_id_slug`, `ix_document_jobs_status_available_at` | Auto-generated random constraint names |
| **TypeScript files & identifiers**| Components: `PascalCase.tsx`; hooks: `use-kebab-case.ts` (`useCamelCase`); utils/api: `kebab-case.ts`; Zod schemas: `kebab-case.schema.ts`; Next.js App Router files: `page.tsx`, `layout.tsx`, `loading.tsx`, `error.tsx`, `route.ts`. Types/interfaces use `PascalCase` with **no `I` prefix**. | `TemplateEditorFeature.tsx`, `use-live-preview.ts`, `template.schema.ts`, `interface TemplateResultDto` | `ITemplateResultDto`, `useLivePreview.ts`, `Template.schema.ts` |

---

### 1.5 Closed Architectural Role-Based Suffix Taxonomy
Every non-entity class uses the suffix of its architectural role from the closed table below.
* **Precedence rule:** When a class implements a framework abstraction whose suffix is defined by .NET or ASP.NET Core (`IExceptionHandler`, `AuthenticationHandler<T>`, `AuthorizationHandler<T>`, `DelegatingHandler`, `Attribute`, `Exception`), **the framework suffix wins**.
* **`*ResultDto` vs `*Dto` rule:** The root output record returned by a UseCase is named `*ResultDto` (`TemplateResultDto`, `ConnectivityCheckResultDto`). Nested child records or collection item projections are named `*Dto` (`TemplateVersionDto`, `DocumentJobItemDto`).
* **Banned catch-all suffixes:** `*Helper`, `*Util`, `*Utils`, `*Manager`, `*Service` (in Application/Domain), `*Info`, `*Data` (except `BoundDocumentData`).

| Architectural Responsibility | Layer | Mandatory Suffix | Canonical Example |
|---|---|---|---|
| **Workflow orchestrator** | Application | `*UseCase` | `CreateTemplateUseCase` |
| **UseCase input contracts** | Application | `*Command`, `*Query` | `CreateTemplateCommand`, `GetTemplateByIdQuery` |
| **UseCase root output / nested DTO** | Application | `*ResultDto` (root), `*Dto` (nested/item) | `TemplateResultDto`, `TemplateVersionDto` |
| **FluentValidation validator** | Application | `*CommandValidator`, `*QueryValidator` | `CreateTemplateCommandValidator` |
| **UseCase cross-cutting wrapper** | Application | `*Decorator` | `ValidationDecorator`, `IdempotencyDecorator` |
| **Aggregate persistence port / impl**| Domain / Infra | `*Repository` | `ITemplateRepository`, `TemplateRepository` |
| **Query projection port / impl** | Application / Infra| `*ReadModel` | `ITemplateReadModel`, `TemplateReadModel` |
| **External service port / impl** | Application / Infra| `*Port` / `*Adapter` | `IGotenbergPort`, `GotenbergAdapter` |
| **Domain / synthesis engine** | App / Infra | `*Engine`, `*Renderer`, `*Processor`, `*Executor`, `*Evaluator`, `*Formatter`, `*Generator` | `DataBindingEngine`, `GotenbergHtmlRenderer`, `HtmlLayoutProcessor`, `DatasetBatchExecutor`, `ThaiDataFormatter`, `DocumentDrawingMlIdGenerator` |
| **Caching & bounded stream** | Infra | `*Cache`, `*Stream` | `HybridTemplateDraftCache`, `HybridSpoolStream`, `ResponseDisposingStream` |
| **Object / contract construction** | All | `*Factory`, `*Builder` (stateful/fluent only) | `RenderEngineFactory`, `ProblemDetailsFactory`, `DatasetCacheKeyBuilder` |
| **Calculation / resolution / parsing**| All | `*Calculator`, `*Resolver`, `*Parser`, `*Verifier`, `*Validator` | `RequestFingerprintCalculator`, `RateLimitPartitionKeyResolver`, `ConnectionHostValidator`, `ApiKeyVerifier` |
| **HTTP pipeline components** | Api | `*Middleware`, `*Filter`, `*Binder`, `*ExceptionHandler`, `*AuthenticationHandler` | `TenantResolutionMiddleware`, `IfMatchVersionBinder`, `GlobalExceptionHandler` |
| **EF Core interceptors** | Infrastructure | `*SaveChangesInterceptor` | `TenantSecuritySaveChangesInterceptor` |
| **Background host loops** | Worker / Infra | `*Worker` | `DocumentJobWorker`, `OutboxRelayWorker`, `DocumentRetentionWorker` |
| **Strongly-typed configuration** | All | `*Options` | `EgressPolicyOptions`, `RateLimitOptions` |
| **Static protocol / policy tokens**| All | `*Constants`, `*Policies` | `AuthorizationPolicies`, `RoleConstants`, `ErrorCodes` |
| **Domain events & exceptions** | Domain / App | `*DomainEvent`, `*Exception` | `TemplatePublishedDomainEvent`, `PreconditionFailedException` |
| **DI & mapping extensions** | All | `*Extensions` | `AuthoringModuleExtensions`, `TemplateMappingExtensions` |

---

### 1.6 Pipeline C and TenantManagement Vocabulary
| Concept | Canonical Identifier | Notes |
|---|---|---|
| Async batch aggregate | `DocumentJob`, `DocumentJobStatus` (`Queued`, `Running`, `Succeeded`, `Failed`, `Canceled`) | Wire status values are lowercase strings (`"queued"`, ...). |
| Job UseCases | `EnqueueDocumentGeneration`, `GetDocumentJob`, `ListDocumentJobs`, `CancelDocumentJob`, `RetryDocumentJob` | Verbs from §1.1 Scope 1. |
| Background workers | `DocumentJobWorker`, `OutboxRelayWorker`, `IdempotencyCleanupWorker`, `DocumentRetentionWorker` | Hosted `BackgroundService` loops in `SmkDoc.Worker`. |
| Queue port | `IDocumentJobQueue` | Backed by PostgreSQL `FOR UPDATE SKIP LOCKED` (ADR-02). |
| Tenant lifecycle context | `TenantManagement` (`Company`, `Project`, `ProjectQuota`, `Font`) | Control Plane routes under `/api/v1/management/**`. |
| Worker tenant scope | `ITenantScope.Begin(ProjectId projectId)` | Sets ambient tenant explicitly for background execution. |

---

### 1.7 Ubiquitous Language Glossary (Thai ↔ English)
All code identifiers, database objects, comments and API contracts are written in English using the canonical terms below.

| Canonical English Term | Thai Business Meaning | Architectural Definition & Scope |
|---|---|---|
| **`Company`** | บริษัท / นิติบุคคลลูกค้า | Top-level legal organization owning one or more `Project` containers (`TenantManagement`). |
| **`Project`** | โครงการ / พื้นที่ทำงานของผู้เช่า (Tenant) | The unit of tenant isolation (`ProjectId`, `IMustHaveProject`). Every Data Plane request is scoped to one `Project`. (`Tenant` is an architectural concept, never a class name.) |
| **`ProjectQuota`** | โควตาการใช้งานของโครงการ | Rate, concurrency, batch-size and storage limits allocated to a `Project` by SuperAdmin. |
| **`BreakGlassGrant`** | สิทธิ์เข้าถึงฉุกเฉินชั่วคราว | Time-boxed, audited, tenant-approved elevation allowing a platform operator to troubleshoot a specific `Project`. |
| **`Template`** | แม่แบบเอกสาร | Authoring aggregate root identified by `(ProjectId, TemplateSlug)` (`Authoring`). |
| **`TemplateVersion`** | รุ่นของแม่แบบเอกสาร | Versioned immutable snapshot of a `Template` (`Draft`, `Published`, `Archived`) containing frozen `MappingsSnapshot`, schema and font references. |
| **`FieldMapping`** | การผูกฟิลด์ข้อมูล | Rule mapping a template placeholder to a JSON payload path or external `Dataset` column. |
| **`DataConnection`** | การเชื่อมต่อฐานข้อมูลภายนอก | Tenant-owned encrypted connection configuration to an external customer database (`Integration`). |
| **`Dataset`** | ชุดคำสั่งดึงข้อมูล | Parameterized read-only query executed against a `DataConnection` to enrich document data. |
| **`Document`** | เอกสารที่สร้างสำเร็จ | Persisted output artifact (PDF, DOCX, XLSX) stored in MinIO with SHA-256 checksum (`Rendering`). |
| **`DocumentJob`** | งานสร้างเอกสารแบบกลุ่ม (Async Batch) | Asynchronous job processing one or more document generation items with retry and lease management (Pipeline C). |
| **`GenerationLog`** | บันทึกประวัติการสร้างเอกสาร | Durable audit record of a document generation execution. |

---

## 2. Level 2: Engineering Rationales & Naming Firewalls

### 2.1 Engineering Rationales
* **Why One Verb per Intent Across Scopes:** When `Get`, `Find` and `Fetch` are mixed arbitrarily, callers cannot tell whether a method returns `null`, throws `NotFoundException`, or hits an external network, and text searches miss usages. Separating UseCase verbs (`Get` throws) from Repository/ReadModel verbs (`FindBy…Async` returns `T?`) makes nullability contracts self-evident.
* **Why `result` in Controllers:** Purely a house consistency rule for the 3-step controller dispatcher (`request` → `command` → `result`). (C# is case-sensitive, so `var response` does not shadow `ControllerBase.Response`; the reason is uniformity, not compiler shadowing.)
* **Why Zero Project Prefix & Closed Role Suffixes:** Prefixing types with `SmkDoc` duplicates the `SmkDoc.*` namespace and violates Microsoft Framework Design Guidelines. Vague suffixes (`*Helper`, `*Util`, `*Manager`, `*Service`) hide responsibilities and attract unrelated methods into God classes.

### 2.2 Proactive Naming Firewalls
```yaml
VerbDeterminismFirewall:
  Invariant: "UseCases, repositories, read models and domain methods use verbs from section 1.1 exclusively."
  Enforcement: "NetArchTest naming rules + code review."

SuffixTaxonomyFirewall:
  Invariant: "Non-entity classes end with a role suffix from section 1.5; *Helper, *Util, *Manager and *Service (in Application/Domain) are prohibited."
  Enforcement: "NetArchTest rule forbidding *Helper, *Util, *Utils, *Manager, SmkDoc* type names."

CasingAndAbbreviationFirewall:
  Invariant: "PascalCase types/methods, camelCase parameters/JSON, snake_case DB objects, kebab-case routes; only approved abbreviations in section 1.3."
  Enforcement: ".editorconfig dotnet_naming_rule (IDE1006:error), @typescript-eslint/naming-convention."

RouteGrammarFirewall:
  Invariant: "Public REST routes follow the pluralization table and custom-method grammar (:verb) in section 1.4.1."
  Enforcement: "OpenAPI contract lint (Redocly) in CI."
```

---

## 3. Level 3: Canonical Naming Reference

The canonical C# listings for Controller (`request` → `command` → `result`), UseCase (`Create{Aggregate}UseCase`, `Rename{Aggregate}UseCase`, `Get{Aggregate}UseCase`), Entity (`Rename`), Value Object (`{Aggregate}Slug`) and Adapter (`{ExternalService}Adapter`) live in a single SSoT location: **[02_coding_conventions.md §3](02_coding_conventions.md)**.

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-NAME-001]: Verb Synonym Collisions and Nullability Ambiguity
```diff
- public sealed record FetchTemplateQuery(Guid Id);                         // synonym of Get
- Task<Template?> GetByIdAsync(TemplateId id, CancellationToken ct);        // repository returning null must be FindBy...
- Task CreateTemplateAsync(Template entity, CancellationToken ct);          // pass-through synonym of Add
+ public sealed record GetTemplateByIdQuery(TemplateId Id);
+ Task<Template?> FindByIdAsync(TemplateId id, CancellationToken ct);
+ void Add(Template entity);
```
*Rationale: UseCase queries use `Get` (throws when missing); Repository and ReadModel single-item lookups that return `T?` use `FindBy…Async`; Repository collection additions use synchronous `Add` (avoiding async-theater `Task.CompletedTask`).*

### [TRAP-NAME-002]: Clipped Abbreviations Across Parameters and Dependencies
```diff
- public sealed class CreateTemplateHandler(ITemplateRepository repo, IUnitOfWork uow, ICurrentProjectContext ctx) {
-     public async Task<Dto> Handle(Cmd c, CancellationToken token) { ... }
- }
+ public sealed class CreateTemplateUseCase(
+     ITemplateRepository templateRepository,
+     IUnitOfWork unitOfWork,
+     ICurrentProjectContext projectContext) : IUseCase<CreateTemplateCommand, TemplateResultDto> {
+     public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) { ... }
+ }
```
*Rationale: Clipped abbreviations (`repo`, `uow`, `ctx`, `c`) obscure types and violate §1.2–1.3.*

### [TRAP-NAME-003]: Inconsistent Local Variable Names in Controller Dispatchers
```diff
- var res = await createTemplateUseCase.ExecuteAsync(cmd, ct);
- return Ok(new ApiResponse<TemplateResultDto>(res));
+ var command = new CreateTemplateCommand(request.Name, request.Slug, request.Category);
+ var result = await createTemplateUseCase.ExecuteAsync(command, ct);
+ return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<TemplateResultDto>(result));
```
*Rationale: Controllers follow the uniform 3-step token cadence (`request` → `command` → `result`).*

### [TRAP-NAME-004]: Project Prefixes (`SmkDoc*`) and Vague Catch-All Suffixes (`*Helper`, `*Manager`)
```diff
- public static class SmkDocProblemDetailsHelper { ... }
- public static class SmkDocPolicies { ... }
- public sealed class TemplateManager { ... }
+ public static class ProblemDetailsFactory { ... }
+ public static class AuthorizationPolicies { ... }
+ public sealed class CreateTemplateUseCase { ... }
```
*Rationale: `SmkDoc*` duplicates the namespace, while `*Helper` and `*Manager` conceal responsibilities.*

### [TRAP-NAME-005]: Overloading `Deactivate` for Irreversible Deletion or `Archive`
```diff
- // Calling hard PDPA deletion "DeactivateDocument" or lifecycle archiving "DeactivateTemplateVersion"
- public sealed record DeactivateDocumentCommand(Guid Id);
+ public sealed record DeleteDocumentCommand(Guid Id);          // user-initiated irreversible delete
+ public sealed record PurgeExpiredDocumentsCommand();          // retention worker irreversible purge
+ public sealed record ArchiveTemplateVersionCommand(Guid Id);  // lifecycle archive
```
*Rationale: Soft reversible disable (`Deactivate`/`Activate`), lifecycle archiving (`Archive`/`Restore`) and irreversible removal (`Delete`/`Purge`) have different business, audit and PDPA compliance effects and must never share a verb.*
