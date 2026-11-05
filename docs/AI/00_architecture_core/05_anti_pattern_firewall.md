# [CORE-005]: 05_anti_pattern_firewall.md — Fatal Traps, Architectural Interceptors & Diagnostics

<context>
Scope: Fatal Production Traps, Memory Safety Interceptors, Security Hardening, and Diagnostic Remediation Cards.
SSoT_For: System-Wide Anti-Pattern Traps (TRAP-001 to TRAP-016), Interceptor Triggers, and Critical Failure Modes.
Out_Of_Scope: Basic language syntax (see: 02_coding_conventions.md), System Architecture layers (see: 01_system_architecture.md).
Target: Full Stack (`backend-v2/` and `frontend-v2/`).
ArchitecturalSignals: [ARCH_EVOLUTION_RENDERING], [ARCH_EVOLUTION_SECURITY]
</context>

---

## 1. Level 1: High-Salience Fatal Traps Matrix

| Trap ID | Category | Prohibited Signature (! BANNED) | Canonical Remedy (>> MANDATORY) | Severity |
|---|---|---|---|---|
| **[TRAP-LOH-001]** | Memory Safety | ! `MemoryStream.ToArray()`, `Task<byte[]>`, or in-memory buffer $\ge 85\text{KB}$ | >> Bounded streaming egress via `File(stream, ...)` with buffer $\le 81,920$ bytes | FATAL |
| **[TRAP-EF-002]** | Concurrency | ! Parallel operations sharing scoped `AppDbContext` | >> Inject `IDbContextFactory<AppDbContext>` for per-task isolation | FATAL |
| **[TRAP-ASYNC-003]** | Thread Safety | ! Synchronous blocking (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) | >> 100% `async`/`await` with forwarded `CancellationToken ct` | FATAL |
| **[TRAP-SSRF-004]** | Security | ! Unsanitized dynamic user HTML forwarded to Gotenberg | >> Apply `HtmlSanitizer` whitelist and run Chromium with restricted file flags | CRITICAL |
| **[TRAP-AUTH-005]** | Security | ! Early return on missing user email (timing leak) | >> Verify against `DummyPasswordHash` for uniform execution latency | CRITICAL |
| **[TRAP-LEAK-006]** | Architecture | ! Returning Domain Entities from Controllers or UseCases | >> Map explicitly to immutable sealed positional `record` DTOs | HIGH |
| **[TRAP-TIME-007]** | Testability | ! Direct queries to system clock (`DateTime.UtcNow`, `DateTime.Now`) | >> Inject `TimeProvider` or pass explicit `DateTimeOffset now` | HIGH |
| **[TRAP-VOID-008]** | Reliability | ! Swallowing exceptions with empty catch blocks (`catch (Exception) {}`) | >> Let bubble to RFC 9457 handler or rethrow strongly-typed `DomainException` | HIGH |
| **[TRAP-AUDIT-009]** | Storage / OLTP | ! Unbounded JSON payloads stored directly in PostgreSQL OLTP rows | >> Offload payloads $>16\text{KB}$ to MinIO S3 and store SHA-256 in DB | HIGH |
| **[TRAP-EVENT-010]** | Consistency | ! Pre-commit domain event dispatching | >> Commit transaction via `unitOfWork.CommitAsync(ct)`, then dispatch | FATAL |
| **[TRAP-SQL-011]** | Security / SSRF | ! Unchecked database connection hosts or mutating SQL execution | >> Validate target host via `ConnectionHostValidator`; enforce `READ ONLY` tx | CRITICAL |
| **[TRAP-SLICE-012]** | Architecture | ! Declaring multiple primary types in UseCase file (*UseCase.cs) | >> Enforce 1-Primary-Type-Per-File (`{TypeName}.cs`) per CQRS slice | HIGH |
| **[TRAP-SQL-013]** | Performance | ! N+1 dataset lookups or unbounded parallel SQL queries | >> Batch `.Include()` query + `SemaphoreSlim(4)` + fail-fast cancellation | HIGH |
| **[TRAP-POD-014]** | Statelessness | ! In-memory process-only idempotency or raw API keys in Redis | >> Atomic Redis Lua script with `SHA-256` key hash and fail-closed 503 | CRITICAL |

---

## 2. Level 2: Interceptor Category ASTs & Cognitive Firewall Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why 81,920-Byte LOH Threshold**: Any memory allocation $\ge 85,000$ bytes lands on the Large Object Heap (LOH), triggering expensive Gen 2 garbage collection sweeps and uncompacted memory fragmentation under high concurrent load.
* **Why `IDbContextFactory` for Parallel Tasks**: EF Core `DbContext` instances are fundamentally non-thread-safe. Concurrently calling methods on the same scoped instance causes race conditions, corrupted tracker state, and `InvalidOperationException`.
* **Why Constant-Time Dummy Hash Verification**: Early returns when an email does not exist create a timing disparity (<2ms vs ~100ms password hash verification), allowing attackers to enumerate registered email addresses via side-channel latency attacks.
* **Why Post-Commit Domain Event Dispatch**: Dispatching domain events before `SaveChangesAsync` commits creates "phantom side-effects" if the database transaction subsequently fails or aborts.

### 2.2 Cognitive Firewall & Interceptor Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Zero-Regression Fatal Trap Interception
  TradeOffPriority: MemorySafety (Zero-LOH) > DataConsistency (Post-Commit) > Security (SSRF/Timing) > ConcurrencySafety > RawSpeed

ProactiveFirewallInterceptors:
  - TrapId: [TRAP-LOH-001]
    Trigger: Detection of in-memory byte[] allocation, MemoryStream.ToArray(), or buffer >= 85KB
    CanonicalAction: Pipe live Stream to HttpResponse.Body with bounded 81,920 buffer >> Ref: [RULE-LOH-001]

  - TrapId: [TRAP-EF-002]
    Trigger: Parallel task or background worker capturing scoped AppDbContext
    CanonicalAction: Inject IDbContextFactory<AppDbContext> and create per-task isolated context >> Ref: [RULE-CONC-001]

  - TrapId: [TRAP-ASYNC-003]
    Trigger: .Result, .Wait(), or .GetAwaiter().GetResult() blocking call
    CanonicalAction: Forward CancellationToken through 100% async/await pipeline

  - TrapId: [TRAP-SSRF-004]
    Trigger: Unsanitized dynamic HTML forwarded to Chromium rendering engine
    CanonicalAction: Apply HtmlSanitizer whitelist and execute with restricted file flags >> Ref: [RULE-SSRF-001]

  - TrapId: [TRAP-AUTH-005]
    Trigger: Fast exit or conditional branch on non-existent user credential
    CanonicalAction: Verify against DummyPasswordHash for uniform execution latency >> Ref: [RULE-AUTH-001]

  - TrapId: [TRAP-LEAK-006]
    Trigger: Domain Entity or AggregateRoot crossing into Presentation controller
    CanonicalAction: Map strictly to immutable sealed record Application DTO

  - TrapId: [TRAP-TIME-007]
    Trigger: Direct system clock access via DateTime.UtcNow or DateTime.Now
    CanonicalAction: Inject TimeProvider.GetUtcNow() or pass explicit DateTimeOffset now

  - TrapId: [TRAP-EVENT-010]
    Trigger: Pre-commit domain event dispatching
    CanonicalAction: Commit transaction first, then dispatch harvested events

  - TrapId: [TRAP-SQL-011]
    Trigger: External database connection targeting loopback or internal infrastructure
    CanonicalAction: Block target via ConnectionHostValidator >> Ref: [RULE-SEC-005]
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Post-Commit Domain Event Dispatcher Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-EVENT-001]:
* [MUST] Commit database state changes via `SaveChangesAsync` BEFORE dispatching events.
* [MUST] Clear domain events from tracked entities after harvesting to prevent duplicate dispatching.

```csharp
public sealed class TransactionalUnitOfWork(
    AppDbContext dbContext,
    IDomainEventDispatcher eventDispatcher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
    {
        // 1. Harvest events from tracked aggregate roots
        var aggregates = dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();

        // 2. Persist state changes in database transaction
        await dbContext.SaveChangesAsync(ct);

        // 3. Clear domain events to prevent duplicate dispatching
        aggregates.ForEach(a => a.ClearDomainEvents());

        // 4. Dispatch events strictly after successful database commit
        foreach (var domainEvent in events)
        {
            await eventDispatcher.DispatchAsync(domainEvent, ct);
        }
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs (The 14 Fatal Traps)

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
- var template = _repository.GetByIdAsync(id).Result;
+ var template = await _repository.GetByIdAsync(id, ct);
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
- public async Task<ActionResult<Template>> Get(Guid id) => Ok(await _repo.GetByIdAsync(id));
+ public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct) {
+     var query = new GetTemplateQuery(id);
+     var result = await _useCase.ExecuteAsync(query, ct);
+     return Ok(new ApiResponse<TemplateResultDto>(result));
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
- RollbackTemplateVersionUseCase.cs // (holds Command + Validator + UseCase)
+ RollbackTemplateVersionCommand.cs
+ RollbackTemplateVersionCommandValidator.cs
+ RollbackTemplateVersionUseCase.cs
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
- // BANNED: Over-engineered generic CRUD base class
- public abstract class CrudServiceBase<TEntity, TDto, TId> : ICrudService<TDto, TId> {
-     protected abstract Task<TEntity> MapToEntity(TDto dto);
-     public virtual async Task<TDto> CreateAsync(TDto dto) { ... }
- }
- public class TemplateService : CrudServiceBase<Template, TemplateDto, Guid> { ... }
+ // MANDATORY: Flat, single-responsibility vertical slice
+ public sealed class CreateTemplateUseCase(
+     ITemplateRepository templateRepository,
+     IUnitOfWork unitOfWork) : IUseCase<CreateTemplateCommand, TemplateResultDto> {
+     public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct) { ... }
+ }
```
*Rationale: Generic CRUD base services obscure business logic, force awkward inheritance overrides, and create tight coupling across disparate domain entities. Flat single-responsibility UseCases maximize readability and isolate changes.*

### [TRAP-SCALE-016]: Stateful In-Memory Singleton Preventing Horizontal Scaling
```diff
- // BANNED: In-memory state breaks when scaling to multiple pods
- public sealed class DocumentRateLimiter {
-     private readonly ConcurrentDictionary<string, int> _tenantCounters = new();
-     public bool Allow(string tenantId) => _tenantCounters.AddOrUpdate(...) < 100;
- }
+ // MANDATORY: External distributed state (Redis / RateLimiterMiddleware)
+ [EnableRateLimiting("rendering")] // Backed by distributed partition key
+ public sealed class DocumentGenerationController(...) : ControllerBase { ... }
```
*Rationale: In-memory state in singletons is invisible to other pod replicas in a Kubernetes cluster, leading to rate-limiting bypasses and inconsistent application state across instances.*
