# [CORE-003]: 03_security_and_performance.md — M2M Security, Tenant Isolation & High-Throughput Performance SSoT

<context>
Scope: M2M Dual-Channel Authentication, ClaimsPrincipal Hydration, Global User vs Tenant Scoping, Zero-LOH Memory Invariants, Gotenberg Protection, SSRF Defense, and Constant-Time Hashing.
SSoT_For: Authentication Schemes, Role Claims Transformation, Ambient Tenancy Resolution, Stream Buffer Limits (<=81,920), and SSRF Host Validation.
Out_Of_Scope: Controller route attributes and RFC 9457 error translation (see: 01_clean_arch_patterns/04_api_presentation_layer.md), General coding idioms (see: 02_coding_conventions.md).
Target: `backend-v2/` (`SmkDoc.Infrastructure/`, `SmkDoc.Api/Middleware/`, and `SmkDoc.Domain/Entities/`).
TriggerSignals: [SIGNAL_SECURITY_MUTATION], [SIGNAL_TOPOLOGY_MUTATION]
</context>

---

## 1. Level 1: Security & Performance Invariants Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) | Enforcement Point |
|---|---|---|---|
| **M2M Key Validation** | ! Variable-time string comparison (`key == storedHash`) | >> Constant-time `CryptographicOperations.FixedTimeEquals` | `ApiKeyValidator` |
| **M2M Claims Transport**| ! Setting ambient context without `ClaimsPrincipal` on `User` | >> Hydrate `ClaimsPrincipal` (`Role: M2MClient`, `Scope`) into `context.User` | `ProjectContextMiddleware` |
| **User Tenancy** | ! Storing `project_id` or `tenant_id` in `users` table | >> `User` is Global Identity; tenancy scoped strictly via `UserProjectRole` | Domain / EF Schema |
| **Multi-Tenant Filter**| ! Manual `WHERE project_id = @id` queries | >> Compile-time EF Core `HasQueryFilter` on `ICurrentProjectContext` | `AppDbContext` |
| **Memory Buffer** | ! `MemoryStream.ToArray()` or in-memory buffer $\ge 85\text{KB}$ | >> Direct stream pipe to `Response.Body` with bounded buffer $\le 81,920$ bytes | Render / Download Egress |
| **Download Egress** | ! HTTP 307 Redirects to internal MinIO S3 instances | >> Gateway reverse-proxy streaming via `File(stream, ...)` | `DocumentController` |
| **SSRF Host Defense** | ! Unchecked database hosts or targeting loopback / metadata | >> Pre-flight DNS/IP validation via `ConnectionHostValidator` | Data Connection Services |
| **Chromium Cluster** | ! Unthrottled render calls crashing Gotenberg pods | >> Dedicated rate limiter (`30 req/min`) + Async batch queue | Presentation Gateway |

---

## 2. Level 2: Core Security AST & Cognitive Interceptor Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why Constant-Time Hash Comparison**: Standard string `==` terminates on the first mismatched byte, creating microsecond timing discrepancies that allow attackers to deduce valid API keys via side-channel latency measurements.
* **Why `ClaimsPrincipal` Hydration for M2M**: ASP.NET Core `[Authorize]` attributes evaluate `HttpContext.User.Identity.IsAuthenticated`. Failing to set a `ClaimsPrincipal` on valid API keys causes `app.UseAuthorization()` to reject M2M requests with `401 Unauthorized`.
* **Why 81,920-Byte Buffer Threshold**: Any memory allocation exceeding 85,000 bytes is placed on the Large Object Heap (LOH). The .NET Garbage Collector does not compact the LOH during Gen 0/1 sweeps, leading to Gen 2 GC pauses and server stalls under concurrent load.
* **Why Banning HTTP 307 Redirects to MinIO**: MinIO runs inside an isolated Docker network. Returning a 307 redirect exposes private hostnames (`http://minio:9000/...`), fails for external callers, and violates Zero-Trust perimeter boundaries.

### 2.2 Cognitive Security & Performance Interceptors
```yaml
OptimizationObjectives:
  PrimaryObjective: Zero-Leakage M2M Dual-Auth, Constant Memory Bounds & SSRF Immunity
  TradeOffPriority: ZeroCrossTenantLeakage > MemorySafety (Zero-LOH) > M2MFirstIntegrity > NetworkPerimeterIsolation

ProactiveSecurityGuards:
  - Scope: SmkDoc.Infrastructure
    Trigger: String equality check (== or .Equals()) on API key hashes
    CanonicalAction: Enforce CryptographicOperations.FixedTimeEquals on byte arrays >> Ref: [RULE-SEC-001]

  - Scope: SmkDoc.Api.Middleware
    Trigger: Validating API Key without setting ClaimsPrincipal on HttpContext.User
    CanonicalAction: Hydrate ClaimsIdentity with Role=M2MClient, Scope, and ProjectId >> Ref: [RULE-SEC-002]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Detection of in-memory byte[] allocation, MemoryStream.ToArray(), or buffer >= 85KB
    CanonicalAction: Pipe live Stream to HttpResponse.Body with bounded 81,920 buffer >> Ref: [RULE-SEC-003]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Returning HTTP 307 Redirect to internal MinIO storage URL
    CanonicalAction: Reject redirect; stream content directly through gateway using File(stream, ...) >> Ref: [RULE-SEC-004]

  - Scope: SmkDoc.Infrastructure
    Trigger: External database connection targeting loopback, link-local, or container network alias
    CanonicalAction: Throw SecurityViolationException via ConnectionHostValidator >> Ref: [RULE-SEC-005]

CoreExtensionSlots:
  ApiKeyValidatorSlot: Cryptographic hash generator and verification engine
  StorageProviderSlot: IBucketStorageService (MinIO S3 / AWS S3 / Azure Blob Storage)
  RenderingEngineSlot: IRenderEngine (Gotenberg Chromium / OpenXML Word / ClosedXML Excel)
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Canonical M2M Dual-Auth Claims Transformation Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-SEC-002]:
* [MUST] Hydrate `ClaimsPrincipal` onto `HttpContext.User` upon successful `X-API-Key` validation.
* [MUST] Assign `Role: M2MClient` and populate `Scope` and `ProjectId` claims.

```csharp
// Inside ProjectContextMiddleware.cs
if (context.Request.Headers.TryGetValue("X-API-Key", out var extractedKey) &&
    !string.IsNullOrWhiteSpace(extractedKey))
{
    var rawKey = extractedKey.ToString();
    var validatedKey = await validateApiKeyUseCase.ExecuteAsync(new ValidateApiKeyQuery(rawKey), context.RequestAborted);

    if (validatedKey == null)
    {
        await WriteUnauthorizedResponseAsync(context, "Invalid or inactive API Key.");
        return;
    }

    // 1. Hydrate Ambient Scoped Project Context
    var projectId = new ProjectId(validatedKey.ProjectId);
    projectContextSetter.SetProject(projectId, validatedKey.CallerApp);

    // 2. Hydrate ASP.NET Core ClaimsPrincipal to pass [Authorize] policies
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, validatedKey.Id.ToString()),
        new Claim(ClaimTypes.Role, "M2MClient"),
        new Claim("Scope", validatedKey.Scope),
        new Claim("ProjectId", validatedKey.ProjectId.ToString())
    };
    var identity = new ClaimsIdentity(claims, "ApiKeyScheme");
    context.User = new ClaimsPrincipal(identity);

    await next(context);
    return;
}
```

### 3.2 Canonical SSRF Pre-Flight ConnectionHostValidator Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-SEC-005]:
* [MUST] Inspect hostnames against blocked infrastructure list prior to opening network connections.
* [MUST] Resolve DNS addresses and block loopback, link-local, and private subnets.

```csharp
using System.Net;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Security;

public static class ConnectionHostValidator
{
    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "postgres", "minio", "redis", "gotenberg", "host.docker.internal"
    };

    public static void EnsureSafeTarget(string host)
    {
        if (BlockedHostnames.Contains(host))
            throw new SecurityViolationException($"Targeting internal infrastructure '{host}' is strictly forbidden.");

        var addresses = Dns.GetHostAddresses(host);
        foreach (var ip in addresses)
        {
            if (IPAddress.IsLoopback(ip) || ip.ToString().StartsWith("169.254.") || ip.ToString().StartsWith("10."))
                throw new SecurityViolationException($"Targeting private or link-local address '{ip}' is prohibited.");
        }
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-SEC-001]: Variable-Time String Equality on API Keys
```diff
- if (computedHash == storedKey.KeyHash) return true;
+ if (CryptographicOperations.FixedTimeEquals(
+     Encoding.UTF8.GetBytes(computedHash),
+     Encoding.UTF8.GetBytes(storedKey.KeyHash))) return true;
```
*Rationale: Standard string equality leaks timing information, allowing attackers to incrementally guess valid cryptographic hashes.*

### [TRAP-SEC-002]: In-Memory Byte Array Buffering Causing LOH Stalls
```diff
- byte[] pdfBytes = await gotenbergClient.RenderToBytesAsync(htmlPayload, ct);
- return File(pdfBytes, "application/pdf");
+ await using Stream pdfStream = await gotenbergClient.RenderToStreamAsync(htmlPayload, ct);
+ Response.Headers.ContentDisposition = "inline";
+ return File(pdfStream, "application/pdf");
```
*Rationale: `byte[]` allocations $\ge 85\text{KB}$ land directly on the Large Object Heap, triggering uncompacted memory fragmentation and Gen 2 GC pauses.*

### [TRAP-SEC-003]: HTTP 307 Redirect Leaking Internal MinIO Network
```diff
- var preSignedUrl = await storageService.GetDownloadUrlAsync(documentId);
- return Redirect(preSignedUrl);
+ var streamResult = await downloadUseCase.ExecuteAsync(new DownloadQuery(documentId), ct);
+ return File(streamResult.Stream, streamResult.ContentType, streamResult.FileName);
```
*Rationale: Redirecting to internal storage reveals private Docker topology and fails for external clients outside the container network.*
