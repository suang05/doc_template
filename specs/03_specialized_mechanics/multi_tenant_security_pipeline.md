# [MECH-001]: multi_tenant_security_pipeline.md — Multi-Tenant Security & Dual-Auth Pipeline SSoT

<context>
Category: 03_specialized_mechanics
Domain: Multi-Tenant Security Pipeline, Dual-Channel Authentication & Context Hydration
SoleResponsibilities:
  - Edge dual-channel authentication pipeline (M2M Channel A SHA-256 API Key vs Channel B Internal JWT)
  - `ProjectContextMiddleware` and ambient `ICurrentProjectContext` hydration
  - The 3-Tier Guard Model SSoT, Plane Zoning (Control vs Data Plane) & Ambient Tenancy Fail-Closed Isolation
  - ClaimsPrincipal claims transformation (`ClaimsPrincipalBuilder`)
  - Project sub-key quota enforcement (maximum 2 active keys per project) and ownership transfer
Target: `backend-v2/` (`SmkDoc.Api/Middleware/`, `SmkDoc.Infrastructure/Security/`, `SmkDoc.Application/Common/Interfaces/`)
Signal: [ARCH_EVOLUTION_TENANCY]
</context>

---

## 1. Level 1: Multi-Tenant & Dual-Auth Invariants Matrix

### 1.1 The Unified Tenancy & Security Governance Spine (Master SSoT Chain)
The system's multi-tenant architecture is governed by a single, unbroken linear pipeline operating from macro-zoning down to database row filters:

```mermaid
flowchart TD
    subgraph Macro ["1. Macro Architecture: Zoning Strategy"]
        direction TB
        Req[Incoming HTTP Request] --> RouteCheck{Route Prefix?}
        RouteCheck -- "/api/v1/management/**" --> CtrlPlane["Control Plane (ZSA Platform Governance)\n• Authorize: RequireSuperAdmin\n• Ingress Tenancy: Permitted without resolved tenant"]
        RouteCheck -- "/api/v1/**" --> DataPlane["Data Plane (Tenant Workload Resources)\n• Authorize: Project/Resource Policies\n• Ingress Tenancy: 100% Mandatory Ambient Tenant"]
    end

    subgraph Defense ["2. Defense Strategy: Defense-in-Depth (3-Tier Guard)"]
        direction TB
        DataPlane --> T1["Tier 1: Perimeter Guard (ProjectContextMiddleware)\n• Channel A (X-API-Key) / Channel B (Bearer JWT)\n• Missing Tenant Header? -> 400 Bad Request (PROJECT_CONTEXT_REQUIRED)\n• SuperAdmin Standing Access? -> 403 Forbidden (CROSS_TENANT_ACCESS_DENIED)\n• Project Inactive? -> 403 Forbidden (PROJECT_DEACTIVATED)"]
        T1 -- Tenant Resolved & Permitted --> T2["Tier 2: Application Guard (UseCase PEMR in Trusted Zone)\n• Never checks HTTP headers, never throws 401 Unauthorized\n• Evaluates business preconditions & domain context"]
        T2 --> T3["Tier 3: Domain Invariants & Interceptor Guard\n• Entity Factory Methods (Create) enforce business invariants\n• EF Core Global Query Filter (WHERE IsResolved AND project_id = @Id)\n• ConcurrencySaveChangesInterceptor ensures transactional safety"]
    end

    subgraph Tactical ["3. Implementation Pattern: Tactical Design Patterns"]
        direction TB
        T2 --> ArchA["Archetype A (Create{Aggregate} / Key-Consuming)\n• Injects: ICurrentProjectContext\n• Line 1 Step 1: var projectId = projectContext.ProjectId;\n• Throws: TenantContextRequiredException (403) fail-fast"]
        T2 --> ArchB["Archetype B (ID-Based / Collection Queries / Updates)\n• Injects: ZERO Context (Zero Phantom Context Injection)\n• Scoped automatically by Tier 1 Perimeter & Tier 3 EF Filter\n• Throws: CrossTenantAccessDeniedException (403) on cross-tenant breach"]
    end

    CtrlPlane --> T2
```

* **Linear Governance Invariant**: Every request passes sequentially through Macro Zoning $\rightarrow$ Tier 1 Perimeter $\rightarrow$ Tier 2 Application $\rightarrow$ Tier 3 Domain. No layer may skip or contradict an upstream boundary.
* **Tier 1 vs Tier 2 Error Distinction**:
  * **Tier 1 HTTP 400 (`PROJECT_CONTEXT_REQUIRED`)**: The HTTP client violated the Data Plane perimeter contract by failing to transmit the `X-Project-Id` header (or valid route `{projectId}`).
  * **Tier 2 HTTP 403 (`TENANT_CONTEXT_REQUIRED`)**: An Archetype A UseCase accessed `projectContext.ProjectId` when no ambient tenant was hydrated, indicating an unauthorized execution context attempt.
  * **Tier 1 vs Tier 2 HTTP 403 (`CROSS_TENANT_ACCESS_DENIED`)**: Tier 1 blocks SuperAdmin from standing access to Data Plane routes; Tier 2 blocks any tenant caller from referencing or mutating resources across tenant partitions.

### 1.2 Architectural Wiring & Security Contracts
| Direction | Upstream Origin | Current Role (Tenancy & Security Engine) | Downstream Destination |
|---|---|---|---|
| **Incoming HTTP** | M2M Client or Web Studio | Inspects auth headers (`X-API-Key` vs `Authorization: Bearer`) | Authentication Handler Dispatch |
| **Channel A (M2M)** | External Integration Client | SHA-256 hash lookup, verifies `FixedTimeEquals`, extracts `project_id`, enforces `ApiKeyScope` | Hydrates `ClaimsPrincipal` (`Role: M2MClient`) & `ICurrentProjectContext` |
| **Channel B (JWT)** | Web Studio User Session | Verifies JWT signature, user membership, role claims & reads `X-Project-Id` / `{projectId}` | Hydrates `ClaimsPrincipal` (`Role: SuperAdmin/RoleType`) & `ICurrentProjectContext` |
| **Ambient Context** | Authenticated Channel | Exposes channel-agnostic `ICurrentProjectContext` (zero `IExecutionContext`) | EF Core `AppDbContext` & Inner Business Logic |
| **Persistence Query**| UseCase / Repository Query | Enforces fail-closed global tenancy SQL partition | PostgreSQL Engine (`WHERE @IsResolved AND project_id = @Id`) |

### 1.3 Affirmative Tenancy & Dual-Auth Invariants
| Security & Tenancy Concern | Affirmative Security Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Cryptographic Key Storage** | API keys are stored exclusively as SHA-256 one-way hashes (`KeyHash`) alongside non-sensitive `KeyPrefix` (e.g. `smk_crm_`) and authentic `KeySuffix` (e.g. `...efgh`). Raw plaintext keys are emitted exactly once upon creation and never persisted. | Credential Storage |
| **Browser Token Security** | Web session JWTs are stored in httpOnly, Secure, SameSite cookies managed via Next.js Proxy (`/api/proxy/[...path]`). Browser localStorage token persistence is prohibited. | Client Session |
| **Unified Ambient Context** | Tenancy and caller identity converge exclusively into `ICurrentProjectContext`. `IExecutionContext` is retired. Dual ambient contexts are prohibited. | Orchestration Core |
| **Fail-Closed Tenancy SQL** | EF Core Global Query Filter enforces `IsResolved && e.ProjectId == CurrentTenantId` across all `IMustHaveProject` entities. Manual `WHERE project_id = @id` clauses and `.IgnoreQueryFilters()` in tenant routes are prohibited. | Relational Boundary |
| **Timing Side-Channel Immunity**| All API key and secret comparisons execute via `CryptographicOperations.FixedTimeEquals` across byte representations. Variable-time string equality is prohibited. | Cryptographic Engine |
| **First-Class M2M Identity** | Channel A hydrates `ClaimsPrincipal` (`AuthenticationScheme: ApiKeyScheme`, `Role: M2MClient`, `ProjectId`, `Scope`, `ApiKeyId`) onto `HttpContext.User`. Prohibit setting ambient context without updating `HttpContext.User`. | Security Context |
| **Anti-Spoofing Parameter Policy**| Tenant UseCases consuming `ProjectId` or caller identity in domain logic resolve tenancy strictly from ambient `ICurrentProjectContext` via Tier 2 Smart Context property (`projectContext.ProjectId`). Archetype B UseCases operating strictly by ID or using EF Core Global Query Filters omit context injection entirely. Declaring caller `ProjectId` overrides on tenant Command/Query records, query strings (`[FromQuery]`), or JSON payloads is strictly prohibited. Controllers must never inject `ICurrentProjectContext`. | Ingress Parameter |
| **Global Identity Scoping** | `User` is a Global Identity (zero `ProjectId` column, zero `IMustHaveProject`). Multi-project tenancy is mapped exclusively through `UserProjectRole`. | Identity Model |
| **Delegated Sub-Key Quota** | Project Admins manage operational sub-keys on `/api/v1/api-keys` under a strict budget of maximum 2 active keys per tenant (1 `ReadOnly` + 1 `ReadWrite`), returning `409 Conflict` on duplicate scope. | Key Governance |
| **Key Masking Wire Egress** | Key list and get endpoints return masked keys derived from authentic components (`$"{KeyPrefix}****{KeySuffix}"`). Synthesizing pseudo-suffixes from the SHA-256 hash or returning raw keys is prohibited. | Contract Privacy |
| **Canonical REST Routes** | User and project access endpoints reside on flat RESTful routes (`/api/v1/users`, `/api/v1/users/{userId}/role`). Duplicate nested controller routes are prohibited. | Route Taxonomy |
| **Deterministic Route Precedence**| Tenant resolution resolves following strict hierarchy: Route (`{projectId}`) => Header (`X-Project-Id`) => JWT Claim (`ProjectId`), rejecting Route vs Header conflicts with RFC 9457 `400 Bad Request`. | Tenant Extraction |
| **Zero Standing Access & Plane Partitioning** | SuperAdmin receives strictly `RoleConstants.SuperAdmin` (never `Admin`). On Data Plane routes (`/api/v1/**`), SuperAdmin has Zero Standing Access and is rejected with `403 Forbidden` unless explicitly mapped in `UserProjectRole` by the tenant. | Zero Trust Perimeter |
| **Perimeter Scope Enforcement** | Channel A keys with `ApiKeyScope.ReadOnly` are blocked at `ProjectContextMiddleware` from executing mutating HTTP verbs (`POST`/`PUT`/`PATCH`/`DELETE`) returning `403 Forbidden`, permitting only stateless `/preview` and `/validate`. | Perimeter Firewall |
| **Authenticated Preview Protection**| Preview endpoints require authenticated tenant context to resolve templates and protect Gotenberg Chromium pods. Anonymous public access to `/preview` is prohibited. | Render Perimeter |
| **Tenant Idempotency Partition**| Distributed idempotency keys are partitioned strictly by resolved project: `idempotency:{ProjectId:N}:{IdempotencyKey}`. Shared caller keying is prohibited. | Cache Partitioning |
| **Portal Persona & Integration Perimeter** | The Web Portal (`frontend-v2`) is strictly an internal operator and authoring console for SuperAdmin and Template Designers. The system is explicitly NOT a central identity hub or IdP for third-party user logins; external enterprise callers (ERP, CRM) integrate exclusively via Channel A (M2M API Keys). | System Boundary |
| **Harmonized 2-Tier Role Taxonomy** | Identity is partitioned into Tier 1 Platform `SystemRole` (`SuperAdmin`, `Member`) and Tier 2 Project `RoleType` (`Admin`, `Developer`, `Viewer`). Spurious role aliases (`Editor`, `Author`) are strictly prohibited. | Role Governance |

---

## 2. Level 2: Dual-Channel Topology & Cognitive Tenancy Engine

### 2.1 Engineering Rationale & Causal Decisions
* **Why Internal Backoffice Console vs External Auth Hub:** Attempting to turn the document portal into a public Identity Provider (IdP) introduces accidental SaaS complexity (social login, user registration, self-service forgot-password). External enterprise software (ERP, CRM) already manages user identities natively; their servers consume document generation via Channel A (M2M API Keys). The web portal exists purely as an internal operator console for SuperAdmin governance and Template Designer authoring.
* **Why Constant-Time Hash Comparison (`FixedTimeEquals`):** Standard string equality (`==`) terminates on the first mismatched character, creating timing side-channel vulnerabilities that allow attackers to deduce hashes through network latency jitter.
* **Why `ClaimsPrincipal` Hydration for M2M:** ASP.NET Core `[Authorize]` attributes evaluate `HttpContext.User.Identity.IsAuthenticated`. Validating an API key into ambient context without updating `HttpContext.User` causes downstream middleware to treat M2M requests as unauthenticated (`401 Unauthorized`).
* **Why Unified `ICurrentProjectContext` (Retiring `IExecutionContext`):** Maintaining both `ICurrentProjectContext` and `IExecutionContext` causes split-brain bugs and leaks transport authentication mechanics into UseCases. Both `X-API-Key` (M2M) and `Authorization: Bearer` (JWT) converge at the perimeter into `ICurrentProjectContext` so inner business logic executes completely agnostically of transport origin.
* **Why Global `User` vs `UserProjectRole`:** Modeling `User` with a `ProjectId` column forces duplicate user accounts across projects, breaks single-sign-on (SSO), and makes enterprise user management impossible. `User` is a Global Identity; multi-tenancy is scoped strictly via the `UserProjectRole` join entity.
* **Why Omitting `ProjectId` from Tenant Payloads:** Eliminating `Guid ProjectId` from tenant-scoped Commands and Queries removes confused-deputy attack vectors by construction rather than relying on repetitive per-handler checks.
* **Why Route Precedence with Conflict Rejection:** Administrative endpoints define resource hierarchy in the URI (`/api/v1/projects/{projectId}/...`). Parsing `{projectId}` from route values and rejecting mismatches against `X-Project-Id` prevents cross-project header bleed attacks.
* **Why Zero Standing Access & Strict Plane Partitioning (SOC 2 / ISO 27001):** SuperAdmin manages platform governance on the Control Plane (`/api/v1/management/**`) via `RequireSuperAdmin`. Auto-hydrating `Admin` onto SuperAdmin creates dangerous standing access across customer data planes. By enforcing `UserProjectRole` membership across all interactive callers on Data Plane routes, tenant templates, documents, and datasets remain 100% isolated and compliant with Separation of Duties (SoD).
* **Why Perimeter Enforcement of `ApiKeyScope.ReadOnly`:** Blocking state-mutating HTTP methods at `ProjectContextMiddleware` prevents read-only integration keys from creating templates, uploading fonts, or mutating documents while still permitting stateless POST `preview` and `validate` calls.
* **Why Master Key / Delegated Ownership Pattern under Guardrails:** Eliminates operational bottlenecks by empowering Project Admins to self-service sub-key creation and rotation (`/api/v1/api-keys`) for daily integration needs (ERP, CRM) without waiting on platform operators. Preserves the Chain of Trust by restricting Master Credential changes and project ownership transfers strictly to SuperAdmin verification (`/api/v1/management/projects/{projectId}/transfer-owner`), while runtime guardrails enforce a hard budget of maximum 2 active keys per project (1 ReadOnly + 1 ReadWrite) returning `409 Conflict` on quota violation.
* **Why Relational `KeyPrefix` and `KeySuffix` Storage:** Masking API keys using the trailing characters of the SHA-256 hash causes serious confusion because the characters displayed in the authoring console do not match the actual secret key possessed by the developer or enterprise ERP system. Storing `KeyPrefix` (e.g. `smk_crm_`) and authentic `KeySuffix` (e.g. `abcd`) in the database allows safe, authentic key identification without ever persisting or exposing the raw secret.

### 2.2 Proactive Tenancy Guards
```yaml
TenancyQueryFilterGuard:
  Invariant: "Tenant repositories and queries must not call .IgnoreQueryFilters()."
  ActionOnViolation: "Remove .IgnoreQueryFilters() to enforce ambient fail-closed isolation."

CryptographicEqualityGuard:
  Invariant: "API key hash checks must use CryptographicOperations.FixedTimeEquals."
  ActionOnViolation: "Convert string comparison to FixedTimeEquals over byte arrays."

M2MClaimsPrincipalGuard:
  Invariant: "Channel A authentication must hydrate ClaimsPrincipal on HttpContext.User."
  ActionOnViolation: "Assign context.User = new ClaimsPrincipal(new ClaimsIdentity(...))."

CallerProjectIdProhibitionGuard:
  Invariant: "Tenant Command and Query records must omit caller ProjectId parameters."
  ActionOnViolation: "Remove ProjectId parameter; resolve ambient ICurrentProjectContext."

ReadOnlyScopeGuard:
  Invariant: "ApiKeyScope.ReadOnly must not execute state-mutating HTTP verbs."
  ActionOnViolation: "Return 403 Forbidden at perimeter for mutating methods on ReadOnly keys."
```

### 2.3 Dual-Channel Authentication Topology
```mermaid
graph TD
    Request["Incoming HTTP Request"] --> AuthCheck{"Which Auth Header Is Present?"}
    AuthCheck -->|"X-API-Key (Channel A: M2M)"| ChannelA["ApiKeyValidator: SHA-256 + FixedTimeEquals"]
    AuthCheck -->|"Authorization: Bearer (Channel B: JWT)"| ChannelB["JWT Handler: Signature + Claims Validation"]
    ChannelA -->|"Valid Key"| M2MHydrate["Hydrate ClaimsPrincipal (Role: M2MClient) & ReadOnly Verb Guard"]
    ChannelB -->|"Valid Token"| JWTHydrate["Hydrate ClaimsPrincipal (SuperAdmin / RoleType Claims)"]
    M2MHydrate --> Context["Hydrate Unified ICurrentProjectContext"]
    JWTHydrate --> Context
    Context --> Inner["Channel-Agnostic UseCase & EF Core Fail-Closed Global Filter"]
```

### 2.4 Unified Ambient Context Interfaces
```csharp
public interface ICurrentProjectContext
{
    /// <summary>
    /// Self-Guarding Smart Property. Returns strongly-typed ProjectId if resolved;
    /// otherwise, throws TenantContextRequiredException (403 Forbidden) fail-fast.
    /// </summary>
    ProjectId ProjectId { get; }
    bool IsResolved { get; }
    string? CallerIdentity { get; }
    UserId? UserId { get; }
    SystemRole? SystemRole { get; }
    ApiKeyId? ApiKeyId { get; }
}

public interface ICurrentProjectContextSetter
{
    void SetProject(ProjectId projectId, string? callerIdentity = null);

    void SetActor(
        UserId? userId = null,
        SystemRole? systemRole = null,
        ApiKeyId? apiKeyId = null,
        string? callerIdentity = null);
}
```

### 2.5 Unified 2-Tier RBAC & Scope Decision Matrix
The system is partitioned into two distinct identity channels: Channel B (Internal Portal for SuperAdmin and Designers) and Channel A (M2M External Systems). Spurious role aliases (`Editor`, `Author`) are strictly prohibited in favor of the canonical SSoT taxonomy:

| Identity Channel | Security Principal Tier | Canonical Role / Scope | Primary Operational Scope & Responsibilities |
|---|---|---|---|
| **Channel B (Portal)** | **Tier 1: Platform Level** (`SystemRole`) | `SuperAdmin` | Platform-wide governance, cross-tenant management, system diagnostics, master API keys. |
| **Channel B (Portal)** | **Tier 1: Platform Level** (`SystemRole`) | `Member` | Standard authenticated operator assigned to specific project workspaces. |
| **Channel B (Portal)** | **Tier 2: Tenant Workspace** (`RoleType`) | `Admin` | Project Administrator: manages project API keys, user memberships, and data connections. |
| **Channel B (Portal)** | **Tier 2: Tenant Workspace** (`RoleType`) | `Developer` | Template Designer: authors templates, Monaco HTML/Handlebars, bindings, datasets, live preview. |
| **Channel B (Portal)** | **Tier 2: Tenant Workspace** (`RoleType`) | `Viewer` | Read-only inspection: views template catalog and previews generated outputs. |
| **Channel A (M2M)** | **Tier 3: External Integration** (`ApiKeyScope`)| `ReadOnly` (`M2MClient`) | External ERP/CRM: stateless preview and schema validation only (`403` on mutating verbs). |
| **Channel A (M2M)** | **Tier 3: External Integration** (`ApiKeyScope`)| `ReadWrite` (`M2MClient`) | External ERP/CRM: document generation, async queueing, asset download, and template CRUD. |

---

## 3. Level 3: Canonical Security Archetypes (Surgical AST Recipes)

### 3.1 Channel A: M2M API Key Validator (`ApiKeyValidator`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-001]:
* Hashes all incoming M2M API keys using SHA-256 before database lookup.
* Compares hash bytes using `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
* Compares expiration against `timeProvider.GetUtcNow()`.

```csharp
public static class ApiKeyValidator
{
    public const string KeyPrefixTemplate = "smk_";

    public static (string RawKey, string KeyPrefix, string KeySuffix, string KeyHash) GenerateKey(string callerApp)
    {
        // Step 1: Generate Cryptographically Secure Secret <Why: eliminate PRNG predictability>
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var secret = Convert.ToHexString(randomBytes).ToLowerInvariant();

        // Step 2: Format Structured Key <Why: human-readable prefix and authentic trailing suffix>
        var prefix = $"{KeyPrefixTemplate}{callerApp.ToLowerInvariant()}_";
        var rawKey = $"{prefix}{secret}";
        var suffix = rawKey[^4..]; // Authentic trailing 4 characters of the raw key

        // Step 3: Compute SHA-256 Digest <Why: one-way cryptographic storage>
        var hash = HashApiKey(rawKey);

        return (rawKey, prefix, suffix, hash);
    }

    public static string HashApiKey(string rawApiKey)
    {
        // Step 1: Compute SHA-256 Hash <Why: one-way cryptographic digest for storage and lookup>
        var bytes = Encoding.UTF8.GetBytes(rawApiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<ApiKey?> AuthenticateAsync(
        string rawApiKey,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        // Step 2: Digest Lookup <Why: indexed search without exposing plaintext keys>
        var keyHash = HashApiKey(rawApiKey);
        var expectedBytes = Encoding.UTF8.GetBytes(keyHash);

        var apiKey = await dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, ct);

        if (apiKey == null) return null;

        // Step 3: Constant-Time Verification <Why: eliminate timing side-channel attacks>
        var actualBytes = Encoding.UTF8.GetBytes(apiKey.KeyHash);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return null;
        }

        // Step 4: Verify Expiration via TimeProvider <Why: deterministic clock assertions in tests>
        var now = timeProvider.GetUtcNow();
        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < now)
        {
            return null;
        }

        return apiKey;
    }
}
```

### 3.2 Ambient Project Context Middleware Archetype (`ProjectContextMiddleware`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-002]:
* Hydrates `ClaimsPrincipal` onto `HttpContext.User` on Channel A (`Role: M2MClient`, `ProjectId`, `Scope`, `ApiKeyId`, `CallerApp`) so `[Authorize]` succeeds.
* Enforces `ApiKeyScope.ReadOnly` on Channel A at perimeter: rejects `POST`/`PUT`/`PATCH`/`DELETE` with RFC 9457 `403 Forbidden` except stateless `/preview` and `/validate`.
* Extracts Channel B tenant identifier following precedence: Route `{projectId}` => Header `X-Project-Id` => JWT Claim `ProjectId`, rejecting Route vs Header conflicts with RFC 9457 `400 Bad Request`.
* Hydrates strictly `SuperAdmin` role claim for authenticated `SystemRole.SuperAdmin` users (Zero Standing Access: do NOT hydrate `Admin`).
* Enforces `UserProjectRole` membership checks across all interactive callers on Data Plane routes (`/api/v1/**`), permitting bypass only on Control Plane (`/api/v1/management/**`).

```csharp
public sealed class ProjectContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        AppDbContext dbContext,
        TimeProvider timeProvider)
    {
        // Step 1: Channel A (M2M X-API-Key) Authentication & Claims Hydration <Why: first-class citizen in [Authorize]>
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues) &&
            !string.IsNullOrWhiteSpace(apiKeyValues))
        {
            var rawApiKey = apiKeyValues.ToString().Trim();
            var apiKey = await ApiKeyValidator.AuthenticateAsync(rawApiKey, dbContext, timeProvider, context.RequestAborted);

            if (apiKey == null)
            {
                await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Invalid API Key",
                    "The provided API key is invalid, inactive, or expired.");
                return;
            }

            // Step 2: Enforce ReadOnly scope <Why: block mutating HTTP verbs at perimeter>
            if (apiKey.Scope == ApiKeyScope.ReadOnly && IsMutatingNonStatelessRequest(context.Request))
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Insufficient Key Scope",
                    "API key with ReadOnly scope is not permitted to execute state-mutating operations.");
                return;
            }

            // Step 3: Hydrate ambient project context <Why: channel-agnostic ambient context>
            var projectId = new ProjectId(apiKey.ProjectId);
            var apiKeyId = new ApiKeyId(apiKey.Id);
            projectContextSetter.SetProject(projectId, apiKey.CallerApp);
            projectContextSetter.SetActor(apiKeyId: apiKeyId, callerIdentity: apiKey.CallerApp);

            // Step 4: Hydrate ClaimsPrincipal onto HttpContext.User <Why: ASP.NET Core authorization pipeline>
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
                new Claim(ClaimTypes.Role, "M2MClient"),
                new Claim("Scope", apiKey.Scope.ToString()),
                new Claim("ProjectId", apiKey.ProjectId.ToString()),
                new Claim("CallerApp", apiKey.CallerApp)
            };
            var identity = new ClaimsIdentity(claims, "ApiKeyScheme");
            context.User = new ClaimsPrincipal(identity);

            await next(context);
            return;
        }

        // Step 5: Channel B (JWT User Session) & Tenant Scoping <Why: web studio user session management>
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var routeProjectId = ExtractRouteProjectId(context);
            var headerProjectId = ExtractHeaderProjectId(context);

            if (routeProjectId.HasValue && headerProjectId.HasValue && routeProjectId.Value != headerProjectId.Value)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Tenant Identification Conflict",
                    $"Route ProjectId '{routeProjectId}' conflicts with Header X-Project-Id '{headerProjectId}'.");
                return;
            }

            var userId = ExtractUserId(context.User);
            var isSuperAdmin = IsSuperAdminPrincipal(context.User);
            var systemRole = isSuperAdmin ? SystemRole.SuperAdmin : SystemRole.Member;

            projectContextSetter.SetActor(
                userId: userId,
                systemRole: systemRole,
                callerIdentity: userId is { } uid ? $"User:{uid.Value:N}" : "portal");

            // Zero Standing Access: Hydrate SuperAdmin claim only (do NOT auto-escalate to tenant Admin)
            if (isSuperAdmin && context.User.Identity is ClaimsIdentity superIdentity)
            {
                EnsureRoleClaim(superIdentity, RoleConstants.SuperAdmin);
            }

            var isManagementRoute = context.Request.Path.Value?.StartsWith("/api/v1/management/", StringComparison.OrdinalIgnoreCase) == true;
            var effectiveProjectId = routeProjectId ?? headerProjectId ?? ExtractClaimProjectId(context);
            if (effectiveProjectId.HasValue)
            {
                var targetProjectId = new ProjectId(effectiveProjectId.Value);
                if (isManagementRoute && isSuperAdmin)
                {
                    // Control Plane: SuperAdmin manages project container without tenant membership
                    projectContextSetter.SetProject(targetProjectId, actorIdentity);
                }
                else
                {
                    // Data Plane: Strict tenant membership verification required (Zero Standing Access)
                    var mapping = await userProjectRoleRepo.GetAsync(targetProjectId, userId!.Value, context.RequestAborted);
                    if (mapping == null)
                    {
                        await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden",
                            $"User does not have permission to access project '{targetProjectId}'.");
                        return;
                    }
                    if (context.User.Identity is ClaimsIdentity projectIdentity)
                    {
                        EnsureRoleClaim(projectIdentity, mapping.Role.Name);
                    }
                    projectContextSetter.SetProject(targetProjectId, actorIdentity);
                }
            }

            await next(context);
            return;
        }

        // Step 6: Unauthenticated / Public Paths <Why: continue pipeline for anonymous routes>
        await next(context);
    }

    private static bool IsMutatingNonStatelessRequest(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return false;

        var path = request.Path.Value ?? string.Empty;
        return !path.Contains("/preview", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("/validate", StringComparison.OrdinalIgnoreCase);
    }

    private static UserId? ExtractUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var guid) && guid != Guid.Empty ? new UserId(guid) : null;
    }

    private static bool IsSuperAdminPrincipal(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirst("SystemRole")?.Value, RoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    private static void EnsureRoleClaim(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(ClaimTypes.Role, role))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }

    private static Guid? ExtractRouteProjectId(HttpContext context) =>
        Guid.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out var id) && id != Guid.Empty ? id : null;

    private static Guid? ExtractHeaderProjectId(HttpContext context) =>
        Guid.TryParse(context.Request.Headers[HttpHeaderConstants.ProjectIdHeader], out var id) && id != Guid.Empty ? id : null;

    private static Guid? ExtractClaimProjectId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("ProjectId")?.Value, out var id) && id != Guid.Empty ? id : null;

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        return context.Response.WriteAsJsonAsync(problem);
    }
}
```

### 3.3 The 3-Tier Guard Model & Smart Context Blueprint
>> MANDATORY CANONICAL BLUEPRINT [RULE-SEC-003]:
* **Tier 1 (Perimeter Guard — `ProjectContextMiddleware`)**:
  - Rejects un-tenanted calls on Data Plane (`/api/v1/**` except `/management/`, `/auth/`, and whitelist) fail-fast with RFC 9457 HTTP `400 Bad Request` (`ErrorCode: PROJECT_CONTEXT_REQUIRED`).
  - Validates project existence (`404 Not Found`) and active status (`403 Forbidden: PROJECT_DEACTIVATED`).
  - Ensures Data Plane Controllers and UseCases operate strictly within a verified, trusted tenancy perimeter.
* **Tier 2 (Application Guard — Self-Guarding Smart Context & Domain Exceptions)**:
  - **Self-Guarding Fail-Fast Property**: `ICurrentProjectContext.ProjectId` throws `TenantContextRequiredException` (`TENANT_CONTEXT_REQUIRED`, HTTP 403 Forbidden) immediately upon access when `IsResolved == false`. Eliminates 20+ fragmented magic strings.
  - **Safe Optional Accessor**: `ICurrentProjectContext.OptionalProjectId` returns `ProjectId?` (or null if unresolved, never `ProjectId.Empty`). Utilized strictly by `AppDbContext` for compile-time global query filters, Control Plane operations, background workers, and migration seeding.
  - **Strict Selective Injection Rule**: UseCases inject `ICurrentProjectContext` strictly when `ProjectId` or caller identity is consumed in domain logic (e.g. `Aggregate.Create(projectId, ...)`, MinIO storage path, DraftCache, or composite key queries).
  - UseCases operating on existing aggregates by ID (e.g. `GetById`, `Delete`, `List`) rely on Tier 1 Perimeter Guard and EF Core Global Query Filter; **they must NOT inject `ICurrentProjectContext`**.
  - **Fail-Fast Line 1 Execution**: When injected (Archetype A), consumes `ICurrentProjectContext` strictly at line 1 of Step 1 (Parse & Guard): `var activeProjectId = projectContext.ProjectId;`. Deferring context evaluation to Step 3 after database queries or external network I/O is strictly prohibited ([TRAP-TENANT-025]).
  - **Cross-Tenant Access Defense**: When checking resource ownership, throws `CrossTenantAccessDeniedException` (`CROSS_TENANT_ACCESS_DENIED`, HTTP 403 Forbidden).
  - Omits `Guid ProjectId` on tenant-scoped Command or Query records (anti-spoofing).
  - Partitions `IdempotencyFilter` cache keys by `projectContext.ProjectId.Value` (`idempotency:{ProjectId:N}:{IdempotencyKey}`).
  - Data Plane UseCases operate in a Trusted Zone and **never throw 401 Unauthorized**.
* **Tier 3 (Persistence & Domain Invariant Guard — Interceptors, Entities & Value Objects)**:
  - `TenantSecuritySaveChangesInterceptor` serves as the fail-safe persistence guard, throwing `CrossTenantAccessDeniedException` on foreign project ID mutation.
  - Aggregate factories (`Create`) and mutating verbs encapsulate invariants, throwing `DomainValidationException` (400) or `BusinessRuleViolationException` (400).

```csharp
// --- SSoT: ICurrentProjectContext Contract ---
public interface ICurrentProjectContext
{
    /// <summary>
    /// Gets the resolved ambient ProjectId.
    /// THROWS TenantContextRequiredException immediately if accessed while unresolved!
    /// </summary>
    ProjectId ProjectId { get; }

    /// <summary>
    /// Safe accessor for Control Plane, Auth, Background Workers, or AppDbContext.
    /// Returns null if unresolved (Never returns ProjectId.Empty).
    /// </summary>
    ProjectId? OptionalProjectId { get; }

    bool IsResolved { get; }
    string? CallerIdentity { get; }
    UserId? UserId { get; }
    SystemRole? SystemRole { get; }
    ApiKeyId? ApiKeyId { get; }
}

// --- Canonical Usage in Archetype A UseCase ExecuteAsync ---
// Step 1: Parse & Guard (Line 1 Fail-Fast via Smart Context Property; zero magic strings)
var activeProjectId = projectContext.ProjectId;
// (Note: Archetype B UseCases operating strictly by ID or using EF Core Global Query Filter do NOT inject context)
```

### 3.4 Compile-Time EF Core Global Tenancy Filter Configuration
>> SSoT DELEGATION: [Compile-Time EF Core Global Query Filter Mechanics] governed strictly by [database_persistence_and_schema_mechanics.md](./database_persistence_and_schema_mechanics.md#33-multi-tenant-global-query-filter-configuration-in-appdbcontext).

---

## 4. Level 4: High-Salience Fatal Traps & Unified Micro-Diffs

### [TRAP-SEC-001]: Plaintext API Key Database Lookup
```diff
- var key = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.RawKey == rawKey);
+ var keyHash = ApiKeyValidator.HashApiKey(rawKey);
+ var key = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash);
```
*Rationale: Querying or storing raw keys in database records leaves credentials vulnerable to leaks via SQL injection, audit logs, and DB backups.*

### [TRAP-SEC-002]: Multi-Tenant Filter Bypass
```diff
- var docs = await dbContext.Documents.IgnoreQueryFilters().ToListAsync(ct);
+ var docs = await dbContext.Documents.ToListAsync(ct);
```
*Rationale: Disabling global query filters creates catastrophic IDOR cross-tenant data leaks in production endpoints.*

### [TRAP-SEC-003]: Variable-Time Hash Comparison
```diff
- if (keyHash == apiKey.KeyHash) return apiKey;
+ if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes)) return null;
```
*Rationale: Standard string equality short-circuits on mismatch, leaking character-by-character timing signals to network attackers.*

### [TRAP-SEC-004]: Dual Ambient Context & Caller ProjectId Override
```diff
- public async Task<IActionResult> ListTemplates([FromQuery] Guid? projectId, ...) {
-     var targetProject = projectId ?? legacyContext.ProjectId!.Value;
+ [HttpGet] public async Task<IActionResult> ListTemplates([FromQuery] string? search, ...) {
+     var query = new ListTemplatesQuery(search);
+     var result = await listTemplatesUseCase.ExecuteAsync(query, ct);
```
*Rationale: Maintaining parallel execution contexts, injecting `ICurrentProjectContext` into controllers, or accepting caller-supplied `ProjectId` overrides creates split-brain cross-tenant queries and controller dependency bloat. Multi-tenant context resolves exclusively via middleware into ambient `ICurrentProjectContext`, and UseCases consuming ProjectId bind tenancy via Tier 2 Smart Context `var activeProjectId = projectContext.ProjectId;` at line 1 of Step 1 PEMR. UseCases operating by ID or querying collections filtered by EF Core omit context injection entirely.*

### [TRAP-SEC-005]: M2M Missing ClaimsPrincipal (Dropping to 401 Unauthorized under `[Authorize]`)
```diff
- projectContextSetter.SetProject(new ProjectId(apiKey.ProjectId));
- // HttpContext.User left unauthenticated
+ projectContextSetter.SetProject(new ProjectId(apiKey.ProjectId), apiKey.CallerApp);
+ var identity = new ClaimsIdentity(
+     new[] { new Claim(ClaimTypes.Role, "M2MClient"), new Claim("ProjectId", apiKey.ProjectId.ToString()) },
+     "ApiKeyScheme");
+ context.User = new ClaimsPrincipal(identity);
```
*Rationale: ASP.NET Core `[Authorize]` attributes evaluate `HttpContext.User.Identity.IsAuthenticated`. Leaving `context.User` unhydrated causes M2M clients to fail authorization even with a valid API key.*

### [TRAP-SEC-006]: Masking API Key from SHA-256 Hash Suffix
```diff
- var suffix = apiKey.KeyHash.Value[^4..]; // REJECTED: Hash suffix never matches the operator's actual key!
- var masked = $"smk_{apiKey.CallerApp}_****{suffix}";
+ // Authentic key suffix persisted at generation time
+ var masked = $"{apiKey.KeyPrefix}****{apiKey.KeySuffix}";
```
*Rationale: Generating masked keys from the trailing characters of the SHA-256 hash causes total confusion because the masked string displayed in the console differs from the physical credentials held by the operator or enterprise ERP. Storing `KeyPrefix` and authentic `KeySuffix` alongside `KeyHash` guarantees accurate, non-sensitive key identification.*

### [TRAP-SEC-008]: SuperAdmin Standing Access Breach (Data Plane Bypass)
```diff
- // ANTI-PATTERN: Auto-hydrating Admin onto SuperAdmin and bypassing tenant membership check
- if (isSuperAdmin) { EnsureRoleClaim(superIdentity, RoleConstants.Admin); }
- if (!isSuperAdmin) { var mapping = await userProjectRoleRepo.GetAsync(targetProjectId, userId); ... }
+ // CANONICAL: Zero Standing Access - Partition Control Plane vs Data Plane
+ if (isSuperAdmin) { EnsureRoleClaim(superIdentity, RoleConstants.SuperAdmin); } // ONLY SuperAdmin
+ if (isManagementRoute && isSuperAdmin) {
+     projectContextSetter.SetProject(targetProjectId, actorIdentity);
+ } else {
+     var mapping = await userProjectRoleRepo.GetAsync(targetProjectId, userId);
+     if (mapping == null) return 403 Forbidden;
+     EnsureRoleClaim(projectIdentity, mapping.Role.Name);
+     projectContextSetter.SetProject(targetProjectId, actorIdentity);
+ }
```
*Rationale: Giving platform SuperAdmin permanent standing read/write access to customer templates, datasets, and rendered documents violates SOC 2 Type II, ISO 27001, and Separation of Duties (SoD). SuperAdmin operates strictly within the Control Plane (`/api/v1/management/**`). On Data Plane routes, every interactive caller without an explicit `UserProjectRole` row is rejected with `403 Forbidden`.*
