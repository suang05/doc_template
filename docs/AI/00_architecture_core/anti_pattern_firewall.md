# [CORE-002]: anti_pattern_firewall.md — Fatal Production Traps & Anti-Pattern Interceptors

<context>
Scope: Fatal Production Traps, Memory Safety, Concurrency, Security & Layer Isolation Interceptors
Target: Full Stack (`backend-v2/` & `frontend-v2/`)
Authority: Absolute SSoT Invariant (Zero violation tolerated)
</context>

---

## 1. Memory Safety & Large Object Heap (LOH)

### [TRAP-LOH-001]: Multi-MB Buffer Allocation in RAM
Allocating byte arrays (`byte[]`) $\ge 85,000$ bytes lands on the Large Object Heap (LOH), causing GC fragmentation and Gen 2 collection stalls under high throughput.

```csharp
// ! BANNED: Buffering Gotenberg / MinIO binary outputs into RAM
public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
{
    var stream = await documentStorage.OpenReadStreamAsync(id, ct);
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms, ct);
    byte[] payload = ms.ToArray(); // FATAL: LOH Allocation!
    return File(payload, "application/pdf", "document.pdf");
}

// >> CANONICAL: Zero-LOH Stream-over-RAM Direct Response Pipe
public async Task DownloadDocument(Guid id, CancellationToken ct)
{
    var stream = await documentStorage.OpenReadStreamAsync(id, ct);
    Response.ContentType = "application/pdf";
    Response.Headers.Append("Content-Disposition", "inline; filename=\"document.pdf\"");
    await stream.CopyToAsync(Response.Body, 81920, ct); // Piped directly without multi-MB buffer
}
```

---

## 2. Concurrency & Thread-Safety

### [TRAP-EF-002]: Scoped DbContext Concurrency in Background Operations
EF Core `AppDbContext` is strictly non-thread-safe. Consuming a scoped DbContext across asynchronous tasks or parallel threads causes `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.

```csharp
// ! BANNED: Capturing scoped DbContext in parallel or background execution
public Task ProcessBatchAsync(List<Guid> ids, AppDbContext dbContext)
{
    return Task.WhenAll(ids.Select(id => dbContext.Documents.FindAsync(id).AsTask())); // FATAL: Concurrent DbContext access!
}

// >> CANONICAL: IDbContextFactory<AppDbContext> for independent units of work
public async Task ProcessBatchAsync(List<Guid> ids, IDbContextFactory<AppDbContext> contextFactory, CancellationToken ct)
{
    await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, token) =>
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(token);
        var document = await dbContext.Documents.FindAsync([id], token);
        // Process independently
    });
}
```

---

## 3. Asynchronous Execution Safety

### [TRAP-ASYNC-003]: Synchronous Blocking on Asynchronous Code
Invoking `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` locks the calling thread, causing ThreadPool starvation and thread-deadlocks under load.

```csharp
// ! BANNED: Synchronous blocking
public Template GetTemplate(Guid id)
{
    return repository.GetByIdAsync(id).Result; // FATAL: Thread deadlock / starvation!
}

// >> CANONICAL: 100% async/await with forwarded CancellationToken
public async Task<Template?> GetTemplateAsync(TemplateId id, CancellationToken cancellationToken)
{
    return await repository.GetByIdAsync(id, cancellationToken);
}
```

---

## 4. Security & Boundary Defense

### [TRAP-SSRF-004]: Gotenberg HTML & Chromium SSRF Exploitation
Gotenberg renders user-provided HTML via Chromium. Unsanitized templates requesting `file:///` or cloud metadata (`http://169.254.169.254/`) expose local server files and infrastructure credentials.

```csharp
// ! BANNED: Forwarding raw user HTML directly to Gotenberg without sanitization or network isolation
var response = await gotenbergClient.SendHtmlAsync(rawUserHtml, ct);

// >> CANONICAL: Defense-in-depth HTML sanitization & Gotenberg Chromium flags
// 1. Sanitize HTML via HtmlSanitizer SSoT: strip <iframe src="file://">, <object>, <embed>
// 2. Gotenberg Docker container executed with: --chromium-disable-javascript=false --chromium-allow-file-access-from-files=false
// 3. Network perimeter blocks outbound requests to 169.254.0.0/16 and 127.0.0.0/8 from rendering container
```

### [TRAP-AUTH-005]: Timing Attack on User Password Verification
Returning early when a user account does not exist allows attackers to enumerate valid usernames by measuring response time deltas.

```csharp
// ! BANNED: Fast return on non-existent user leaking user identity
var user = await userRepository.FindByEmailAsync(request.Email, ct);
if (user == null)
{
    throw new UnauthorizedException("Invalid email or password."); // Fast return (< 2ms) leaks user existence
}
bool valid = passwordHasher.VerifyPassword(user.PasswordHash, request.Password); // Slow BCrypt (~80ms)

// >> CANONICAL: Constant-time dummy verification hash & RFC 9457 exception
var user = await userRepository.FindByEmailAsync(request.Email, ct);
string hashToVerify = user?.PasswordHash ?? SecurityConstants.DummyPasswordHash;
bool valid = passwordHasher.VerifyPassword(hashToVerify, request.Password);

if (user == null || !valid)
{
    throw new UnauthorizedException("Invalid email or password."); // Uniform latency dispatched to RFC 9457 ProblemDetails
}
```

---

## 5. Domain & Layer Encapsulation

### [TRAP-LEAK-006]: Leaking Domain Entities Across Application / Presentation Boundary
Exposing Domain Entities directly to Controllers causes JSON serialization circular references, over-posting vulnerabilities, and leaks internal domain state.

```csharp
// ! BANNED: Returning Domain Entity directly from Controller or UseCase
[HttpGet("{id}")]
public async Task<ActionResult<Template>> Get(Guid id) // FATAL: Exposing internal domain state
{
    var template = await templateRepository.GetByIdAsync(new TemplateId(id));
    return Ok(template);
}

// >> CANONICAL: Explicit mapping to immutable Application DTO and ApiResponse<T> record (Exceptions handled via RFC 9457)
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
{
    var result = await useCase.ExecuteAsync(new GetTemplateByIdQuery(id), ct);
    return Ok(new ApiResponse<TemplateResultDto>(result));
}
```

### [TRAP-TIME-007]: Direct System Clock Queries
Direct calls to `DateTime.UtcNow` or `DateTimeOffset.UtcNow` make business logic untestable with determinism.

```csharp
// ! BANNED: Direct system clock access
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
Catching exceptions without re-throwing or logging hides catastrophic failures.

```csharp
// ! BANNED: Empty catch block
try { await unitOfWork.CommitAsync(ct); } catch { } // REJECTED

// >> CANONICAL: Rethrow typed domain exception or structured semantic log
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
