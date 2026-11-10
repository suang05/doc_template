# [CORE-005]: 05_anti_pattern_firewall.md — Fatal Traps, Architectural Interceptors & Diagnostics

<context>
Category: 00_architecture_core
Domain: System-Wide Anti-Pattern Traps & Architectural Interceptors
SoleResponsibilities:
  - Registry of 20 compiler-grade fatal architectural traps (`[TRAP-MEM-001]` to `[TRAP-PREVIEW-020]`)
  - Unified micro-diffs (`-` bad / `+` good) illustrating forbidden implementations and remediations
  - Pre-commit architectural violation checks across all layers
Target: Full Stack (`backend-v2/` and `frontend-v2/`)
Signal: [ARCH_EVOLUTION_FIREWALL]
</context>

---

## 1. Level 1: Fatal Traps & Affirmative Invariants Matrix

| Trap ID | Architectural Category | Causal Failure Mode & Production Risk | Affirmative Canonical Invariant (Remedy) | Severity |
|---|---|---|---|---|
| **[TRAP-LOH-001]** | Memory Safety | In-memory buffering $\ge 85\text{KB}$ (`byte[]`, `ToArray()`) triggers uncompacted Gen 2 GC pauses. | Binary outputs must stream directly to `Response.Body` with bounded buffers $\le 81,920$ bytes. | FATAL |
| **[TRAP-EF-002]** | Concurrency | Concurrently calling methods on shared scoped `DbContext` corrupts EF tracker state. | Inject `IDbContextFactory<AppDbContext>` for per-task isolated execution in parallel workers. | FATAL |
| **[TRAP-ASYNC-003]** | Thread Safety | Synchronous blocking (`.Result`, `.Wait()`) causes thread-pool worker thread starvation. | Execute 100% pure `async`/`await` pipeline forwarding `CancellationToken ct`. | FATAL |
| **[TRAP-SSRF-004]** | Security | Unsanitized dynamic user HTML allows Chromium to read local files (`file:///etc/passwd`). | Sanitize HTML via whitelist and run Chromium with strict isolation flags. | CRITICAL |
| **[TRAP-AUTH-005]** | Security | Early return on non-existent user accounts leaks registered emails via side-channel latency. | Verify credentials against `DummyPasswordHash` to guarantee constant-time execution. | CRITICAL |
| **[TRAP-LEAK-006]** | Architecture | Exposing Domain Entities leaks internal schema, navigation proxies, and domain invariants. | Map domain state strictly to immutable sealed positional `*ResultDto` records. | HIGH |
| **[TRAP-TIME-007]** | Testability | Direct queries to `DateTime.UtcNow` create non-deterministic test failures and temporal drift. | Inject `TimeProvider` or pass explicit parameterized `DateTimeOffset now`. | HIGH |
| **[TRAP-VOID-008]** | Reliability | Swallowing exceptions with empty catch blocks hides production crashes and data corruption. | Allow exceptions to bubble to RFC 9457 handler or rethrow strongly-typed `DomainException`. | HIGH |
| **[TRAP-AUDIT-009]** | Storage / OLTP | Storing unbounded JSON payloads $>16\text{KB}$ directly in PostgreSQL table rows bloats WAL. | Offload payloads $>16\text{KB}$ to MinIO S3 and store immutable SHA-256 hash in DB. | HIGH |
| **[TRAP-EVENT-010]** | Consistency | Pre-commit event dispatching creates phantom side-effects if the database transaction aborts. | Execute transaction commit via `unitOfWork.CommitAsync(ct)` prior to event dispatch. | FATAL |
| **[TRAP-SQL-011]** | Security / SSRF | Unchecked database connections target internal Docker network or cloud metadata services. | Pre-flight DNS validation via `ConnectionHostValidator`; enforce `READ ONLY` transactions. | CRITICAL |
| **[TRAP-SLICE-012]** | Architecture | Bundling multiple primary types in one file hides contracts and degrades PR reviewability. | Enforce 1-Primary-Type-Per-File (`{TypeName}.cs`) co-located within feature slices. | HIGH |
| **[TRAP-SQL-013]** | Performance | N+1 sequential database lookups exhaust connection pool and spike query latency. | Batch data fetching via `.Include()` and throttle parallel tasks with `SemaphoreSlim(4)`. | HIGH |
| **[TRAP-POD-014]** | Statelessness | In-memory singleton state breaks horizontal pod scaling and allows rate-limit bypasses. | Enforce distributed state via Redis Lua scripts and declarative `[EnableRateLimiting]`. | CRITICAL |
| **[TRAP-ARCH-015]** | Simplicity | Generic CRUD base classes create tight coupling and obscure business invariants. | Enforce flat composition and single-responsibility `IUseCase<TReq, TRes>` slices. | HIGH |
| **[TRAP-SCALE-016]** | Scalability | In-memory dictionaries in singletons cause state divergence across Kubernetes replicas. | State must reside in external distributed backends or immutable stateless services. | FATAL |
| **[TRAP-SSOT-017]** | Architecture | Querying mutable relational tables during versioned runtime execution causes published versions to drift. | Runtime generation must strictly read immutable `TemplateVersion.MappingsSnapshot` manifest. | CRITICAL |
| **[TRAP-WRAPPER-018]**| Simplicity | Artificial pass-through wrappers duplicating identical DTOs create cognitive bloat and GC allocations. | Pipeline must consume domain engines (`IDataBindingEngine`) directly without wrapper indirection. | HIGH |
| **[TRAP-DI-019]** | Modularity | Monolithic DI composition roots accumulating hundreds of lines across multiple subsystems hide dependencies. | Decompose DI into single-responsibility Feature Installers under `SmkDoc.Infrastructure/Extensions/`. | HIGH |
| **[TRAP-PREVIEW-020]**| Architecture | Parallel duplicate preview use cases cause rendering logic divergence and dual-pipeline maintenance. | Route all preview variants exclusively through `PreviewDocumentUseCase` SSoT. | HIGH |

---

## 2. Level 2: Interceptor Rationales & Cognitive Firewalls

### 2.1 Engineering Rationale & Causal Decisions
* **Why 81,920-Byte LOH Threshold**: Any memory allocation $\ge 85,000$ bytes lands on the Large Object Heap (LOH), triggering expensive Gen 2 garbage collection sweeps and uncompacted memory fragmentation under high concurrent load.
* **Why `IDbContextFactory` for Parallel Tasks**: EF Core `DbContext` instances are fundamentally non-thread-safe. Concurrently calling methods on the same scoped instance causes race conditions, corrupted tracker state, and `InvalidOperationException`.
* **Why Constant-Time Dummy Hash Verification**: Early returns when an email does not exist create a timing disparity (<2ms vs ~100ms password hash verification), allowing attackers to enumerate registered email addresses via side-channel latency attacks.
* **Why Post-Commit Domain Event Dispatch**: Dispatching domain events before `SaveChangesAsync` commits creates "phantom side-effects" if the database transaction subsequently fails or aborts.

### 2.2 Proactive Firewall Interceptors
```yaml
LohFirewall:
  Trigger: MemoryStream.ToArray() or buffer >= 85KB
  Action: Stream through bounded 81,920 buffer directly to response >> Ref: [RULE-PERF-001]

DbContextConcurrencyFirewall:
  Trigger: Parallel tasks capturing scoped DbContext
  Action: Inject IDbContextFactory<T> and allocate isolated context per task

AsyncPurityFirewall:
  Trigger: Synchronous blocking (.Result, .Wait())
  Action: Enforce 100% async/await forwarding CancellationToken ct

DomainLeakageFirewall:
  Trigger: Domain Entity crossing into Presentation controller
  Action: Map strictly to immutable sealed record ResultDto

ClockIntegrityFirewall:
  Trigger: Direct ambient clock access (DateTime.UtcNow)
  Action: Inject TimeProvider.GetUtcNow() or pass explicit DateTimeOffset now

EventConsistencyFirewall:
  Trigger: Pre-commit domain event dispatching
  Action: Commit transaction first via IUnitOfWork, then dispatch harvested events
```

---

## 3. Level 3: Canonical Working Archetypes

### 3.1 Architectural Perimeter Interception Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-FIREWALL-001]:
* Intercept fatal failure modes at the architecture perimeter before entering inner business execution.
* Route persistence and post-commit event harvesting through `IUnitOfWork`:
  >> Ref: `specs/01_clean_arch_patterns/03_infrastructure_layer.md`
* Route perimeter network validation through `ConnectionHostValidator`:
  >> Ref: `specs/00_architecture_core/03_security_and_performance.md`

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs (The 20 Fatal Traps)

### [TRAP-LOH-001]: Buffer Exceeding Large Object Heap Threshold
```diff
- byte[] bytes = await storage.DownloadAsync(id, ct);
- return File(bytes, "application/pdf");
+ Stream stream = await storage.OpenReadStreamAsync(id, ct);
+ return File(stream, "application/pdf", enableRangeProcessing: true);
```

### [TRAP-EF-002]: Shared Scoped DbContext in Parallel Tasks
```diff
- await Task.WhenAll(items.Select(i => _dbContext.Items.AddAsync(i).AsTask()));
+ await using var context = await _contextFactory.CreateDbContextAsync(ct);
+ await context.Items.AddRangeAsync(items, ct);
+ await context.SaveChangesAsync(ct);
```

### [TRAP-ASYNC-003]: Synchronous Blocking on Asynchronous Task
```diff
- var entity = _repository.GetByIdAsync(id).Result;
+ var entity = await _repository.GetByIdAsync(id, ct);
```

### [TRAP-AUTH-005]: Timing Side-Channel Discrepancy on Login
```diff
- if (user == null) return Unauthorized(); // Early return leaks user absence
+ if (user == null) {
+     _passwordHasher.Verify(DummyPasswordHash, password); // Constant-time burn
+     return Unauthorized();
+ }
```

### [TRAP-LEAK-006]: Domain Entity Exposed in API Presentation
```diff
- public async Task<ActionResult<{Aggregate}>> Get(Guid id) => Ok(await _repo.GetByIdAsync(id));
+ public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct) {
+     var query = new Get{Aggregate}Query(id);
+     var result = await _useCase.ExecuteAsync(query, ct);
+     return Ok(new ApiResponse<{Aggregate}ResultDto>(result));
+ }
```

### [TRAP-TIME-007]: Direct Clock Query Breaking Test Determinism
```diff
- var publishedAt = DateTime.UtcNow;
+ var publishedAt = _timeProvider.GetUtcNow();
```

### [TRAP-VOID-008]: Swallowed Exception in Catch Block
```diff
- catch (Exception ex) { /* swallowed */ }
+ catch (Exception ex) when (ex is not DomainException) {
+     _logger.LogError(ex, "Unexpected failure during document generation.");
+     throw;
+ }
```

### [TRAP-EVENT-010]: Pre-Commit Domain Event Dispatching
```diff
- await _eventDispatcher.DispatchAsync(entity.DomainEvents);
- await _dbContext.SaveChangesAsync();
+ await _dbContext.SaveChangesAsync(); // Commit transaction first
+ await _eventDispatcher.DispatchAsync(entity.DomainEvents);
```

### [TRAP-SQL-011]: Unchecked Database Host Opening SSRF Vector
```diff
- using var connection = new NpgsqlConnection(request.ConnectionString);
+ ConnectionHostValidator.EnsureSafeTarget(request.ConnectionString);
+ using var connection = new NpgsqlConnection(request.ConnectionString);
```

### [TRAP-SSRF-004]: Unsanitized HTML Forwarded to Gotenberg
```diff
- var httpResponse = await gotenbergClient.SendAsync(new StringContent(userHtml));
+ var sanitizedHtml = _htmlSanitizer.Sanitize(userHtml);
+ var httpResponse = await gotenbergClient.SendAsync(new StringContent(sanitizedHtml));
```

### [TRAP-AUDIT-009]: Unbounded JSON Stored in Relational OLTP Rows
```diff
- generationLog.PayloadJson = rawJson; // >16KB causes table bloat
+ if (rawJson.Length > 16384) {
+     var s3Path = await storageService.UploadPayloadAsync(rawJson, ct);
+     generationLog.PayloadHash = ComputeSha256(rawJson);
+     generationLog.StoragePath = s3Path;
+ }
```

### [TRAP-SLICE-012]: Multi-Type CQRS Slices in Single File
```diff
- {Verb}{Aggregate}UseCase.cs // (holds Command + Validator + UseCase)
+ {Verb}{Aggregate}Command.cs
+ {Verb}{Aggregate}CommandValidator.cs
+ {Verb}{Aggregate}UseCase.cs
```

### [TRAP-SQL-013]: N+1 External Database Queries Without Throttling
```diff
- foreach (var alias in aliases) await ExecuteDatasetAsync(alias);
+ await using var semaphore = new SemaphoreSlim(4);
+ await Task.WhenAll(aliases.Select(async a => {
+     await semaphore.WaitAsync(ct);
+     try { return await ExecuteDatasetAsync(a, ct); }
+     finally { semaphore.Release(); }
+ }));
```

### [TRAP-POD-014]: In-Memory Process-Only Idempotency
```diff
- _memoryCache.Set(idempotencyKey, cachedResult, TimeSpan.FromHours(24));
+ await redisDatabase.ScriptEvaluateAsync(LuaIdempotencyAcquireScript, [key], [ttl]);
```

### [TRAP-ARCH-015]: Premature Generic Base Class Over-Engineering
```diff
- // Over-engineered generic CRUD base class
- public abstract class CrudServiceBase<TEntity, TDto, TId> : ICrudService<TDto, TId> { ... }
- public class {Aggregate}Service : CrudServiceBase<{Aggregate}, {Aggregate}Dto, Guid> { ... }
+ // Flat, single-responsibility vertical slice complying with GodServiceEliminationFirewall
+ public sealed class {Verb}{Aggregate}UseCase(
+     I{Aggregate}Repository {aggregate}Repository,
+     IUnitOfWork unitOfWork) : IUseCase<{Verb}{Aggregate}Command, {Aggregate}ResultDto> {
+     public async Task<{Aggregate}ResultDto> ExecuteAsync({Verb}{Aggregate}Command command, CancellationToken ct) { ... }
+ }
```
*Rationale: Concrete layer violation of system-wide GodServiceEliminationFirewall. Multi-action generic base classes and fat services introduce artificial inheritance hierarchies, tight coupling, and obscure domain invariants. Every business capability must remain a flat, single-responsibility Action-Centric Vertical Slice.*

### [TRAP-SCALE-016]: Stateful In-Memory Singleton Preventing Horizontal Scaling
```diff
- // In-memory state breaks when scaling to multiple pods
- public sealed class DocumentRateLimiter {
-     private readonly ConcurrentDictionary<string, int> _tenantCounters = new();
-     public bool Allow(string tenantId) => _tenantCounters.AddOrUpdate(...) < 100;
- }
+ // External distributed state (Redis / RateLimiterMiddleware)
+ [EnableRateLimiting("rendering")] // Backed by distributed partition key
+ public sealed class {Aggregate}sController(...) : ControllerBase { ... }
```

### [TRAP-SSOT-017]: Relational Table Querying During Versioned Runtime Execution
```diff
- var mappings = template.FieldMappings.ToList(); // REJECTED: Mutable relational query causes version drift
+ List<FieldMapping> mappings = !string.IsNullOrWhiteSpace(version.MappingsSnapshot)
+     ? FieldMappingSnapshotSerializer.Deserialize(template.Id, version.MappingsSnapshot, now)
+     : template.FieldMappings.ToList();
```
>> SSoT DELEGATION: Full archetype and mechanics governed strictly by [document_rendering_mechanics.md](../03_specialized_mechanics/document_rendering_mechanics.md#trap-mech-007).
*Rationale: Querying mutable relational tables during document generation breaks version immutability. An author editing field mappings in the studio causes historical published versions to immediately drift and produce corrupt outputs. The frozen `TemplateVersion.MappingsSnapshot` is the single source of truth at runtime.*

### [TRAP-WRAPPER-018]: Anemic Pass-Through Wrapper & Redundant Intermediate DTOs
```diff
- var prepared = await dataPreparationWrapper.PrepareDataAsync(template, version, payload, skip, ct);
- var rawStream = await engine.RenderStreamAsync(templateStream, prepared.DataJson, format, ct);
+ var bound = await dataBindingEngine.BindAndValidateAsync(template, version, payload, skip, ct);
+ var rawStream = await engine.RenderStreamAsync(templateStream, bound.DataJson, format, ct);
```
>> SSoT DELEGATION: Governed system-wide by CodebaseSSoTAndDeduplicationFirewall in [01_system_architecture.md](./01_system_architecture.md#22-proactive-structural-firewalls); concrete pipeline application archetype governed by [document_rendering_mechanics.md](../03_specialized_mechanics/document_rendering_mechanics.md#trap-mech-008).
*Rationale: Concrete layer violation of system-wide CodebaseSSoTAndDeduplicationFirewall. Creating single-method wrappers or parallel services that merely forward arguments and duplicate DTO records adds cognitive bloat and GC allocations without delivering any domain abstraction. Pipelines must consume canonical domain engines directly.*

### [TRAP-DI-019]: Monolithic Composition Root Sprawl
```diff
- // Root DependencyInjection.cs dumping 50+ registrations across unrelated modules
- services.AddUseCase<GenerateDocumentCommand, ...>();
- services.AddUseCase<CreateTemplateCommand, ...>();
- services.AddUseCase<LoginUserCommand, ...>();
+ // Modular Bounded Context Installers complying with CompositionRootFirewall
+ services.AddRenderingModule();
+ services.AddAuthoringModule();
+ services.AddIntegrationModule();
+ services.AddIdentityAccessModule();
```
>> SSoT DELEGATION: Governed system-wide by CompositionRootFirewall in [01_system_architecture.md](./01_system_architecture.md#22-proactive-structural-firewalls); concrete Application module archetypes governed by [02_application_layer.md](../01_clean_arch_patterns/02_application_layer.md#36-modular-application-di-composition-archetype-addapplicationservices--bounded-context-modules); Infrastructure module archetypes governed by [03_infrastructure_layer.md](../01_clean_arch_patterns/03_infrastructure_layer.md#36-infrastructure-dependency-injection-composition-archetype).
*Rationale: Concrete layer violation of system-wide CompositionRootFirewall. Monolithic composition roots accumulate technical debt rapidly as developers append registrations indiscriminately. Decomposing by Bounded Context and Subsystem enforces single responsibility and makes subsystem testing and lifecycle auditing straightforward.*

### [TRAP-PREVIEW-020]: Parallel Duplicate Preview UseCases
```diff
- // Divergent preview use cases maintaining duplicate rendering logic
- public class PreviewMonacoUseCase { ... }
- public class PreviewSlugUseCase { ... }
- public class PreviewDraftUseCase { ... }
+ // Unified preview gateway routing all inputs through single non-persisting engine
+ public class PreviewDocumentUseCase(ITemplateRepository repo, IRenderEngineFactory factory, ...) : IUseCase<PreviewDocumentQuery, Stream> { ... }
```
>> SSoT DELEGATION: Presentation routing governed strictly by [04_api_presentation_layer.md](../01_clean_arch_patterns/04_api_presentation_layer.md#trap-api-013); preview execution archetype governed strictly by [document_rendering_mechanics.md](../03_specialized_mechanics/document_rendering_mechanics.md#32-unified-live-preview-gateway-archetype-previewresourceusecase--previewresourcequery).
*Rationale: Having separate preview use cases for Monaco live HTML, template slugs, and studio mapping editors causes business logic duplication and engine divergence. Consolidate all preview modes into `PreviewDocumentUseCase`.*
