# [CORE-002]: anti_pattern_firewall.md — Fatal Production Traps & Anti-Pattern Interceptors

<context>
Scope: Fatal Production Traps, Memory Safety, Concurrency, Security & Layer Isolation Interceptors
Target: Full Stack (`backend-v2/` & `frontend-v2/`)
Authority: Absolute SSoT Invariant (Zero violation tolerated)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_DECEPTIVE_BUG]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: High-Salience Fatal Traps Matrix

| Trap Identifier | Category | Prohibited Signature (! BANNED) | Canonical Remedy (>> CANONICAL) |
|---|---|---|---|
| **[TRAP-LOH-001]** | Memory Safety | `MemoryStream.ToArray()` / Buffer $\ge 85\text{KB}$ | Zero-LOH Stream-over-RAM direct response pipe |
| **[TRAP-EF-002]** | Concurrency | Parallel consumption of scoped `AppDbContext` | `IDbContextFactory<AppDbContext>` per background worker |
| **[TRAP-ASYNC-003]** | Thread Safety | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | 100% `async`/`await` + forwarded `CancellationToken` |
| **[TRAP-SSRF-004]** | Security | Forwarding unsanitized user HTML to Gotenberg Chromium | `HtmlSanitizer` + Chromium disabled flags + network isolation |
| **[TRAP-AUTH-005]** | Security | Early return on missing user email (Timing leak) | Constant-time dummy hash verification & RFC 9457 dispatch |
| **[TRAP-LEAK-006]** | Architecture | Returning Domain Entities from Controller or UseCase | Explicit immutable Application DTO record mapping |
| **[TRAP-TIME-007]** | Testability | `DateTime.UtcNow` / `DateTimeOffset.UtcNow` queries | Injected `TimeProvider` or explicit `DateTimeOffset now` parameter |
| **[TRAP-VOID-008]** | Reliability | Empty catch block swallowing exceptions | Rethrow strongly-typed `DomainException` or structured log |
| **[TRAP-AUDIT-009]** | Storage / OLTP | Unbounded JSON payloads stored in PostgreSQL OLTP | Threshold offload (>16KB) to MinIO `audit-payloads` + SHA-256 hash in DB |

---

## 2. Level 2: Interceptor Category ASTs

### Memory Safety Invariants
* Any buffer $\ge 85,000$ bytes lands on the Large Object Heap (LOH).
* Streaming endpoints MUST pipe `Stream` to `Response.Body` using bounded buffers ($81,920$ bytes). Zero multi-MB byte array allocations.

### Concurrency Invariants
* EF Core `DbContext` instances are strictly non-thread-safe and scoped to a single logical thread/request.
* Multi-threaded operations MUST instantiate short-lived contexts via `IDbContextFactory<T>`.

### Security Invariants
* Dynamic template inputs must be neutralized against SSRF (`file:///`, metadata endpoints).
* Authentication paths must execute with uniform latency regardless of user presence.

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

---

## 4. Level 4: Trap Micro-Diffs & Anti-Patterns

### [TRAP-LOH-001]: Multi-MB Buffer Allocation in RAM
```csharp
// ! BANNED: Buffering Gotenberg / MinIO binary outputs into RAM byte arrays
using var ms = new MemoryStream();
await stream.CopyToAsync(ms, ct);
byte[] payload = ms.ToArray(); // FATAL: LOH Allocation!
return File(payload, "application/pdf", "document.pdf");

// >> CANONICAL: Stream-over-RAM Direct Response Pipe
Response.ContentType = "application/pdf";
Response.Headers.Append("Content-Disposition", "inline; filename=\"document.pdf\"");
await stream.CopyToAsync(Response.Body, 81920, ct);
```

### [TRAP-EF-002]: Scoped DbContext Concurrency in Background Operations
```csharp
// ! BANNED: Capturing scoped DbContext in parallel or background execution
public Task ProcessBatchAsync(List<Guid> ids, AppDbContext dbContext)
{
    return Task.WhenAll(ids.Select(id => dbContext.Documents.FindAsync(id).AsTask())); // FATAL: Concurrent DbContext access!
}

// >> CANONICAL: IDbContextFactory<AppDbContext> for independent units of work
await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, token) =>
{
    await using var dbContext = await contextFactory.CreateDbContextAsync(token);
    var document = await dbContext.Documents.FindAsync([id], token);
});
```

### [TRAP-ASYNC-003]: Synchronous Blocking on Asynchronous Code
```csharp
// ! BANNED: Synchronous blocking causing thread-pool starvation
public Template GetTemplate(Guid id)
{
    return repository.GetByIdAsync(id).Result; // FATAL: Thread deadlock / starvation!
}

// >> CANONICAL: 100% async/await with forwarded CancellationToken
public async Task<Template?> GetTemplateAsync(TemplateId id, CancellationToken ct)
{
    return await repository.GetByIdAsync(id, ct);
}
```

### [TRAP-SSRF-004]: Gotenberg HTML & Chromium SSRF Exploitation
```csharp
// ! BANNED: Forwarding raw user HTML directly to Gotenberg without sanitization
var response = await gotenbergClient.SendHtmlAsync(rawUserHtml, ct);

// >> CANONICAL: Defense-in-depth HTML sanitization & Chromium container flags
// 1. Sanitize HTML via HtmlSanitizer SSoT: strip <iframe src="file://">, <object>, <embed>
// 2. Gotenberg container: --chromium-disable-javascript=false --chromium-allow-file-access-from-files=false
// 3. Network perimeter blocks 169.254.0.0/16 and 127.0.0.0/8 from rendering container
```

### [TRAP-AUTH-005]: Timing Attack on User Password Verification
```csharp
// ! BANNED: Fast return on non-existent user leaking email validity
var user = await userRepository.FindByEmailAsync(request.Email, ct);
if (user == null)
{
    throw new UnauthorizedException("Invalid email or password."); // Fast return (< 2ms) leaks user existence
}
bool valid = passwordHasher.VerifyPassword(user.PasswordHash, request.Password);

// >> CANONICAL: Constant-time dummy verification hash & RFC 9457 exception
var user = await userRepository.FindByEmailAsync(request.Email, ct);
string hashToVerify = user?.PasswordHash ?? SecurityConstants.DummyPasswordHash;
bool valid = passwordHasher.VerifyPassword(hashToVerify, request.Password);

if (user == null || !valid)
{
    throw new UnauthorizedException("Invalid email or password."); // Uniform latency dispatched to RFC 9457
}
```

### [TRAP-LEAK-006]: Leaking Domain Entities Across Boundaries
```csharp
// ! BANNED: Returning Domain Entity directly from Controller or UseCase
[HttpGet("{id}")]
public async Task<ActionResult<Template>> Get(Guid id) // FATAL: Exposing internal domain state
{
    var template = await templateRepository.GetByIdAsync(new TemplateId(id));
    return Ok(template);
}

// >> CANONICAL: Explicit mapping to immutable Application DTO record
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
{
    var result = await useCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
    return Ok(new ApiResponse<TemplateResultDto>(result));
}
```

### [TRAP-TIME-007]: Direct System Clock Queries
```csharp
// ! BANNED: Direct system clock access makes business logic non-deterministic
public void Publish()
{
    PublishedAt = DateTimeOffset.UtcNow; // FATAL: Non-deterministic time coupling
}

// >> CANONICAL: Injected TimeProvider or explicit parameter dependency
public void Publish(DateTimeOffset now)
{
    PublishedAt = Guard.NotDefault(now, nameof(now));
}
```

### [TRAP-VOID-008]: Swallowed Exceptions & Empty Catch Blocks
```csharp
// ! BANNED: Empty catch block swallowing errors
try { await unitOfWork.CommitAsync(ct); } catch { } // REJECTED

// >> CANONICAL: Rethrow strongly-typed domain exception or structured semantic log
try
{
    await unitOfWork.CommitAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    logger.LogWarning(ex, "Concurrency conflict during template update {TemplateId}", id);
    throw new ConcurrencyDomainException("Resource was modified by another transaction.");
}
```

### [TRAP-AUDIT-009]: Raw Multi-MB Payload in PostgreSQL OLTP
```csharp
// ! BANNED: Storing unbounded JSON payloads in PostgreSQL OLTP GenerationLog
var log = GenerationLog.CreateSuccess(..., inputData: largeJsonPayload, ...);

// >> CANONICAL: Threshold Offloading to MinIO + SHA-256 Integrity Hash
var (storedInputData, payloadHash) = await PreparePayloadAsync(projectId, dataJson, now, ct);
// If bytes > 16KB: stream to MinIO "audit-payloads", store "[OFFLOADED:audit/...]" pointer & SHA-256 in DB.
// If bytes <= 16KB: store inline JSON & SHA-256 in DB.
```
