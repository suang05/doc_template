# [CORE-003]: 03_security_and_performance.md — M2M Security, Tenant Isolation & High-Throughput Performance SSoT

<context>
Scope: Cross-Cutting Security Standards, High-Throughput Performance Limits, Zero-LOH Memory Thresholds (<=81,920 buffer), SSRF IP/Host Perimeter Validation, and Constant-Time Hashing.
SSoT_For: Performance Thresholds, Stream Buffer Limits (<=81,920 bytes), Constant-Time Comparison, and SSRF Network Boundary Invariants.
Out_Of_Scope: Concrete multi-tenant middleware execution and dual-auth pipeline (see: ../03_specialized_mechanics/multi_tenant_security_pipeline.md), Presentation layer routing and RFC 9457 error translation (see: ../01_clean_arch_patterns/04_api_presentation_layer.md).
Target: `backend-v2/` (`SmkDoc.Infrastructure/`, `SmkDoc.Api/Middleware/`, and `SmkDoc.Domain/Entities/`).
ArchitecturalSignals: [ARCH_EVOLUTION_SECURITY], [ARCH_EVOLUTION_RENDERING]
</context>

---

## 1. Level 1: Security & Performance Invariants Matrix

> [SSOT DELEGATION]: Multi-tenant dual-channel authentication (M2M/JWT), ClaimsPrincipal hydration, ambient project context, and API key quota governance are governed exclusively by `specs/03_specialized_mechanics/multi_tenant_security_pipeline.md`.

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) | Enforcement Point |
|---|---|---|---|
| **Cryptographic Equality** | ! Variable-time string comparison (`key == storedHash`) | >> Constant-time comparison: `CryptographicOperations.FixedTimeEquals` across all secret/hash validations | `ApiKeyValidator` / Crypto Services |
| **Large Object Heap (LOH)** | ! `MemoryStream.ToArray()`, `Task<byte[]>`, or in-memory buffer $\ge 85\text{KB}$ | >> Direct streaming pipe to `Response.Body` with bounded buffer $\le 81,920$ bytes | Render / Download Egress |
| **Download Egress** | ! HTTP 307 Redirects to internal MinIO S3 instances | >> Gateway reverse-proxy streaming via `File(stream, ...)` | `DocumentArtifactController` |
| **SSRF Host Defense** | ! Unchecked database connection hosts or loopback/metadata targets | >> Pre-flight DNS/IP validation via `ConnectionHostValidator`; enforce `READ ONLY` database transactions | Data Connection / Integration Services |
| **Chromium Cluster Protection** | ! Unthrottled render calls crashing Gotenberg pods | >> Dedicated rate limiter (`30 req/min`) + async batch queuing | Presentation Layer Gateway |
| **API Throttling & DDoS** | ! Unthrottled endpoints susceptible to volumetric DoS or brute-force | >> Declarative rate limiting via `[EnableRateLimiting("...")]` (`api`: 60/m, `auth-strict`: 5/m, `rendering`: 30/m, `test-conn`: 10/m) | Presentation Layer Controllers |
| **Non-Blocking I/O** | ! Synchronous blocking calls (`.Result`, `.Wait()`) on wire/database I/O | >> 100% `async`/`await` with forwarded `CancellationToken ct` | All Application & Infrastructure Services |

---

## 2. Level 2: Core Security AST & Cognitive Interceptor Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why Constant-Time Hash Comparison**: Standard string `==` terminates on the first mismatched byte, creating microsecond timing discrepancies that allow attackers to incrementally deduce valid cryptographic hashes via side-channel latency measurements.
* **Why 81,920-Byte Buffer Threshold**: Any memory allocation exceeding 85,000 bytes is placed on the Large Object Heap (LOH). The .NET Garbage Collector does not compact the LOH during Gen 0/1 sweeps, leading to Gen 2 GC pauses and server stalls under concurrent load.
* **Why Banning HTTP 307 Redirects to MinIO**: MinIO runs inside an isolated Docker network. Returning a 307 redirect exposes private hostnames (`http://minio:9000/...`), fails for external callers, and violates Zero-Trust perimeter boundaries.
* **Why Pre-Flight Host & Private Subnet Validation**: Attackers configuring custom database connections can target container host interfaces (`172.17.0.1`), loopback (`127.0.0.1`), or cloud instance metadata services (`169.254.169.254`) to extract credentials. Pre-flight DNS resolution and subnet blocking stops SSRF at the network edge.
* **Why Multi-Tier Declarative Rate Limiting**: Chromium HTML-to-PDF generation is CPU/memory intensive. Without endpoint rate limits, simultaneous rendering requests exhaust pod resources. Applying tiered limits protects both Gotenberg (`30/m`) and authentication routes (`5/m`) from resource exhaustion.

### 2.2 Cognitive Security & Performance Interceptors
```yaml
OptimizationObjectives:
  PrimaryObjective: Constant Memory Bounds, SSRF Immunity & Cryptographic Timing Safety
  TradeOffPriority: MemorySafety (Zero-LOH) > NetworkPerimeterIsolation > TimingSideChannelImmunity > HighThroughputAvailability

ProactiveSecurityGuards:
  - Scope: SmkDoc.Infrastructure
    Trigger: String equality check (== or .Equals()) on API key hashes or secrets
    CanonicalAction: Enforce CryptographicOperations.FixedTimeEquals on byte arrays >> Ref: [RULE-SEC-002]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Detection of in-memory byte[] allocation, MemoryStream.ToArray(), or buffer >= 85KB
    CanonicalAction: Pipe live Stream to HttpResponse.Body with bounded 81,920 buffer >> Ref: [RULE-PERF-LOH]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Returning HTTP 307 Redirect to internal MinIO storage URL
    CanonicalAction: Reject redirect; stream content directly through gateway using File(stream, ...) >> Ref: [RULE-SEC-REDIRECT]

  - Scope: SmkDoc.Infrastructure
    Trigger: External database connection targeting loopback, link-local, or container network alias
    CanonicalAction: Throw SecurityViolationException via ConnectionHostValidator >> Ref: [RULE-SEC-001]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Controller or endpoint exposed without declarative rate limiting policy
    CanonicalAction: Decorate with [EnableRateLimiting("...")] matching designated policy >> Ref: [RULE-PERF-001]

CoreExtensionSlots:
  RateLimitingSlot: Microsoft.AspNetCore.RateLimiting (auth-strict, rendering, test-conn, api)
  HostValidatorSlot: ConnectionHostValidator (SSRF DNS resolution and subnet filtering)
  StorageProviderSlot: IBucketStorageService (MinIO S3 / AWS S3 / Azure Blob Storage)
  RenderingEngineSlot: IRenderEngine (Gotenberg Chromium / OpenXML Word / ClosedXML Excel)
```

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Canonical SSRF Pre-Flight ConnectionHostValidator Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-SEC-001]:
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

### 3.2 Canonical Constant-Time Cryptographic Comparison Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-SEC-002]:
* [MUST] Compare secret hashes and token signatures using `CryptographicOperations.FixedTimeEquals` across byte representations.

```csharp
using System.Security.Cryptography;
using System.Text;

namespace SmkDoc.Infrastructure.Security;

public static class ConstantTimeVerifier
{
    public static bool VerifyHash(string computedHash, string storedHash)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(computedHash);
        var actualBytes = Encoding.UTF8.GetBytes(storedHash);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
```

### 3.3 Canonical Multi-Tier Rate Limiting Configuration Recipe
>> MANDATORY CANONICAL TEMPLATE [RULE-PERF-001]:
* [MUST] Register declarative rate limiting tiers in ASP.NET Core service configuration.
* [MUST] Decorate Controllers or endpoints using `[EnableRateLimiting("...")]` matching designated tier policies.

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SmkDoc.Api.Configuration;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("api", opt =>
            {
                opt.PermitLimit = 60;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.AddFixedWindowLimiter("rendering", opt =>
            {
                opt.PermitLimit = 30;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.AddFixedWindowLimiter("test-conn", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.AddFixedWindowLimiter("auth-strict", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
            });
        });

        return services;
    }
}
```

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
+ await using Stream result = await gotenbergClient.RenderToStreamAsync(htmlPayload, ct);
+ Response.Headers.ContentDisposition = "inline";
+ return File(result, "application/pdf");
```
*Rationale: `byte[]` allocations $\ge 85\text{KB}$ land directly on the Large Object Heap, triggering uncompacted memory fragmentation and Gen 2 GC pauses.*

### [TRAP-SEC-003]: HTTP 307 Redirect Leaking Internal MinIO Network
```diff
- var preSignedUrl = await storageService.GetDownloadUrlAsync(documentId);
- return Redirect(preSignedUrl);
+ var query = new DownloadQuery(documentId);
+ var result = await downloadUseCase.ExecuteAsync(query, ct);
+ return File(result.Stream, result.ContentType, result.FileName);
```
*Rationale: Redirecting to internal storage reveals private Docker topology and fails for external clients outside the container network.*
