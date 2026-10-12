# ANTI-PATTERNS.md — Cognitive Firewall & Pre-Training Bias Interceptor

> **[PURPOSE_SSOT]:** Authoritative Cognitive Firewall for `backend-v2/`. Exclusively intercepts deceptive illusions and deep pre-training gravitational biases where LLMs mistakenly assume code is idiomatic or performant, but triggers runtime crashes, memory fragmentation, or security vulnerabilities in production.
> **[TRIAD_ORTHOGONAL_ROLE]:**
> - [`CODING_CONVENTIONS.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md) ➔ Micro-AST Grammar, Syntax Substitution & Token Naming.
> - [`PATTERNS.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md) ➔ Generative Macro-Factory (Positive Feature-Slice Assembly Blueprints).
> - [`ANTI-PATTERNS.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ANTI-PATTERNS.md) ➔ Cognitive Firewall (Deceptive Traps & Pre-Training Gravitational Biases).

---

<anti_patterns_scope>

<admission_and_update_governance>
## 1. ⚖️ Document Admission & Update Governance (`RULE-DOC-AP`)

An anti-pattern is PERMITTED into this document ONLY if it satisfies all 3 qualification gates:

```mermaid
graph TD
    A["Proposed Rule"] --> B{"Gate 1: Is it merely the mirror inverse of PATTERNS.md?<br/>(e.g., Don't put logic in controllers)"}
    B -- "Yes" --> C["REJECT: Governed by PATTERNS.md"]
    B -- "No" --> D{"Gate 2: Is it a general styling or craftsmanship rule?<br/>(e.g., naming, indentation, magic strings)"}
    D -- "Yes" --> E["REJECT: Governed by CODING_CONVENTIONS.md"]
    D -- "No" --> F{"Gate 3: Does it target a Deceptive Trap or Pre-Training Bias?<br/>(Looks right to LLM but breaks production / internet muscle memory)"}
    F -- "No" --> G["REJECT: Not an LLM Cognitive Hazard"]
    F -- "Yes" --> H["QUALIFIED: Record in Cognitive Trap Matrix"]
```

### Mandatory Invariants:
1. **`[RULE-DOC-AP-001] [GATE_1_MIRROR_REJECTION] [MUST NOT]`**: Do NOT add negative rules that merely restate the absence of a positive pattern in `PATTERNS.md`.
2. **`[RULE-DOC-AP-002] [GATE_2_STYLE_REJECTION] [MUST NOT]`**: Do NOT add styling, naming, or nesting rules (e.g. arrow anti-pattern, underscore prefixes). Those belong strictly in `CODING_CONVENTIONS.md`.
3. **`[RULE-DOC-AP-003] [GATE_3_COGNITIVE_BIAS_QUALIFICATION] [MUST]`**: Every entry MUST intercept either:
   - **Deceptive Production Trap:** Code that appears clean and idiomatic on the surface, but triggers silent memory fragmentation (LOH), DbContext concurrency races, or multi-tenant IDOR leaks.
   - **Pre-Training Gravitational Bias:** Pervasive internet habits (>90% GitHub prevalence) that LLMs instinctively reproduce despite seeing positive blueprints (e.g., God `*Service`, exposing Entities directly, inlining `DateTime.UtcNow`).
4. **`[RULE-DOC-AP-004] [FORMAT_INVARIANT] [MUST]`**: Entries MUST follow the 5-column Cognitive Trap Schema (`Concept & Exemplars`, `LLM Illusion`, `Fatal Reality`, `Permitted Exemptions`, `Minimal Contrastive Diff & SSoT`). Zero multi-line runnable defective code blocks permitted.
5. **`[RULE-DOC-AP-005] [ACTIVE_SSOT_LINK] [MUST]`**: Every entry MUST provide an active, clickable Markdown link redirecting to the exact canonical archetype in `PATTERNS.md` or `CODING_CONVENTIONS.md`.

</admission_and_update_governance>

---

<cognitive_trap_matrix>
## 2. 🛡️ The Cognitive Trap & Pre-Training Bias Interception Matrix

The rules are organized sequentially by runtime evaluation priority: **Host Safety ➔ Data Isolation ➔ Architecture Boundaries ➔ Operational Resilience**.

### Phase 1: Host Process, Memory & Thread Safety (Fatal Production Killers)

| Invariant Concept & Token Exemplars | The LLM Illusion (จุดที่โมเดลมักหลงกล) | Fatal Reality & Blast Radius | Permitted Exemptions (ข้อยกเว้นที่ยอมรับได้) | Minimal Contrastive Diff & SSoT Bypass |
|---|---|---|---|---|
| **RAM Buffering & LOH Allocation**<br/>`new MemoryStream()`, `stream.ToArray()`, `byte[] buffer`, `File.ReadAllBytes` | *"Buffering payload bytes in memory is standard, safe, and simpler to return."* | **LOH Fragmentation & Gen-2 Freeze:** Buffers > 85KB enter Large Object Heap permanently, triggering blocking Gen-2 GCs that freeze request worker threads. | Payloads guaranteed < 85KB (e.g. small barcode generation, in-memory crypto hashing, or unit test mock streams). | `- using var ms = new MemoryStream(); await stream.CopyToAsync(ms); return ms.ToArray();`<br/>`+ await sourceStream.CopyToAsync(response.Body, ct);`<br/>👉 [`Patterns Arch 8`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-8-zero-loh-stream-over-ram-direct-pipeline) |
| **Thread-Hostile Concurrency**<br/>`Task.WhenAll`, `Parallel.ForEachAsync`, `Task.Run` on shared scoped `DbContext` | *"Optimizing performance with parallel asynchronous database queries."* | **DbContext Concurrency Crash:** EF Core instance is thread-hostile; concurrent task operations corrupt change tracker state and throw `InvalidOperationException`. | Sequential `await` calls, or distinct independent context instances injected via `IDbContextFactory<T>`. | `- await Task.WhenAll(repo.GetAsync(a, ct), repo.GetAsync(b, ct));`<br/>`+ var resA = await repo.GetAsync(a, ct); var resB = await repo.GetAsync(b, ct);`<br/>👉 [`Conventions §3.7`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#37-concurrency--dbcontext-thread-safety-rule-conc) |
| **Sync-over-Async Blocking**<br/>`.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | *"Synchronously resolving a task simplifies the calling method signature."* | **CLR ThreadPool Starvation:** Synchronous blocking locks ThreadPool worker threads awaiting I/O completion, causing cascade request queuing and server hang. | Console CLI application root `static async Task Main` or test runners where async pipeline is unavailable. | `- var result = useCase.ExecuteAsync(cmd, ct).Result;`<br/>`+ var result = await useCase.ExecuteAsync(cmd, ct);`<br/>👉 [`Conventions §1`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#1-runtime--compiler-capabilities-ground-truth) |

---

### Phase 2: Security & Tenant Isolation (Data Invariants)

| Invariant Concept & Token Exemplars | The LLM Illusion (จุดที่โมเดลมักหลงกล) | Fatal Reality & Blast Radius | Permitted Exemptions (ข้อยกเว้นที่ยอมรับได้) | Minimal Contrastive Diff & SSoT Bypass |
|---|---|---|---|---|
| **Unscoped Tenant Mutation / IDOR**<br/>`GetByIdAsync(id)`, `Find(id)`, `repo.Update(e)` without `projectId` | *"Standard Repository query retrieving an Aggregate Root by its primary key UUID."* | **Critical IDOR Breach:** Malicious or compromised tenants guess/brute-force UUIDs to read, mutate, or delete foreign project templates. | Platform maintenance jobs, system database seeders, or tenant-agnostic lookup (e.g. `GetUserByEmailAsync`). | `- await repo.GetByIdAsync(id, ct);`<br/>`+ await repo.GetByIdAndProjectIdAsync(id, projectId, ct);`<br/>👉 [`Patterns Arch 6`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-6-action-centric-cqrs-usecase-pattern) |
| **Mass-Assignment Binding**<br/>`[FromBody] Template template`, `action([FromBody] Project project)` | *"ASP.NET Core Model Binder seamlessly deserializes incoming JSON directly into Entity."* | **Mass-Assignment Exploit:** Untrusted callers overwrite sensitive internal entity properties (`Id`, `xmin`, `CreatedBy`, `ProjectId`). | None in HTTP presentation ingress. Domain models must never bind to transport endpoints. | `- public async Task<IActionResult> Create([FromBody] Template entity)`<br/>`+ public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request)`<br/>👉 [`Patterns Arch 1`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-1-thin-controller--api-envelope-pattern) |
| **Catastrophic ReDoS**<br/>`new Regex(pattern)`, `Regex.Match(input)` without non-backtracking | *"Standard regular expression parsing template placeholders or text expressions."* | **ReDoS Vulnerability:** Catastrophic exponential backtracking $O(2^n)$ on user-crafted payloads locks CPU core at 100% permanently. | Pre-compiled static regex using `[GeneratedRegex]` or `RegexOptions.NonBacktracking` with explicit timeout. | `- var regex = new Regex(@"\{\{(.+?)\}\}");`<br/>`+ [GeneratedRegex(@"\{\{(.+?)\}\}", RegexOptions.NonBacktracking)]`<br/>👉 [`Conventions §3.9`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#39-code-level-security--redos-defense-rule-sec) |

---

### Phase 3: Clean Architecture & Boundaries (CQRS & Transaction Invariants)

| Invariant Concept & Token Exemplars | The LLM Illusion (จุดที่โมเดลมักหลงกล) | Fatal Reality & Blast Radius | Permitted Exemptions (ข้อยกเว้นที่ยอมรับได้) | Minimal Contrastive Diff & SSoT Bypass |
|---|---|---|---|---|
| **God Service Anti-Pattern**<br/>`public class *Service` co-locating multiple CRUD methods (`Create`, `Update`, `Delete`) | *"Standard .NET service class grouping related CRUD operations in one place."* | **SRP Violation & Merge Conflicts:** Violates Single Responsibility, balloons dependency injection graphs, prevents clean pipeline behaviors. | Pure stateless mathematical/formatting utilities (e.g. `ThaiDataTransformer`). | `- public class TemplateService : ITemplateService { ... }`<br/>`+ public class CreateTemplateUseCase(ITemplateRepo repo) : IUseCase<CreateTemplateCommand, TemplateResultDto>`<br/>👉 [`Patterns Arch 6`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-6-action-centric-cqrs-usecase-pattern) |
| **Domain Entity Boundary Leakage**<br/>`IUseCase<Command, Template>`, `ApiResponse<Template>`, returning Entity | *"Returning the entity directly is convenient and avoids redundant DTO mapping code."* | **Domain Leakage & Circular Crash:** Leaks domain model across boundary; triggers lazy-load crashes or serialization loops. | Internal Domain Services operating strictly within `SmkDoc.Domain.dll`. | `- return TypedResults.Ok(ApiResponse<Template>.Success(template));`<br/>`+ return TypedResults.Ok(ApiResponse<TemplateResultDto>.Success(dto));`<br/>👉 [`Conventions §2.2`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#22-dto-layering--ownership-matrix-rule-dto) |
| **Broken ACID Transaction Boundaries**<br/>Multiple `CommitAsync()` calls or uncommitted mutations in one UseCase | *"Committing intermediate steps ensures data is saved before subsequent processing."* | **Broken ACID Transaction:** Failures during subsequent steps leave half-persisted orphaned records in database. | Explicit long-running Saga/Outbox workflows managed via distributed background workers. | `- await repo.AddAsync(t, ct); await uow.CommitAsync(ct); await log.AddAsync(l, ct); await uow.CommitAsync(ct);`<br/>`+ await repo.AddAsync(t, ct); await log.AddAsync(l, ct); await uow.CommitAsync(ct);`<br/>👉 [`Patterns Arch 5`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-5-atomic-unit-of-work--transaction-boundary) |
| **Leaking IQueryable & Deferred Execution**<br/>`public IQueryable<T> GetAll()`, exposing expressions from Repo | *"Returning IQueryable gives callers full flexibility to filter or paginate."* | **Leaky Abstraction & N+1 Queries:** Database query executes outside repository boundary; unclosed connections and lazy-loads leak. | Specification Pattern queries executed strictly inside `SmkDoc.Infrastructure`. | `- public IQueryable<Template> GetAll() => dbContext.Templates;`<br/>`+ public async Task<IReadOnlyList<Template>> ListAsync(..., ct) => await dbContext.Templates.ToListAsync(ct);`<br/>👉 [`Conventions §3.3`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#33-infrastructure-layer-rule-infra) |
| **Infrastructure Coupling in Core**<br/>`using SmkDoc.Infrastructure;`, `AppDbContext`, `IFormFile`, `HttpContext` in Application | *"Directly referencing DbContext or ASP.NET types speeds up implementation."* | **Dependency Inversion Violation:** Couples application to relational databases or HTTP hosting, breaking in-memory unit testing. | None. Dependencies flow strictly inward. Outer layers depend on Application interfaces. | `- public record UploadCommand(IFormFile File) : ICommand;`<br/>`+ public record UploadCommand(Stream Content, string FileName, long Length) : ICommand;`<br/>👉 [`Conventions §3.2`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#32-application-layer-rule-app) |

---

### Phase 4: Operational Resilience & Systemic Governance

| Invariant Concept & Token Exemplars | The LLM Illusion (จุดที่โมเดลมักหลงกล) | Fatal Reality & Blast Radius | Permitted Exemptions (ข้อยกเว้นที่ยอมรับได้) | Minimal Contrastive Diff & SSoT Bypass |
|---|---|---|---|---|
| **Hardcoded Engine Branching**<br/>`switch (format)` or `if (type == "Gotenberg")` in render pipeline | *"Straightforward switch-case statement selecting Gotenberg vs OpenXml."* | **Open-Closed Violation (OCP):** Adding new render engines requires modifying core pipeline code, risking regressions across existing engines. | Binary boolean checks internal to a single isolated strategy engine. | `- if (engine == "Gotenberg") return await gotenberg.RenderAsync(...);`<br/>`+ var engine = engines.First(e => e.CanRender(format)); return await engine.RenderAsync(...);`<br/>👉 [`Patterns Arch 9`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-9-polymorphic-render-strategy-engine) |
| **System Clock Direct Inlining**<br/>`DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `DateTime.Now` | *"Fetching the current system time using standard .NET BCL properties."* | **Non-Deterministic Untestable Code:** Business rules involving expiration or audit timestamps cannot be simulated or tested deterministically. | Performance benchmark measurements (`Stopwatch.GetTimestamp()`) where duration only is measured. | `- var entity = Template.Create(..., DateTime.UtcNow);`<br/>`+ var entity = Template.Create(..., timeProvider.GetUtcNow());`<br/>👉 [`Conventions §3.1`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#31-domain-layer-rule-dom) |
| **Ad-hoc Controller Error Handling**<br/>`try-catch` inside Controller Action returning manual `StatusCode` | *"Catching exceptions locally to return custom StatusCodes or problem strings."* | **Bypasses Centralized RFC 9457:** Inconsistent error responses, unindexed telemetry, and potential stack trace leakage. | Global Exception Middleware or Controller Filters explicitly designed for telemetry recording. | `- try { await useCase.ExecuteAsync(cmd, ct); } catch (Exception ex) { return BadRequest(ex.Message); }`<br/>`+ await useCase.ExecuteAsync(cmd, ct); // Let GlobalExceptionHandler map to RFC 9457`<br/>👉 [`Patterns Arch 2`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-2-rfc-9457-global-exception-pipeline-iexceptionhandler-ssot) |
| **Mutating Actions Omitting Idempotency**<br/>`[HttpPost]`, `[HttpPut]`, `[HttpDelete]` without `[Idempotent]` | *"Standard mutation action that does not require additional filtering overhead."* | **Duplicate State Mutations:** Network timeouts and client retries trigger duplicate billing, duplicated rows, or corrupted documents. | Read-only idempotent queries (`GET`, `HEAD`, `OPTIONS`) where repeat requests produce zero side-effects. | `- [HttpPost] public async Task<IActionResult> Create(...)`<br/>`+ [HttpPost, Idempotent] public async Task<IActionResult> Create(...)`<br/>👉 [`Patterns Arch 3`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/PATTERNS.md#archetype-3-ietf-idempotency-key-pipeline-pattern-safe-mutations) |
| **Modifying Legacy v1 Codebase**<br/>Editing files inside root `src/` or obsolete directories | *"Editing existing codebase files found in root src/ directory."* | **Phantom Edits:** Legacy v1 files are disconnected from active runtime architecture and produce zero effect. | Historical reference reading and migration analysis only. | `- src/SmkDocServer/Services/TemplateService.cs`<br/>`+ backend-v2/src/SmkDoc.Application/Features/Templates/Commands/CreateTemplate/`<br/>👉 [`Conventions §2.1`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md#21-boundary-invariants-rule-arch) |

</cognitive_trap_matrix>

</anti_patterns_scope>
