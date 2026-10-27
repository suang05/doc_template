# [CORE-002]: anti_pattern_firewall.md — Fatal Production Traps & Anti-Pattern Interceptors

<context>
Scope: Fatal Production Traps, Memory Safety, Concurrency, Security & Layer Isolation Interceptors
Target: Full Stack (`backend-v2/` & `frontend-v2/`)
Authority: Absolute SSoT Invariant (Zero violation tolerated)
Invariants: Zero-LOH Streaming (<=81,920 buffer), Scoped DbContext Isolation, 100% Async/Await
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical ASCII Symbols
TriggerSignals: [SIGNAL_DECEPTIVE_BUG]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: High-Salience Fatal Traps Matrix

| Trap Identifier | Category | Prohibited Signature (! BANNED) | Canonical Remedy (>> CANONICAL) | Severity |
|---|---|---|---|---|
| **[TRAP-LOH-001]** | Memory Safety | `MemoryStream.ToArray()` / Buffer $\ge 85\text{KB}$ | Zero-LOH Stream-over-RAM direct response pipe | FATAL |
| **[TRAP-EF-002]** | Concurrency | Parallel consumption of scoped `AppDbContext` | `IDbContextFactory<AppDbContext>` per background task | FATAL |
| **[TRAP-ASYNC-003]** | Thread Safety | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | 100% `async`/`await` + forwarded `CancellationToken` | FATAL |
| **[TRAP-SSRF-004]** | Security | Forwarding unsanitized user HTML to Gotenberg | `HtmlSanitizer` + Chromium disabled flags + isolation | CRITICAL |
| **[TRAP-AUTH-005]** | Security | Early return on missing user email (Timing leak) | Constant-time dummy hash verification & RFC 9457 dispatch | CRITICAL |
| **[TRAP-LEAK-006]** | Architecture | Returning Domain Entities from Controller/UseCase | Explicit immutable Application DTO record mapping | HIGH |
| **[TRAP-TIME-007]** | Testability | `DateTime.UtcNow` / `DateTimeOffset.UtcNow` queries | Injected `TimeProvider` or explicit `DateTimeOffset now` | HIGH |
| **[TRAP-VOID-008]** | Reliability | Empty catch block swallowing exceptions | Rethrow strongly-typed `DomainException` or log | HIGH |
| **[TRAP-AUDIT-009]** | Storage / OLTP | Unbounded JSON payloads stored in PostgreSQL OLTP | Threshold offload (>16KB) to MinIO + SHA-256 in DB | HIGH |
| **[TRAP-EVENT-010]** | Consistency | Pre-commit domain event dispatching | Harvest, commit transaction, clear, dispatch post-commit | FATAL |

---

## 2. Level 2: Interceptor Category ASTs & Cognitive Firewall Engine

### Engineering Rationale & Causal Decisions
* **Why 85,000 bytes LOH Threshold:** Any object allocation $\ge 85,000$ bytes lands on the Large Object Heap (LOH), triggering expensive Gen 2 garbage collection sweeps and uncompacted memory fragmentation under high concurrent load.
* **Why `IDbContextFactory` for Parallel Tasks:** EF Core `DbContext` instances are fundamentally non-thread-safe. Concurrently calling methods on the same scoped instance causes race conditions, corrupted tracker state, and `InvalidOperationException`.
* **Why Constant-Time Dummy Hash Verification:** Early returns when an email does not exist create a timing disparity (<2ms vs ~100ms password hash verification), allowing attackers to enumerate registered email addresses via side-channel latency attacks.
* **Why Threshold Offloading for Audit Payloads:** Storing multi-MB JSON payloads directly in PostgreSQL OLTP rows causes severe table bloat, memory-to-disk paging, and degrades B-Tree index efficiency on primary keys.
* **Why Post-Commit Domain Event Dispatch:** Dispatching domain events before `SaveChangesAsync` commits creates "phantom side-effects" if the database transaction subsequently fails or aborts.
* **Why Defense-in-Depth HTML Sanitization:** Remote Chromium instances are susceptible to local file disclosure (`file:///`) and cloud instance metadata exfiltration (`169.254.169.254`) if dynamic user HTML is rendered raw.

### Cognitive Firewall & Interceptor Engine
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
    CanonicalAction: Apply HtmlSanitizer whitelist and execute with --chromium-allow-file-access-from-files=false >> Ref: [RULE-SSRF-001]

  - TrapId: [TRAP-AUTH-005]
    Trigger: Fast exit or conditional branch on non-existent user credential
    CanonicalAction: Verify against DummyPasswordHash for uniform execution latency >> Ref: [RULE-AUTH-001]

  - TrapId: [TRAP-LEAK-006]
    Trigger: Domain Entity or AggregateRoot crossing into Presentation controller
    CanonicalAction: Map strictly to immutable sealed record Application DTO

  - TrapId: [TRAP-TIME-007]
    Trigger: Direct system clock access via DateTime.UtcNow or DateTime.Now
    CanonicalAction: Inject TimeProvider.GetUtcNow() or pass explicit DateTimeOffset now

  - TrapId: [TRAP-VOID-008]
    Trigger: Empty catch block swallowing exception
    CanonicalAction: Rethrow strongly-typed DomainException or log with structured parameters

  - TrapId: [TRAP-AUDIT-009]
    Trigger: GenerationLog JSON payload exceeding 16KB threshold
    CanonicalAction: Offload payload to MinIO audit-payloads bucket and store SHA-256 hash in DB >> Ref: [RULE-AUDIT-001]

  - TrapId: [TRAP-EVENT-010]
    Trigger: Event dispatcher invoked prior to database transaction commit
    CanonicalAction: Harvest events, commit SaveChangesAsync, clear events, dispatch strictly post-commit >> Ref: [RULE-EVENT-001]

CoreExtensionSlots:
  StorageOffloadSlot: IBucketStorageService (MinIO S3 adapter / Enterprise Cloud Storage)
  SanitizationSlot: IHtmlSanitizer (HtmlRuleProvider with SVG / CSS policy)
  EventDispatchSlot: IDomainEventDispatcher (In-Memory Mediator / Outbox / EventBus)
```

---

## 3. Level 3: Canonical Implementation Archetypes

### Zero-LOH Direct Response Pipe
>> MANDATORY CANONICAL TEMPLATE [RULE-LOH-001]:
* [MUST] Pipe binary documents directly from source stream to `Response.Body` with a bounded buffer ($81,920$ bytes).
* [MUST NOT] Buffer streams into in-memory `byte[]` arrays, byte lists, or unbounded `MemoryStream` instances.
* [MUST] Forward `CancellationToken` to `Stream.CopyToAsync`.
```csharp
// >> CANONICAL: Direct stream pipe to HTTP response body (Zero-LOH buffer)
public async Task StreamPdfDocumentAsync(Guid id, HttpResponse response, CancellationToken ct)
{
    var stream = await documentStorage.OpenReadStreamAsync(id, ct);
    response.ContentType = "application/pdf";
    response.Headers.Append("Content-Disposition", "inline; filename=\"document.pdf\"");
    await stream.CopyToAsync(response.Body, 81920, ct);
}
```

### DbContextFactory Parallel Execution Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-CONC-001]:
* [MUST] Inject `IDbContextFactory<AppDbContext>` for multi-threaded, parallel, or background batch processing.
* [MUST] Create and dispose isolated `AppDbContext` instances inside each parallel task via `await using var dbContext = await contextFactory.CreateDbContextAsync(ct)`.
* [MUST NOT] Share a single scoped `AppDbContext` instance across concurrent threads.
```csharp
// >> CANONICAL: Isolated DbContext instances per parallel thread
public async Task ProcessBatchAsync(IReadOnlyList<Guid> ids, IDbContextFactory<AppDbContext> contextFactory, CancellationToken ct)
{
    await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, token) =>
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(token);
        var document = await dbContext.Documents.FindAsync([id], token);
        // Process independently within isolated DbContext
    });
}
```

### Post-Commit Domain Event Dispatcher Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-EVENT-001]:
* [MUST] Harvest domain events from tracked entities prior to transaction commit.
* [MUST] Clear entity domain events to prevent duplicate subsequent dispatches.
* [MUST] Commit database transaction via `await dbContext.SaveChangesAsync(ct)` BEFORE dispatching events.
* [MUST] Dispatch harvested domain events strictly post-commit to prevent phantom external side-effects.
```csharp
// >> CANONICAL: Post-commit domain event execution pipeline
public async Task CommitAndDispatchEventsAsync(AppDbContext dbContext, IDomainEventDispatcher dispatcher, CancellationToken ct)
{
    var entitiesWithEvents = dbContext.ChangeTracker.Entries<BaseEntity>()
        .Where(e => e.Entity.DomainEvents.Count > 0)
        .Select(e => e.Entity)
        .ToList();

    var pendingEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
    entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

    await dbContext.SaveChangesAsync(ct);

    if (dispatcher != null && pendingEvents.Count > 0)
    {
        await dispatcher.DispatchEventsAsync(pendingEvents, ct);
    }
}
```

### Defense-in-Depth HTML Sanitization & Streaming Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-SSRF-001]:
* [MUST] Sanitize dynamic template HTML via `HtmlSanitizer` whitelist before dispatching to Gotenberg.
* [MUST] Configure Gotenberg flags to prohibit local file system access (`--chromium-allow-file-access-from-files=false`).
* [MUST] Forward `CancellationToken` throughout the remote HTTP conversion pipeline.
```csharp
// >> CANONICAL: Multi-layer HTML sanitization and remote rendering dispatch
public async Task<Stream> ConvertHtmlToPdfStreamAsync(
    string rawHtml,
    IHtmlSanitizer sanitizer,
    IGotenbergClient gotenbergClient,
    CancellationToken ct)
{
    var sanitizedHtml = sanitizer.Sanitize(rawHtml);
    return await gotenbergClient.ConvertHtmlToPdfAsync(sanitizedHtml, ct);
}
```

### Constant-Time Authentication & Dummy Hash Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-AUTH-001]:
* [MUST] Execute password hash verification with uniform latency regardless of user account existence.
* [MUST] Substitute `SecurityConstants.DummyPasswordHash` when user lookup returns null to prevent email enumeration timing leaks.
* [MUST] Dispatch failed authentication to standard RFC 9457 `UnauthorizedException`.
```csharp
// >> CANONICAL: Constant-time authentication preventing side-channel timing leaks
public async Task<AuthTokenResultDto> AuthenticateAsync(
    LoginUserCommand request,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    CancellationToken ct)
{
    var user = await userRepository.FindByEmailAsync(request.Email, ct);
    var hashToVerify = user?.PasswordHash ?? SecurityConstants.DummyPasswordHash;
    var isPasswordValid = passwordHasher.VerifyPassword(hashToVerify, request.Password);

    if (user == null || !isPasswordValid)
    {
        throw new UnauthorizedDomainException("Invalid credentials.");
    }

    return await tokenService.GenerateTokensAsync(user, ct);
}
```

### Threshold-Based Payload Offloading Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-AUDIT-001]:
* [MUST] Evaluate JSON payload size against `AuditConstants.PayloadOffloadThresholdBytes` ($16,384$ bytes).
* [MUST] Offload payloads exceeding threshold to MinIO `audit-payloads` bucket and store pointer `[OFFLOADED:key]` in DB.
* [MUST] Calculate and persist SHA-256 checksum in PostgreSQL OLTP row for tamper-evident data integrity.
```csharp
// >> CANONICAL: Hybrid audit storage preventing PostgreSQL table bloat
public async Task<(string StoredPayload, string ChecksumSha256)> PrepareAuditPayloadAsync(
    Guid projectId,
    string jsonPayload,
    IBucketStorageService storageService,
    DateTimeOffset now,
    CancellationToken ct)
{
    var payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
    var checksum = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

    if (payloadBytes.Length <= 16384)
    {
        return (jsonPayload, checksum);
    }

    var storageKey = $"audit/{projectId:D}/{now:yyyy/MM/dd}/{Guid.CreateVersion7():D}.json";
    using var stream = new MemoryStream(payloadBytes, writable: false);
    await storageService.UploadStreamAsync("audit-payloads", storageKey, stream, "application/json", ct);

    return ($"[OFFLOADED:{storageKey}]", checksum);
}
```

---

## 4. Level 4: Trap Micro-Diffs & Anti-Patterns

### [TRAP-LOH-001]: Multi-MB Buffer Allocation in RAM
```diff
- using var ms = new MemoryStream(); await stream.CopyToAsync(ms, ct); byte[] payload = ms.ToArray(); return File(payload, "application/pdf");
+ response.ContentType = "application/pdf"; await stream.CopyToAsync(response.Body, 81920, ct);
```
*Rationale: Allocating buffers $\ge 85\text{KB}$ degrades GC Gen 2 and leads to OutOfMemory under concurrent load.*

### [TRAP-EF-002]: Scoped DbContext Concurrency in Background Operations
```diff
- await Parallel.ForEachAsync(ids, ct, async (id, t) => await scopedDbContext.Documents.FindAsync([id], t));
+ await using var dbContext = await contextFactory.CreateDbContextAsync(t); await dbContext.Documents.FindAsync([id], t);
```
*Rationale: Scoped DbContext is non-thread-safe; concurrent access causes corrupted state tracking and deadlocks.*

### [TRAP-ASYNC-003]: Synchronous Blocking on Asynchronous Code
```diff
- var template = repository.GetByIdAsync(id).Result;
+ var template = await repository.GetByIdAsync(id, ct);
```
*Rationale: Synchronous `.Result` or `.Wait()` causes thread pool exhaustion and deadlocks under ASP.NET Core.*

### [TRAP-SSRF-004]: Gotenberg HTML & Chromium SSRF Exploitation
```diff
- var response = await gotenbergClient.SendHtmlAsync(rawUserHtml, ct);
+ var safeHtml = htmlSanitizer.Sanitize(rawUserHtml); var response = await gotenbergClient.SendHtmlAsync(safeHtml, ct);
```
*Rationale: Unsanitized user HTML allows remote attackers to read server files (`file:///`) or cloud instance metadata.*

### [TRAP-AUTH-005]: Timing Attack on User Password Verification
```diff
- if (user == null) throw new UnauthorizedException("Invalid credentials."); bool valid = hasher.Verify(user.Hash, pwd);
+ string hash = user?.PasswordHash ?? SecurityConstants.DummyPasswordHash; bool valid = hasher.Verify(hash, pwd); if (user == null || !valid) throw new UnauthorizedException("Invalid credentials.");
```
*Rationale: Fast returns on non-existent users leak account registration state via side-channel latency differences.*

### [TRAP-LEAK-006]: Leaking Domain Entities Across Boundaries
```diff
- public async Task<ActionResult<Template>> GetById(Guid id) => Ok(await repo.GetByIdAsync(new TemplateId(id)));
+ public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => Ok(new ApiResponse<TemplateResultDto>(await useCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct)));
```
*Rationale: Domain entities leak internal structure, navigation proxies, and EF Core change tracking state to presentation callers.*

### [TRAP-TIME-007]: Direct System Clock Queries
```diff
- PublishedAt = DateTimeOffset.UtcNow;
+ PublishedAt = timeProvider.GetUtcNow();
```
*Rationale: Ambient system clock access prevents deterministic unit testing via `FakeTimeProvider`.*

### [TRAP-VOID-008]: Swallowed Exceptions & Empty Catch Blocks
```diff
- try { await unitOfWork.CommitAsync(ct); } catch { }
+ try { await unitOfWork.CommitAsync(ct); } catch (DbUpdateConcurrencyException ex) { logger.LogWarning(ex, "Conflict {Id}", id); throw new ConcurrencyDomainException("Resource modified by another transaction."); }
```
*Rationale: Swallowed exceptions hide silent transaction failures and leave the system in an inconsistent state.*

### [TRAP-AUDIT-009]: Raw Multi-MB Payload in PostgreSQL OLTP
```diff
- var log = GenerationLog.CreateSuccess(..., inputData: largeJsonPayload, ...);
+ var (storedData, hash) = await payloadStorage.PreparePayloadAsync(largeJsonPayload, ct);
```
*Rationale: Storing large payloads (>16KB) directly in PostgreSQL OLTP causes severe disk/memory paging and index bloat.*

### [TRAP-EVENT-010]: Pre-Commit Domain Event Dispatch (Phantom Side-Effects)
```diff
- await dispatcher.DispatchEventsAsync(entity.DomainEvents, ct); await dbContext.SaveChangesAsync(ct);
+ await dbContext.SaveChangesAsync(ct); await dispatcher.DispatchEventsAsync(pendingEvents, ct);
```
*Rationale: Pre-commit dispatching triggers external side-effects (e.g. email, message queue) that cannot be rolled back if DB throws.*
