# [CORE-003]: 03_security_and_performance.md — M2M Security, Tenant Isolation & High-Throughput Performance SSoT

<context>
Category: 00_architecture_core
Domain: Platform Security Perimeter, Zero-LOH Memory & Performance SLAs
SoleResponsibilities:
  - Platform-wide dual-channel security architecture (Channel A M2M API Keys vs Channel B Internal JWT)
  - Memory & GC boundaries: strictly bounded streaming (<= 81,920 bytes) guaranteeing Zero-LOH
  - SSRF elimination via private IP/loopback address validation (`ConnectionHostValidator`)
  - Cryptographic constant-time comparison for sensitive credentials and API key tokens
Target: `backend-v2/` (`SmkDoc.Infrastructure/`, `SmkDoc.Api/`, `SmkDoc.Domain/`)
Signal: [ARCH_EVOLUTION_SECURITY]
</context>

---

## 1. Level 1: Affirmative Security & Performance Invariants Matrix

>> SSoT DELEGATION: Multi-tenant dual-channel authentication (M2M/JWT), ClaimsPrincipal hydration, ambient project context, and API key quota governance are governed strictly by [multi_tenant_security_pipeline.md](../03_specialized_mechanics/multi_tenant_security_pipeline.md).

| Architectural Concern | Affirmative Security/Performance Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Cryptographic Timing** | Compare all secret hashes and token signatures using `CryptographicOperations.FixedTimeEquals` across byte representations. | Crypto Services |
| **Zero-LOH Memory** | Binary outputs stream directly to `HttpResponse.Body` using bounded buffers $\le 81,920$ bytes. Zero multi-MB in-memory buffering (`byte[]`, unbounded `MemoryStream`). | Egress / Rendering |
| **Gateway Egress** | Stream internal storage artifacts through gateway reverse-proxy via `File(stream, ...)`. Zero public HTTP 307 redirects to internal MinIO S3 instances. | Artifact Delivery |
| **SSRF Perimeter** | Validate external database hosts against private/loopback/cloud-metadata subnets via `IConnectionHostValidator` prior to connection. Enforce bitwise byte-level subnet checks covering RFC 1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), RFC 3927 (`169.254.0.0/16`), and IPv6 private addresses. Enforce `READ ONLY` transactions. | Data Integration |
| **Tiered Rate Limiting** | Protect all controllers and sensitive endpoints via declarative `[EnableRateLimiting("...")]` policies (`api`: 60/m, `rendering`: 30/m, `test-conn`: 10/m, `auth-strict`: 5/m). | Presentation |
| **Non-Blocking Concurrency** | 100% pure `async`/`await` pipeline forwarding `CancellationToken ct` on all database, network, and storage I/O. | Universal |

---

## 2. Level 2: Engineering Rationales & Cognitive Interceptors

### 2.1 Engineering Rationale & Causal Decisions
* **Why Constant-Time Hash Comparison**: Standard string `==` terminates on the first mismatched byte, creating microsecond timing discrepancies that allow attackers to incrementally deduce valid cryptographic hashes via side-channel latency measurements.
* **Why 81,920-Byte Buffer Threshold**: Any memory allocation exceeding 85,000 bytes is placed on the Large Object Heap (LOH). The .NET Garbage Collector does not compact the LOH during Gen 0/1 sweeps, leading to Gen 2 GC pauses and server stalls under concurrent load.
* **Why Banning HTTP 307 Redirects to MinIO**: MinIO runs inside an isolated Docker network. Returning a 307 redirect exposes private hostnames (`http://minio:9000/...`), fails for external callers, and violates Zero-Trust perimeter boundaries.
* **Why Pre-Flight Host & Private Subnet Validation**: Attackers configuring custom database connections can target container host interfaces (`172.17.0.1`), loopback (`127.0.0.1`), or cloud instance metadata services (`169.254.169.254`) to extract credentials. Pre-flight DNS resolution and subnet blocking stops SSRF at the network edge.

### 2.2 Proactive Security & Performance Firewalls
```yaml
LohAllocationFirewall:
  Invariant: "Output streaming must never allocate >= 85,000 bytes in memory."
  ActionOnViolation: "Replace byte[] and MemoryStream.ToArray() with bounded 81,920-byte Stream pass-through."

TimingSideChannelFirewall:
  Invariant: "Secret and API key comparisons must execute in constant time."
  ActionOnViolation: "Replace string == or .Equals() with CryptographicOperations.FixedTimeEquals."

SsrfPerimeterFirewall:
  Invariant: "Outbound connections must not resolve to internal, private, or loopback subnets."
  ActionOnViolation: "Enforce await hostValidator.ValidateHostAsync(host, ct) before establishing network transport."

InternalTopologyFirewall:
  Invariant: "Gateway must never expose private storage cluster endpoints."
  ActionOnViolation: "Replace HTTP 307 redirects with gateway reverse-proxy streaming."
```

---

## 3. Level 3: Canonical Working Archetypes (Surgical AST Recipes)

### 3.1 Canonical Zero-LOH Bounded Streaming Recipe
>> MANDATORY CANONICAL BLUEPRINT [RULE-PERF-001]:
* Binary output streaming must consume bounded 81,920-byte buffers to prevent LOH fragmentation.

```csharp
public sealed class BoundedStreamResult(Stream sourceStream, string contentType, string? fileName = null) : IActionResult
{
    private const int BoundedBufferSize = 81_920; // Exact threshold: <= 85,000 bytes prevents LOH Gen 2 GC pauses

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = contentType;

        if (!string.IsNullOrEmpty(fileName))
        {
            response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        }

        // Direct bounded pass-through to wire without in-memory buffering
        await sourceStream.CopyToAsync(response.Body, BoundedBufferSize, context.HttpContext.RequestAborted);
        await sourceStream.DisposeAsync();
    }
}
```

### 3.2 Canonical Constant-Time Cryptographic Comparison Recipe
>> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-002]:
* Secret hashes and token signatures must be compared in constant time across byte representations.

```csharp
public static class ConstantTimeVerifier
{
    public static bool VerifyHash(string computedHash, string storedHash)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(computedHash);
        var actualBytes = Encoding.UTF8.GetBytes(storedHash);

        // Constant-time comparison: prevents side-channel timing attacks
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
```

### 3.3 Canonical SSRF Pre-Flight ConnectionHostValidator Recipe
>> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-001]:
* Inspect hostnames against blocked infrastructure list and resolve DNS asynchronously using injectable `IDnsResolver`.
* Enforce bitwise subnet filtering (`GetAddressBytes()`) across all RFC 1918 private subnets, link-local addresses, and IPv6 unique local networks.

```csharp
public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken ct = default);
}

public interface IConnectionHostValidator
{
    Task ValidateHostAsync(Uri uri, CancellationToken ct = default);
    Task ValidateHostAsync(string host, CancellationToken ct = default);
}

public sealed class ConnectionHostValidator(
    IDnsResolver dnsResolver,
    ILogger<ConnectionHostValidator> logger) : IConnectionHostValidator
{
    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "postgres", "minio", "redis", "gotenberg", "host.docker.internal"
    };

    public Task ValidateHostAsync(Uri uri, CancellationToken ct = default) =>
        ValidateHostAsync(Guard.NotNull(uri, nameof(uri)).Host, ct);

    public async Task ValidateHostAsync(string host, CancellationToken ct = default)
    {
        Guard.NotBlank(host, nameof(host));

        if (BlockedHostnames.Contains(host))
        {
            throw new SecurityViolationException($"Targeting internal infrastructure '{host}' is strictly forbidden.");
        }

        var addresses = await dnsResolver.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0)
        {
            throw new SecurityViolationException($"Host '{host}' could not be resolved.");
        }

        foreach (var ip in addresses)
        {
            if (IsPrivateOrReserved(ip))
            {
                logger.LogWarning("SSRF attempt blocked: Host '{Host}' resolved to private/reserved IP '{Ip}'", host, ip);
                throw new SecurityViolationException($"Targeting private, internal, or link-local address '{ip}' is prohibited.");
            }
        }
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal) return true;

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 0.0.0.0/8 (Current network)
            if (bytes[0] == 0) return true;
            // 10.0.0.0/8 (RFC 1918 Private)
            if (bytes[0] == 10) return true;
            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127) return true;
            // 169.254.0.0/16 (RFC 3927 Link-Local / Cloud Metadata 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 172.16.0.0/12 (RFC 1918 Private - covers 172.16.0.0 through 172.31.255.255 & Docker 172.17.x.x)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16 (RFC 1918 Private)
            if (bytes[0] == 192 && bytes[1] == 168) return true;
        }

        return false;
    }
}
```

### 3.4 Canonical Multi-Tier Rate Limiting Configuration Recipe
>> MANDATORY CANONICAL BLUEPRINT [RULE-PERF-002]:
* Register declarative rate limiting tiers and enforce designated policies on controllers.

```csharp
public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("api", opt => { opt.PermitLimit = 60; opt.Window = TimeSpan.FromMinutes(1); });
            options.AddFixedWindowLimiter("rendering", opt => { opt.PermitLimit = 30; opt.Window = TimeSpan.FromMinutes(1); });
            options.AddFixedWindowLimiter("test-conn", opt => { opt.PermitLimit = 10; opt.Window = TimeSpan.FromMinutes(1); });
            options.AddFixedWindowLimiter("auth-strict", opt => { opt.PermitLimit = 5; opt.Window = TimeSpan.FromMinutes(1); });
        });

        return services;
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

### [TRAP-SEC-004]: Incomplete String-Based SSRF Filtering and Static Network I/O
```diff
- public static class ConnectionHostValidator {
-     public static void EnsureSafeTarget(string host) {
-         var addresses = Dns.GetHostAddresses(host); // Real Network I/O breaks Unit Tests
-         if (ip.ToString().StartsWith("10.")) ... // FAILS: Misses 172.16.0.0/12 (Docker) and 192.168.0.0/16
-     }
- }
+ public sealed class ConnectionHostValidator(IDnsResolver resolver, ILogger<ConnectionHostValidator> logger) : IConnectionHostValidator {
+     public async Task ValidateHostAsync(string host, CancellationToken ct = default) {
+         var addresses = await resolver.GetHostAddressesAsync(host, ct);
+         // Bitwise byte matching covers all RFC 1918, RFC 3927, and IPv6 private subnets
+     }
+ }
```
*Rationale: String prefix checks like `.StartsWith("10.")` completely miss Docker subnets (`172.17.x.x`), internal LANs (`192.168.x.x`), and IPv6 private addresses, enabling critical SSRF attacks against internal infrastructure. Furthermore, static DNS calls prevent 100% in-memory unit testing.*
