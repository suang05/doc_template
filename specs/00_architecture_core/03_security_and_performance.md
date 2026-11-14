# [CORE-003]: 03_security_and_performance.md — Enterprise Security, Egress Controls, Bounded Memory & Performance SLOs

<context>
Category: 00_architecture_core
Domain: Platform Security Perimeter, Untrusted Input Isolation, Bounded Pooled Memory & Performance SLOs
SoleResponsibilities:
  - Enterprise security domains (API keys, JWT, secrets/KMS, encryption, TLS/mTLS, headers, PDPA retention, container/supply-chain hardening)
  - Untrusted-input controls (DOCX/XLSX zip-bomb/XXE, Gotenberg Chromium isolation, tenant SQL read-only execution, structured connection strings)
  - Outbound SSRF protection (`IConnectionHostValidator`) with IPv4-mapped IPv6 normalization, platform deny-lists, per-tenant egress CIDR allow-lists and IP pinning
  - Bounded, pooled memory management (64 KB buffers, `HybridSpoolStream`, `RecyclableMemoryStream`, spool-before-commit truncation prevention)
  - Partitioned, distributed rate limiting and Chromium concurrency backpressure
  - Service-Level Objectives (SLOs) and performance budgets
Out of scope (owned elsewhere): multi-tenant 3-tier guard pipeline (`multi_tenant_security_pipeline.md`), wire status table (06).
Target: `backend-v2/` (`SmkDoc.Application/`, `SmkDoc.Infrastructure/`, `SmkDoc.Api/`)
Platform baseline: declared in `Directory.Build.props` (ADR-01).
Signal: [ARCH_EVOLUTION_SECURITY]
</context>

---

## 1. Level 1: Security & Performance Invariants Matrix

> SSoT DELEGATION: Multi-tenant dual-channel authentication (`ApiKey` / `Jwt`), `TenantResolutionMiddleware`, ambient `ICurrentProjectContext`, and Zero Standing Access are governed by [multi_tenant_security_pipeline.md](../03_specialized_mechanics/multi_tenant_security_pipeline.md) and [01_system_architecture.md §1.1](01_system_architecture.md).

### 1.1 Core Security & Performance Invariants
| Concern | Invariant (positive SSoT law) | Enforcement |
|---|---|---|
| **API key cryptography** | Keys follow `smk_live_{keyId}_{secret}` (≥ 128 bits entropy). Lookup by indexed `keyId`; compute `HMAC-SHA256(kmsPepper, secret)` and compare raw digest bytes via `CryptographicOperations.FixedTimeEquals(ReadOnlySpan<byte>, ReadOnlySpan<byte>)`. Support scopes, expiry, dual-key rotation and immediate revocation. | Unit + security tests |
| **Bounded, pooled memory** | Stream copy buffers use `65_536` bytes (64 KB, power-of-two friendly for `ArrayPool<byte>.Shared`). Seekable package formats (DOCX, XLSX) and Pipeline A outputs use `HybridSpoolStream` (64 KB pooled RAM, spilling to an encrypted/ephemeral `DeleteOnClose` temp file capped by `MaxArtifactSizeBytes`) or `RecyclableMemoryStream`. Unbounded `MemoryStream.ToArray()` and multi-MB `byte[]` allocations are prohibited. | BannedApiAnalyzers, benchmark gate |
| **Truncation-free streaming** | Non-seekable upstream renders (Gotenberg) are spooled before sending `200 OK` headers so mid-render failures return `502`/`504 ProblemDetails` rather than a truncated `200` PDF, and responses carry exact `Content-Length` and `SHA-256` checksums. | Integration tests |
| **Artifact delivery (ADR)** | Default: stream artifacts through the API gateway via `File(stream, contentType, fileDownloadName, enableRangeProcessing: stream.CanSeek)` with `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`. Direct `307` redirects to internal MinIO hostnames are prohibited. (If bandwidth offload is required for large archives, a short-TTL ≤ 60 s single-use presigned URL via a public reverse-proxied S3 edge is an explicit ADR opt-in.) | NetArchTest + integration tests |
| **Outbound SSRF guard** | Primary control is network-layer egress policy (Kubernetes `NetworkPolicy` / firewall). Defense-in-depth `IConnectionHostValidator.ResolveAndValidateAsync` normalizes IPv4-mapped IPv6, enforces allowed ports, checks platform deny-list CIDRs and per-tenant approved egress CIDR allow-lists (`IPNetwork`), returns one generic error (`SecurityViolationException.EgressDenied()`), and pins the resolved `IPAddress` at connect time to defeat DNS rebinding. | Unit tests with fake `IDnsResolver` |
| **Partitioned rate limiting** | Rate limiters are partitioned by caller identity (`apiKeyId` \| `projectId` \| `userId`, fallback `clientIp`), backed by Redis (or gateway) across replicas, with a `GlobalLimiter` default, a concurrency limiter for rendering, and an `OnRejected` writer emitting RFC 9457 `ProblemDetails` (`429 RATE_LIMITED`) + `Retry-After`. | Integration tests |
| **Short DB transactions** | Database transactions never span external network or rendering I/O (Gotenberg, MinIO, tenant databases). Reads happen outside the transaction; `IUnitOfWork.CommitAsync(ct)` runs only after external I/O has completed. | Code review + latency gate |

### 1.2 Untrusted-Input Attack Surface & Mandatory Controls
| Attack Surface | Threat Vector | Mandatory Control |
|---|---|---|
| **Uploaded DOCX / XLSX templates** | Zip bomb, XML External Entity (XXE), macros (`.docm`/`.xlsm`), external relationship SSRF | Verify magic bytes and content type; reject macro-enabled formats and external relationships; cap compressed size (≤ 10 MB), uncompressed size (≤ 50 MB), compression ratio (≤ 100:1) and zip entry count (≤ 1,000); parse XML with `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }`. |
| **Monaco HTML sent to Gotenberg** | SSRF to cloud metadata (`169.254.169.254`) or internal services (`minio`, `redis`, `postgres`), local file read (`file://`), runaway JS CPU | Run Gotenberg in an isolated network namespace with **zero internet and zero internal cluster egress**; pass `--chromium-disable-javascript=true` (unless explicitly required by a sandboxed feature) and `--chromium-deny-list` blocking `file://` and non-allow-listed origins; HTML-encode bound data values in `DataBindingEngine` (`HtmlEncoder.Default`); enforce max HTML size (≤ 2 MB) and hard render timeout (15 s). |
| **Tenant `DatasetQuery` SQL** | Arbitrary SQL execution, data modification, slow-query DoS, memory exhaustion | Connect using a least-privilege read-only database role; open every session with `BEGIN TRANSACTION READ ONLY` (PostgreSQL `SET LOCAL default_transaction_read_only = on`); enforce `CommandTimeout` (≤ 5 s), max rows (≤ 10,000), max result bytes (≤ 16 MB), and single-statement validation. |
| **Tenant `ConnectionStringEncrypted`** | Connection-string option injection (local file read, custom plugins, extra `Host=`), plaintext credential leak | Store structured fields (`Host`, `Port`, `Database`, `Username`, `EncryptedPassword`); construct the driver string strictly with `DbConnectionStringBuilder` and an allow-list of safe keywords; encrypt credentials with **AES-256-GCM envelope encryption** (KMS/Vault master key + per-record DEK); never log or echo connection strings. |
| **JSON payloads (M2M / Studio)** | Deeply nested JSON stack overflow, large-payload memory exhaustion | Kestrel `MaxRequestBodySize` (default 10 MB, per-endpoint override), `JsonSerializerOptions { MaxDepth = 32 }`, fail-fast JSON Schema validation. |

### 1.3 Enterprise Security Domains Matrix (OWASP ASVS 4.0 / ISO 27001 / Thai PDPA)
| Domain | Standard & Implementation Requirement |
|---|---|
| **Authentication (Portal JWT & Passwords)** | Portal passwords hashed with Argon2id or BCrypt (`workFactor >= 12`); login verifies against a precomputed dummy hash of identical work factor when the email is unknown. Access JWTs are short-lived (≤ 15 min), validated with strict `Issuer`, `Audience`, `Lifetime` and `ValidAlgorithms = [SecurityAlgorithms.RsaSha256]` (or `EcdsaSha256`). Refresh tokens use rotation with reuse detection. Browser sessions keep tokens in `httpOnly; Secure; SameSite=Lax` cookies via the Next.js server proxy; tokens in `localStorage` are prohibited. |
| **Secrets Management** | Zero secrets, API peppers, JWT signing keys or connection passwords in source control, container images or plain `appsettings.json`. Secrets are injected from KMS / Vault / Kubernetes External Secrets and rotated without downtime. |
| **Encryption in Transit & at Rest** | TLS 1.2+ (prefer TLS 1.3) on all external and inter-service links; optional mTLS for enterprise banking M2M callers. PostgreSQL storage and backups encrypted at rest; MinIO buckets use server-side encryption (`SSE-KMS` / `SSE-S3`). |
| **HTTP Security Headers & CORS** | Emit `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` (or `CSP: frame-ancestors 'self'` for Studio preview), `Referrer-Policy: no-referrer`, and `Cache-Control: private, no-store` on all tenant responses. CORS origins are explicitly allow-listed from configuration (never `*` with credentials). |
| **Security Audit & Tamper Evidence** | Security events (login failure, API key creation/revocation, egress denied, rate-limit rejection, break-glass activation, cross-tenant plane rejection) are logged with structured fields (`projectId`, `actorId`, `apiKeyId`, `traceId`, `errorCode`) and never include secrets or raw payloads. Production audit archives in MinIO support S3 Object Lock (WORM) when required by banking compliance. |
| **PII & Data Retention (Thai PDPA)** | Generated documents (`outputs/`) and audit payloads (`audit-payloads/`) follow per-project configurable retention TTLs; `DocumentRetentionWorker` purges expired objects and scrubs PII metadata on schedule or upon tenant offboarding (`Purge`). |
| **Container & Supply-Chain Hardening** | Containers run as non-root UID, read-only root filesystem (with bounded `/tmp` tmpfs for spooling), dropped Linux capabilities (`ALL`), and pinned base image digests. CI runs `dotnet list package --vulnerable`, SAST (CodeQL/Sonar), container vulnerability scanning (Trivy) and SBOM generation. |

### 1.4 Service-Level Objectives (SLOs) & Performance Budgets
| Pipeline / Operation | Latency Target (p95) | Latency Target (p99) | Hard Timeout | Max Payload / Output Size | Concurrency / Throughput Budget |
|---|---|---|---|---|---|
| **Pipeline A: Sync PDF Generation** | ≤ 1,500 ms | ≤ 3,000 ms | 15 s | 10 MB payload / 25 MB output | Bounded by Gotenberg concurrency limiter |
| **Pipeline A: Sync DOCX / XLSX** | ≤ 800 ms | ≤ 1,500 ms | 10 s | 10 MB payload / 25 MB output | CPU-bound pool; `RecyclableMemoryStream` |
| **Pipeline B: Studio Live Preview** | ≤ 800 ms | ≤ 1,500 ms | 10 s | 2 MB HTML / 10 MB PDF | Debounced 800 ms on client; per-project concurrency limit |
| **Pipeline C: Enqueue Document Job** | ≤ 100 ms | ≤ 250 ms | 5 s | ≤ 500 items per batch | Durable PostgreSQL insert + outbox |
| **Data / Control Plane CRUD & Queries**| ≤ 80 ms | ≤ 200 ms | 5 s | 1 MB JSON body | ReadModels use `AsNoTracking` + SQL projection |
| **Tenant External SQL Dataset Fetch** | ≤ 1,000 ms | ≤ 2,500 ms | 5 s per query | ≤ 10,000 rows / 16 MB | Max 4 parallel dataset queries per request (`Parallel.ForEachAsync`) |

Runtime resource configuration: Server GC enabled (`ServerGarbageCollection=true`), container memory limit paired with `DOTNET_GCHeapHardLimitPercent=0x4B` (75%), `SocketsHttpHandler.PooledConnectionLifetime = TimeSpan.FromMinutes(2)`, and Npgsql connection pool sized so `replicas × max_pool_size < postgres_max_connections`.

---

## 2. Level 2: Engineering Rationales & Security/Performance Firewalls

### 2.1 Engineering Rationales
* **Why "Bounded, Pooled Memory" instead of "Zero-LOH Guarantees":** In .NET, objects ≥ 85,000 bytes are allocated on the Large Object Heap (LOH), which is collected only during Gen 2 GCs. However, claiming "100% Zero-LOH" is technically inaccurate because (a) `ArrayPool<byte>.Shared.Rent(81_920)` rounds up to the next power-of-two bucket (`131_072` bytes, which resides on the LOH—though pooled and reused), and (b) OpenXML (DOCX) and ClosedXML (XLSX) require seekable package structures. Using **`65_536`-byte (64 KB) copy buffers**, `RecyclableMemoryStream`, and `HybridSpoolStream` (64 KB in-memory pooled array, spilling larger payloads to a `DeleteOnClose` temp file) keeps heap allocations bounded and pooled under concurrent load.
* **Why Spool-Before-Commit Prevents Truncated 200 Responses:** Once ASP.NET Core writes the first byte to `HttpResponse.Body`, the `200 OK` status header is sent on the wire and cannot be changed. If an upstream Gotenberg stream fails mid-transfer, the M2M caller receives a truncated PDF with a `200 OK` status. Spooling the rendered stream into `HybridSpoolStream` first ensures that rendering succeeded, provides the exact `Content-Length` and `SHA-256` digest, and allows MinIO archival before committing `200 OK`.
* **Why `prefix_keyId_secret` + HMAC-SHA256 + `FixedTimeEquals`:** An API key must be looked up by a non-secret `keyId` index in $O(1)$ time, then its secret verified by computing `HMAC-SHA256(pepper, secretBytes)` (where the pepper lives in KMS, not the database) and comparing the raw 32-byte spans with `CryptographicOperations.FixedTimeEquals`. Comparing raw byte spans avoids string allocations and eliminates timing side-channels.
* **Why Platform Deny-List + Per-Tenant Egress Allow-List + IP Pinning:** Enterprise ERP and Core Banking databases frequently reside on private RFC 1918 networks reachable via VPN/DirectConnect. A hard-coded blanket RFC 1918 block breaks legitimate customer integrations, whereas a naive hostname check is bypassed by IPv4-mapped IPv6 (`::ffff:10.0.0.1`) or DNS rebinding (returning a public IP on validation and `169.254.169.254` on connect). The validator therefore normalizes IPv6-mapped addresses, blocks platform-internal and special-use CIDRs unconditionally, checks per-tenant approved egress CIDRs for private networks, and returns the resolved `IPAddress` list so the transport connects directly to the validated IP.
* **Why Partitioned & Distributed Rate Limiting + Concurrency Bulkheads:** A fixed-window limiter without a partition key puts all tenants into one bucket, allowing one noisy caller to starve the entire platform or lock out all logins (`auth-strict`). Partitioning by credential/tenant/IP isolates callers, while a concurrency limiter on `rendering` protects Gotenberg Chromium pods from memory exhaustion.

### 2.2 Proactive Security & Performance Firewalls
```yaml
BoundedMemoryFirewall:
  Invariant: "Binary outputs use 65,536-byte pooled buffers and HybridSpoolStream/RecyclableMemoryStream; unbounded MemoryStream.ToArray() and whole-file byte[] are prohibited."
  Enforcement: "BannedApiAnalyzers + memory allocation benchmark gate in CI."

StreamOwnershipFirewall:
  Invariant: "Streams passed to File(stream, ...) are owned and disposed by FileStreamResult; never wrap them in await using inside the controller action."
  Enforcement: "Code review + integration test on download/generate endpoints."

OutboundEgressFirewall:
  Invariant: "All connections to tenant-supplied hosts must call IConnectionHostValidator.ResolveAndValidateAsync and pin the returned IP address."
  Enforcement: "BannedApiAnalyzers (Dns.GetHostAddresses) + unit test suite covering IPv4-mapped IPv6, CIDR boundaries and DNS rebinding."

PartitionedRateLimitFirewall:
  Invariant: "Rate limiters must partition by tenant/credential/IP and emit RFC 9457 ProblemDetails (429 RATE_LIMITED) with Retry-After."
  Enforcement: "Integration tests verifying per-tenant isolation and 429 headers."

UntrustedInputFirewall:
  Invariant: "XML parsers prohibit DTD/external entities; archive readers enforce entry and uncompressed byte caps; Gotenberg runs without network egress; tenant SQL runs in READ ONLY transactions with statement timeouts."
  Enforcement: "Semgrep rules (XmlReaderSettings, FromSqlRaw) + security integration tests."

ShortTransactionFirewall:
  Invariant: "Database transactions must never be held open across Gotenberg, MinIO or external SQL network calls."
  Enforcement: "Architecture review + connection-hold duration metric alert."
```

---

## 3. Level 3: Canonical Working Archetypes

### 3.1 Safe Artifact Streaming & `HybridSpoolStream` Recipe
> MANDATORY CANONICAL BLUEPRINT [RULE-PERF-001]:
* Use `65_536`-byte (64 KB) buffers so `ArrayPool<byte>.Shared` rents stay within the 64 KB bucket (< 85,000-byte LOH threshold).
* Pass stream ownership to ASP.NET Core's built-in `FileStreamResult` (`File(stream, contentType, fileDownloadName, enableRangeProcessing: stream.CanSeek)`), which streams in 64 KB chunks, encodes `Content-Disposition` per RFC 6266, and disposes `stream` when the response finishes.

```csharp
[HttpGet("{id:guid}/content")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken ct)
{
    // Step 1: Query -> use case returns an open stream, content type and sanitized file name
    var query = new DownloadDocumentQuery(new DocumentId(id));
    var result = await downloadDocumentUseCase.ExecuteAsync(query, ct);

    // Step 2: Security headers for tenant artifacts <Why: prevent shared caching and MIME sniffing>
    Response.Headers.CacheControl = "private, no-store";
    Response.Headers.XContentTypeOptions = "nosniff";

    // Step 3: Hand stream ownership to FileStreamResult <Why: never 'await using' a stream returned to File(...)>
    return File(result.ContentStream, result.ContentType, result.FileName, enableRangeProcessing: result.ContentStream.CanSeek);
}
```

### 3.2 API Key Cryptographic Verification Recipe
> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-002]:
* Compare raw digest bytes using `CryptographicOperations.FixedTimeEquals(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` over `HMACSHA256`.

```csharp
public sealed class ApiKeyVerifier(IOptions<SecurityOptions> options) : IApiKeyVerifier
{
    private readonly byte[] _pepperBytes = Convert.FromBase64String(options.Value.ApiKeyPepperBase64);

    public bool VerifySecret(ReadOnlySpan<char> providedSecret, ReadOnlySpan<byte> storedKeyHash)
    {
        // <Why: bound stackalloc size against untrusted header length to prevent stack overflow DoS>
        if (providedSecret.Length is 0 or > 256 || storedKeyHash.Length != HMACSHA256.HashSizeInBytes)
            return false;

        Span<byte> secretUtf8 = stackalloc byte[Encoding.UTF8.GetMaxByteCount(providedSecret.Length)];
        int bytesWritten = Encoding.UTF8.GetBytes(providedSecret, secretUtf8);

        Span<byte> computedHash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(_pepperBytes, secretUtf8[..bytesWritten], computedHash);

        // <Why: constant-time span comparison with zero heap allocations>
        return CryptographicOperations.FixedTimeEquals(computedHash, storedKeyHash);
    }
}
```

### 3.3 Outbound SSRF Guard with IPv4-Mapped IPv6 Normalization & IP Pinning
> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-001]:
* `IConnectionHostValidator` lives in `SmkDoc.Application/Abstractions`.
* Normalizes IPv4-mapped IPv6 addresses (`::ffff:x.x.x.x`), checks platform deny-list CIDRs (`IPNetwork`), allows per-tenant approved private CIDRs when configured by SuperAdmin, emits a single generic error (`EGRESS_DENIED`), and returns the validated `IPAddress` list for connection pinning.

```csharp
public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken ct);
}

public interface IConnectionHostValidator
{
    Task<IReadOnlyList<IPAddress>> ResolveAndValidateAsync(string host, int port, ProjectId projectId, CancellationToken ct);
}

public sealed class ConnectionHostValidator(
    IDnsResolver dnsResolver,
    IOptions<EgressPolicyOptions> options,
    ILogger<ConnectionHostValidator> logger) : IConnectionHostValidator
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAndValidateAsync(
        string host,
        int port,
        ProjectId projectId,
        CancellationToken ct)
    {
        Guard.NotNullOrWhiteSpace(host, nameof(host));
        var policy = options.Value;

        // Step 1: Port allow-list guard <Why: prevent internal port scanning via tenant connection configs>
        if (!policy.AllowedPorts.Contains(port))
        {
            logger.LogWarning("Egress port {Port} denied for project {ProjectId}.", port, projectId);
            throw SecurityViolationException.EgressDenied();
        }

        // Step 2: Bounded DNS resolution
        using var dnsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        dnsCts.CancelAfter(policy.DnsTimeout);
        var addresses = await dnsResolver.GetHostAddressesAsync(host, dnsCts.Token);

        // Step 3: Validate every returned A/AAAA record <Why: an attacker may mix one private IP into public records>
        if (addresses.Length == 0 || addresses.Any(ip => IsDenied(ip, policy, projectId)))
        {
            logger.LogWarning("Egress host {Host} denied for project {ProjectId}.", host, projectId);
            throw SecurityViolationException.EgressDenied(); // Single generic error; never echo host or IP to caller
        }

        // Step 4: Return validated IPs so caller pins the connection (anti-DNS-rebinding) while validating TLS SAN against original hostname
        return addresses;
    }

    private static bool IsDenied(IPAddress address, EgressPolicyOptions policy, ProjectId projectId)
    {
        // <Why: close IPv4-mapped IPv6 bypass such as ::ffff:127.0.0.1 or ::ffff:169.254.169.254>
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        // Platform-internal networks (loopback, link-local/metadata, cluster pod/service CIDRs) are unconditionally denied
        if (IPAddress.IsLoopback(normalized)
            || normalized.IsIPv6LinkLocal
            || normalized.IsIPv6SiteLocal
            || policy.PlatformDeniedNetworks.Any(network => network.Contains(normalized)))
        {
            return true;
        }

        // Per-tenant approved private CIDRs (e.g. customer VPN/DirectConnect ERP subnet)
        if (policy.TenantAllowedNetworks.TryGetValue(projectId.Value, out var tenantNetworks)
            && tenantNetworks.Any(network => network.Contains(normalized)))
        {
            return false;
        }

        // Default-deny all remaining RFC 1918 / RFC 6598 / special-use ranges
        return policy.DefaultPrivateNetworks.Any(network => network.Contains(normalized));
    }
}
```

### 3.4 Partitioned Rate Limiting & Concurrency Backpressure Recipe
> MANDATORY CANONICAL BLUEPRINT [RULE-PERF-002]:
* Partition rate limiters by caller key (`apiKeyId` / `projectId` / `userId` / `clientIp`) so one tenant never starves another.
* Emit RFC 9457 `ProblemDetails` (`429 RATE_LIMITED`) with a `Retry-After` header on rejection.

```csharp
public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitRejectionWriter.WriteProblemDetailsAsync; // writes ProblemDetails + Retry-After + ErrorCode: RATE_LIMITED

            // Global default: token bucket partitioned per authenticated credential/project, fallback client IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: RateLimitPartitionKeyResolver.ResolveCallerKey(httpContext),
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.ApiPermitLimit,
                        TokensPerPeriod = settings.ApiPermitLimit,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Rendering backpressure: bounded concurrent renders per project
            options.AddPolicy("rendering", httpContext =>
                RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: RateLimitPartitionKeyResolver.ResolveProjectKey(httpContext),
                    factory: _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = settings.MaxConcurrentRendersPerProject,
                        QueueLimit = settings.RenderQueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Connectivity checks: sliding window per project
            options.AddPolicy("connectivity-check", httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: RateLimitPartitionKeyResolver.ResolveProjectKey(httpContext),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = settings.ConnectivityCheckPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4
                    }));

            // Auth endpoints: sliding window per client IP
            options.AddPolicy("auth-strict", httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: RateLimitPartitionKeyResolver.ResolveClientIp(httpContext),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6
                    }));
        });

        return services;
    }
}
```

---

## 4. Level 4: Anti-Patterns & Unified Micro-Diffs

### [TRAP-SEC-001]: String Equality and Heap Allocations on API Key Verification
```diff
- if (computedHash == storedKey.KeyHash) return true;
+ if (apiKeyVerifier.VerifySecret(providedSecret, storedKey.KeyHashBytes)) return true;
```
*Rationale: Standard `==` short-circuits on the first mismatched byte and hex-string comparisons allocate unnecessarily. Lookup by `keyId` and compare raw HMAC-SHA256 digest spans via `CryptographicOperations.FixedTimeEquals`.*

### [TRAP-SEC-002]: Disposing a Stream Before `FileStreamResult` Executes (or Buffering `byte[]`)
```diff
- byte[] pdfBytes = await gotenbergClient.RenderToBytesAsync(htmlPayload, ct);
- return File(pdfBytes, "application/pdf");
- // OR:
- await using Stream stream = await downloadUseCase.ExecuteAsync(query, ct); // BUG: disposes before MVC reads the stream!
- return File(stream, "application/pdf");
+ var result = await downloadDocumentUseCase.ExecuteAsync(query, ct);
+ Response.Headers.CacheControl = "private, no-store";
+ Response.Headers.XContentTypeOptions = "nosniff";
+ return File(result.ContentStream, result.ContentType, result.FileName, enableRangeProcessing: result.ContentStream.CanSeek);
```
*Rationale: `byte[]` buffers whole documents in memory, while `await using` inside a controller action disposes the stream before ASP.NET Core writes the response body (`ObjectDisposedException`). `FileStreamResult` owns and disposes the stream when transmission completes.*

### [TRAP-SEC-003]: HTTP 307 Redirect Exposing Internal Storage Topology
```diff
- var preSignedUrl = await storageService.GetDownloadUrlAsync(documentId);
- return Redirect(preSignedUrl);
+ var result = await downloadDocumentUseCase.ExecuteAsync(new DownloadDocumentQuery(documentId), ct);
+ return File(result.ContentStream, result.ContentType, result.FileName);
```
*Rationale: Redirecting callers to internal MinIO URLs (`http://minio:9000/...`) leaks private container hostnames, bypasses gateway audit/headers, and fails outside the internal network.*

### [TRAP-SEC-004]: SSRF Bypass via IPv4-Mapped IPv6, DNS Rebinding, or Error Oracle
```diff
- if (ip.AddressFamily == AddressFamily.InterNetwork && bytes[0] == 10)
-     throw new SecurityViolationException($"Targeting private IP '{ip}' on host '{host}' is prohibited.");
+ var validatedIps = await hostValidator.ResolveAndValidateAsync(host, port, projectId, ct); // normalizes ::ffff:x.x.x.x, checks CIDRs, generic error
+ await using var connection = sqlDriver.CreatePinnedConnection(validatedIps[0], port, credentials);
```
*Rationale: Checking only `AddressFamily.InterNetwork` misses IPv4-mapped IPv6 addresses (`::ffff:10.0.0.1` and `::ffff:169.254.169.254`), resolving DNS twice enables DNS rebinding, and echoing resolved IPs in error messages gives attackers an internal network scanner.*

### [TRAP-SEC-005]: Unpartitioned Global Rate Limiter Starving All Tenants
```diff
- options.AddFixedWindowLimiter("api", opt => { opt.PermitLimit = 60; opt.Window = TimeSpan.FromMinutes(1); });
+ options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
+     RateLimitPartition.GetTokenBucketLimiter(RateLimitPartitionKeyResolver.ResolveCallerKey(ctx), _ => ...));
```
*Rationale: An unpartitioned limiter shares one counter across all tenants and callers, allowing a single noisy client to exhaust the quota or lock out login for the entire platform.*
