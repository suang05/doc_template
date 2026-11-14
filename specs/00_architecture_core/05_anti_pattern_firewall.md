# [CORE-005]: 05_anti_pattern_firewall.md — Anti-Pattern Firewall & Static Enforcement Matrix

<context>
Category: 00_architecture_core
Domain: Anti-Pattern Firewall, Static Analysis Rules & Quality Gates
SoleResponsibilities:
  - Canonical catalog of forbidden anti-patterns (TRAP registry) across architecture, domain, persistence, security, and frontend
  - Severity classification (FATAL, CRITICAL, HIGH) and CI/CD gate policies
  - Static enforcement matrix mapping traps to Roslyn analyzers, NetArchTest, and BannedSymbols.txt
  - Paired anti-pattern (-) and safe architectural pattern (+) implementation blueprints
Out of scope (owned elsewhere): code cadences and idioms (02), security controls and performance budgets (03), API contract standards (06).
Target: Full Stack (`backend-v2/` and `frontend-v2/`)
Platform baseline: declared in `Directory.Build.props` (ADR-01).
Signal: [ARCH_EVOLUTION_FIREWALL]
</context>

---

> **Purpose:** Define the non-negotiable architectural firewall for `smk-doc-server`. Every pattern cataloged in this document has caused production outages, security breaches, cross-tenant data leaks, or memory exhaustion in enterprise document engines. All code—whether written by humans or AI agents—is gated against these rules by automated static analyzers, architecture tests, and CI pipelines.
> **Authoritative Cross-References:** [01_system_architecture.md](./01_system_architecture.md), [02_coding_conventions.md](./02_coding_conventions.md), [03_security_and_performance.md](./03_security_and_performance.md), [04_naming_standards.md](./04_naming_standards.md), [06_api_contract_standards.md](./06_api_contract_standards.md).

---

## 1. Severity Classification & Enforcement Policy

Every anti-pattern is assigned a strict severity tier that governs CI/CD behavior and code review triage:

| Severity | Operational / Security Impact | CI / Release Gate Policy |
| :--- | :--- | :--- |
| **`FATAL`** | Cross-tenant data leakage, Remote Code Execution (RCE), Server-Side Request Forgery (SSRF), SQL injection, thread-pool starvation, or Out-Of-Memory (`OutOfMemoryException`) crash under normal load. | **Immediate Build Failure.** Enforced as compiler errors (`WarningsAsErrors`) or mandatory CI gate failures. Zero waivers permitted. |
| **`CRITICAL`** | Silent data loss (dropped domain events, lost updates / race conditions), corrupted document streams, broken horizontal scaling across pods, or Clean Architecture boundary violations. | **Immediate Build / PR Failure.** Enforced via `NetArchTest.Rules`, Roslyn analyzers, and integration tests. Cannot be merged to `main`. |
| **`HIGH`** | Observability blindness, API contract drift, frontend memory leaks, or maintainability degradation that leads to latent defects. | **PR Gate Failure.** Enforced via OpenAPI linters, ESLint/TypeScript strict checks, and code-review checklists. |

---

## 2. Level 1 Anti-Pattern Firewall Matrix (Quick Reference)

All Trap IDs in this document are globally unique across `00_architecture_core` (note that `TRAP-SEC-001` through `TRAP-SEC-005` are defined in [03_security_and_performance.md](./03_security_and_performance.md#7-security--performance-anti-pattern-traps)).

| Trap ID | Category | Severity | Forbidden Anti-Pattern (`-`) | Safe Architectural Pattern (`+`) | Detection Mechanism | CI Gate | Authoritative Reference |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`TRAP-ASYNC-001`** | Async / Threading | `FATAL` | Sync-over-async (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`), `async void`, fire-and-forget `_ = Task.Run(...)`, or omitting `CancellationToken ct`. | `async`/`await` end-to-end; propagate `CancellationToken ct` as the last parameter of every I/O signature; use durable queues for background work. | `Microsoft.VisualStudio.Threading.Analyzers` (`VSTHRD002`, `VSTHRD100`, `VSTHRD110`) + `CA2016` | `dotnet build --warnaserror` | [02_coding_conventions.md](./02_coding_conventions.md#32-cadence-2-application-usecase) |
| **`TRAP-EF-002`** | Persistence / EF Core | `FATAL` | Concurrent `Task.WhenAll` on the same `DbContext`, multi-collection `.Include()` without `.AsSplitQuery()`, tracking read queries, or holding DB transactions across external HTTP/MinIO I/O. | Sequential operations per `DbContext` + `AddRange` in one `IUnitOfWork.CommitAsync(ct)`; `.AsNoTracking()` + `.AsSplitQuery()` on reads; commit DB (< 5 ms) after external I/O finishes. | Custom Roslyn / Semgrep rule (`ef-no-parallel-dbcontext`) + Connection Hold Telemetry | Semgrep CI + Integration Tests | [01_system_architecture.md](./01_system_architecture.md#141-pipeline-a-synchronous-on-demand-preview--small-renders) |
| **`TRAP-MEM-003`** | Memory / Streams | `FATAL` | Buffering files via `File.ReadAllBytesAsync`, `.ToArray()`, `new MemoryStream()`, or wrapping a stream returned to `FileStreamResult` in `await using`. | Stream with `65_536`-byte pooled buffers via `HybridSpoolStream` or `RecyclableMemoryStream`; transfer stream disposal ownership directly to `FileStreamResult`. | `BannedApiAnalyzers` (`MemoryStream.ToArray`, `File.ReadAllBytes*`) | `dotnet build --warnaserror` | [03_security_and_performance.md](./03_security_and_performance.md#1-memory-management--zero-loh-streaming-mandates) |
| **`TRAP-DATA-004`** | Rendering / XSS & Egress | `FATAL` | Stripping HTML/CSS tags from templates with regex/sanitizers (breaking layouts) OR interpolating unencoded tenant data into HTML templates without Gotenberg egress isolation. | Keep rich HTML/CSS template structure intact; HTML-encode all bound data values in `DataBindingEngine` (`HtmlEncoder.Default.Encode`) and isolate Gotenberg (`--chromium-disable-javascript`, `--chromium-deny-list`). | Semgrep (`raw-html-interpolation`) + Container Security Policy | Semgrep CI + Container Smoke Test | [03_security_and_performance.md](./03_security_and_performance.md#5-untrusted-input-attack-surface-matrix) |
| **`TRAP-LEAK-005`** | Clean Architecture | `CRITICAL` | Referencing `Microsoft.EntityFrameworkCore`, `[Table]`, `HttpContext`, `IFormFile`, or `repository.SetExpectedVersion` inside `SmkDoc.Domain` or `SmkDoc.Application`. | Keep Domain 100% dependency-free; enforce concurrency via `IConcurrencyAware.Version` in Domain/Application and configure EF Core inside `IEntityTypeConfiguration<T>`. | `NetArchTest.Rules` (`Domain_Should_Not_Have_Dependency_On_Infrastructure`) | `dotnet test` Architecture Suite | [01_system_architecture.md](./01_system_architecture.md#12-dependency-direction--layer-contracts) |
| **`TRAP-TIME-006`** | Determinism & DI Lifetimes | `CRITICAL` | Calling `DateTime.Now`, `DateTime.UtcNow`, `Guid.NewGuid()`, `new HttpClient()`, or injecting `Scoped` services (`DbContext`, `IProjectContext`) into `Singleton` services. | Inject `TimeProvider`, use `Guid.CreateVersion7()`, use `IHttpClientFactory`, and enable `ValidateScopes = true` + `ValidateOnBuild = true` in all environments. | `BannedApiAnalyzers` (`DateTime.Now`, `DateTime.UtcNow`, `Guid.NewGuid`, `HttpClient..ctor`) | `dotnet build --warnaserror` | [02_coding_conventions.md](./02_coding_conventions.md#1-core-language--compiler-mandates) |
| **`TRAP-NULL-007`** | Domain Modeling | `HIGH` | Null-forgiving operator (`!`) abuse outside EF `null!` navigation properties, primitive obsession (`string status`, raw `Guid`), or public property setters on Entities. | Enable `<Nullable>enable</Nullable>`, use Strongly-Typed IDs (`readonly record struct`), Value Objects with factory validation, and private setters with rich domain methods. | Compiler `CS8600`–`CS8625` + `NetArchTest` (`Entities_Should_Not_Have_Public_Setters`) | `dotnet build --warnaserror` | [02_coding_conventions.md](./02_coding_conventions.md#33-cadence-3-domain-aggregate-root) |
| **`TRAP-VOID-008`** | Exception Handling | `CRITICAL` | Empty `catch (Exception) {}`, catching `OperationCanceledException` as a server fault, or "log-and-rethrow" inside UseCases/Repositories causing duplicate error logs. | Let Domain/Application exceptions bubble untouched to `GlobalExceptionHandler` (log once at the boundary); filter `catch (Exception ex) when (ex is not OperationCanceledException)`. | Semgrep (`no-empty-catch`, `no-log-and-rethrow`) + `CA1031` | Semgrep CI + `dotnet build` | [06_api_contract_standards.md](./06_api_contract_standards.md#31-canonical-exception-to-http-mapping-matrix) |
| **`TRAP-AUDIT-009`** | Audit & PDPA Privacy | `CRITICAL` | Storing unbounded > 16 KB JSON payloads in OLTP `generation_logs` rows, checking `string.Length` instead of UTF-8 byte count, or logging raw PII/secrets. | Check `Encoding.UTF8.GetByteCount(rawJson) > 16_384`, compute SHA-256 over UTF-8 bytes, offload oversized payloads to MinIO via Outbox, and redact PII fields. | Unit & Integration Tests (`GenerationLogPayloadTests`) | `dotnet test` | [03_security_and_performance.md](./03_security_and_performance.md#6-enterprise-security-domains-owasp-asvs-v40--thai-pdpa) |
| **`TRAP-EVENT-010`** | Domain Events & Outbox | `CRITICAL` | Dispatching domain events in-process after `unitOfWork.CommitAsync(ct)` (`foreach (var ev in events) await publisher.Publish(ev)`), losing events if the pod crashes. | Persist `entity.DomainEvents` into `outbox_messages` inside the **same** PostgreSQL transaction via `OutboxInterceptor` during `unitOfWork.CommitAsync(ct)`. | `NetArchTest` + Integration Fault-Injection Test | `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#143-pipeline-c-asynchronous-batch--webhook-dispatch-queue--outbox-mechanics) |
| **`TRAP-SQL-011`** | External Tenant DB / SSRF | `FATAL` | Building tenant connection strings via string interpolation, resolving DNS without IP pinning/CIDR validation, or executing tenant queries on read-write connections. | Validate host via `ConnectionHostValidator.ResolveAndValidateAsync`, build connection string via `DbConnectionStringBuilder`, enforce `BEGIN TRANSACTION READ ONLY`, `statement_timeout = '5000ms'`, and `10_000`-row cap. | `BannedApiAnalyzers` (`FromSqlRaw`) + Security Integration Suite | `dotnet build` + `dotnet test` | [03_security_and_performance.md](./03_security_and_performance.md#3-ssrf--external-data-source-firewall-connectionhostvalidator) |
| **`TRAP-BATCH-012`** | Batch Processing | `FATAL` | Looping over batch items to render PDFs synchronously inside an HTTP controller/UseCase, or queuing jobs into `Channel.CreateUnbounded<T>()`. | Use Pipeline C (`202 Accepted`): persist batch items into PostgreSQL `document_jobs`, return `Location: /api/v1/document-jobs/{id}`, and process asynchronously via `FOR UPDATE SKIP LOCKED` workers. | `BannedApiAnalyzers` (`Channel.CreateUnbounded`) + API Timeout Gate | `dotnet build --warnaserror` | [01_system_architecture.md](./01_system_architecture.md#143-pipeline-c-asynchronous-batch--webhook-dispatch-queue--outbox-mechanics) |
| **`TRAP-SQL-013`** | Concurrency & Bulkheads | `CRITICAL` | Unbounded `Task.WhenAll(items.Select(...))` exhausting connection pools, or writing `await using var s = new SemaphoreSlim(4)` (fails with compiler error `CS8410`). | Bound concurrency using `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 4` and shared `PartitionedRateLimiter` bulkheads. | Compiler `CS8410` + Semgrep (`unbounded-task-whenall`) | `dotnet build` + Semgrep CI | [03_security_and_performance.md](./03_security_and_performance.md#4-partitioned--distributed-rate-limiting) |
| **`TRAP-SCALE-014`** | Multi-Pod State & Idempotency | `CRITICAL` | Storing `Idempotency-Key` records or rate-limit counters only in local `IMemoryCache` / `ConcurrentDictionary` without distributed backing or payload hash verification. | Persist idempotency state in PostgreSQL `idempotency_keys` (with `request_hash` SHA-256 verification → `422 IDEMPOTENCY_KEY_REUSED`) and coordinate multi-pod rate limits via Redis/gateway + `PartitionedRateLimiter`. | Architecture Review + Multi-Instance Integration Test | `dotnet test` | [06_api_contract_standards.md](./06_api_contract_standards.md#25-idempotency-protocol) |
| **`TRAP-SYNC-015`** | Optimistic Concurrency | `CRITICAL` | Blind `PUT`/`PATCH` overwrites (`Last-Write-Wins`) or leaking EF Core `SetExpectedVersion` into Domain repository interfaces. | Require `If-Match: "v{version}"` (`428` if missing), check `entity.Version != command.ExpectedVersion` in UseCase (`412`), and back with EF Core `.IsConcurrencyToken()` on `version bigint`. | OpenAPI Contract Linter + Concurrency Integration Tests | `Redocly` + `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#144-optimistic-concurrency-end-to-end-flow) |
| **`TRAP-SSOT-017`** | Template Versioning | `CRITICAL` | Resolving live mutable `Template.FieldMappings` during historical regeneration or falling back via `version.MappingsSnapshot ?? template.FieldMappings`. | Bind every generated document and batch job to an immutable `TemplateVersion` snapshot (freezing template asset, schema, mappings, fonts, and engine version); fail fast if snapshot is missing. | Domain Unit Tests (`TemplateVersionSnapshotTests`) | `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#142-pipeline-b-template-publish--immutable-snapshot) |
| **`TRAP-WRAPPER-018`** | Application Design & Envelopes | `HIGH` | Creating pure pass-through `*Wrapper` / `*SubPipeline` classes with zero independent logic, OR returning `200 OK` with `{ "isError": true }` instead of `ApiResponse<T>` + RFC 9457 `ProblemDetails`. | Ban pure pass-through wrappers while allowing cohesive domain/rendering collaborators (`IDocumentRenderer`, `DataBindingEngine`); cap UseCase cognitive complexity via analyzer; wrap `2xx` JSON in `ApiResponse<T>` / `PagedApiResponse<T>`. | Roslyn Cognitive Complexity Analyzer + `NetArchTest` | `dotnet build` + `dotnet test` | [06_api_contract_standards.md](./06_api_contract_standards.md#31-apiresponset-blueprint) |
| **`TRAP-TENANT-022`** | Tenant Resolution & Oracle | `FATAL` | Reading `projectId` from route/query/body instead of `Project-Id` header + credential claim, using legacy `X-Project-Id`, or returning `403 Forbidden` when looking up another tenant's resource ID. | Resolve tenant strictly in `TenantResolutionMiddleware` (`Project-Id` header matched against credential → `400 PROJECT_CONTEXT_MISMATCH` on mismatch); return `404 NotFoundException` when a resource ID is not in the active tenant scope. | Multi-Tenant Security Integration Suite | `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#15-multi-tenant-isolation-architecture) |
| **`TRAP-TENANT-023`** | Archetype B Tenant Isolation | `FATAL` | Injecting `IProjectContext` into Archetype B UseCases for manual `if (entity.ProjectId != ...)` checks, OR calling `.IgnoreQueryFilters()` / interpolated `FromSqlRaw($"...")`. | Keep Archetype B UseCases free of `IProjectContext`; rely on the 5-Layer Defense-in-Depth Isolation Architecture (fail-closed EF global filter + write interceptor + PostgreSQL RLS + banned APIs). | `BannedApiAnalyzers` (`IgnoreQueryFilters`, `FromSqlRaw`) + RLS Leakage Tests | `dotnet build --warnaserror` + `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#15-multi-tenant-isolation-architecture) |
| **`TRAP-TENANT-024`** | Archetype A Creation & Slug | `CRITICAL` | Creating a tenant-owned aggregate without `IProjectContext`, or checking `SlugExistsAsync` with a raw un-normalized string before constructing the Value Object. | Inject `IProjectContext`, call `projectContext.GetRequiredProjectId()`, construct/normalize the Value Object (`TemplateSlug.Create`) **first**, then check `SlugExistsAsync(projectId, slug, ct)` backed by `uq_{table}_project_id_slug`. | Code Review + Unique Constraint Integration Tests | `dotnet test` | [02_coding_conventions.md](./02_coding_conventions.md#32-cadence-2-application-usecase) |
| **`TRAP-TENANT-025`** | SuperAdmin Standing Access | `FATAL` | Bypassing EF Core Global Query Filters via `|| currentUser.IsSuperAdmin` on `ProjectDbContext`, granting standing cross-tenant read/write access on tenant endpoints. | Enforce **Zero Standing Access**: `ProjectDbContext` never checks `IsSuperAdmin`; platform operations run on `/api/v1/management/**` (`RequireSuperAdmin`) with explicit `ProjectId`, time-boxed `BreakGlassGrant`, and immutable `AuditLog`. | `NetArchTest` + Multi-Tenant Isolation Tests | `dotnet test` | [01_system_architecture.md](./01_system_architecture.md#15-multi-tenant-isolation-architecture) |
| **`TRAP-FE-026`** | Frontend Security & Memory | `HIGH` | Rendering untrusted HTML via `dangerouslySetInnerHTML`, storing tokens in `localStorage`/`NEXT_PUBLIC_*`, putting `'use client'` on root layouts, or leaking `URL.createObjectURL(blob)` without revoking. | Use BFF `HttpOnly` cookies, keep Next.js App Router pages as Server Components by default, and always call `URL.revokeObjectURL(objectUrl)` inside a `finally` or `useEffect` cleanup block. | ESLint (`react/no-danger`, custom rules) + TypeScript Strict | `pnpm lint && pnpm typecheck` | [03_security_and_performance.md](./03_security_and_performance.md#6-enterprise-security-domains-owasp-asvs-v40--thai-pdpa) |

---

## 3. Level 2 Deep-Dive Code Catalog (`-` Forbidden vs. `+` Mandatory)

Every code example below strictly obeys [02_coding_conventions.md](./02_coding_conventions.md) and [04_naming_standards.md](./04_naming_standards.md): primary constructors, `sealed` classes, zero `_camelCase` backing fields in DI classes, full descriptive identifiers (`templateRepository`, `ct`), and explicit `CancellationToken` propagation.

---

### `TRAP-ASYNC-001` — Sync-Over-Async, Fire-and-Forget & Missing `CancellationToken` (`FATAL`)

Blocking threads with `.Result` / `.Wait()` causes thread-pool starvation under concurrent PDF rendering load, while fire-and-forget `Task.Run` silently drops exceptions and loses work on pod shutdown.

```csharp
// - FORBIDDEN: Sync-over-async blocking, fire-and-forget Task.Run, and missing CancellationToken
public sealed class BadRenderDocumentUseCase(
    ITemplateRepository templateRepository,
    IDocumentRenderer documentRenderer)
{
    public DocumentDto Execute(RenderDocumentCommand command)
    {
        // Deadlocks / starves the .NET thread pool under load (VSTHRD002)
        var template = templateRepository.FindByIdAsync(command.TemplateId).Result;

        // Fire-and-forget loses exceptions and aborts mid-flight during pod SIGTERM (VSTHRD110)
        _ = Task.Run(() => documentRenderer.WarmupCacheAsync());

        return MapToDto(template!);
    }
}

// + MANDATORY: End-to-end async/await with mandatory CancellationToken propagation
public sealed class RenderDocumentUseCase(
    ITemplateRepository templateRepository,
    IDocumentRenderer documentRenderer)
    : IUseCase<RenderDocumentCommand, RenderDocumentResult>
{
    public async Task<RenderDocumentResult> ExecuteAsync(
        RenderDocumentCommand command,
        CancellationToken ct)
    {
        var template = await templateRepository.FindByIdAsync(command.TemplateId, ct)
            ?? throw NotFoundException.For<Template>(command.TemplateId.Value);

        var stream = await documentRenderer.RenderAsync(template, command.Payload, ct);
        return new RenderDocumentResult(stream);
    }
}
```

---

### `TRAP-EF-002` — Concurrent `DbContext` Access, Cartesian Explosion & Long Transactions (`FATAL`)

`DbContext` is not thread-safe. Running `Task.WhenAll` over queries sharing a scoped `DbContext` throws `InvalidOperationException`. Furthermore, holding a database transaction open while waiting for Gotenberg or MinIO exhausts the Npgsql connection pool.

```csharp
// - FORBIDDEN: Concurrent DbContext calls and holding a DB transaction across external I/O
public sealed class BadPublishTemplateUseCase(
    ProjectDbContext dbContext,
    IObjectStorageClient objectStorageClient)
{
    public async Task ExecuteAsync(PublishTemplateCommand command, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        // 1. Crashes at runtime: two concurrent operations on the same scoped DbContext
        var templateTask = dbContext.Templates.Include(x => x.Versions).Include(x => x.FieldMappings)
            .FirstOrDefaultAsync(x => x.Id == command.TemplateId, ct);
        var auditTask = dbContext.AuditLogs.Where(x => x.EntityId == command.TemplateId.Value).ToListAsync(ct);
        await Task.WhenAll(templateTask, auditTask);

        // 2. Holds Npgsql connection + row locks open during slow network upload to MinIO!
        await objectStorageClient.UploadAsync("templates/frozen.docx", command.Stream, ct);
        await transaction.CommitAsync(ct);
    }
}

// + MANDATORY: Split queries on multi-collection reads, perform external I/O outside DB transactions, then commit once (< 5 ms)
public sealed class PublishTemplateUseCase(
    ITemplateRepository templateRepository,
    IObjectStorageClient objectStorageClient,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IUseCase<PublishTemplateCommand, TemplateVersionDto>
{
    public async Task<TemplateVersionDto> ExecuteAsync(
        PublishTemplateCommand command,
        CancellationToken ct)
    {
        var template = await templateRepository.FindByIdWithMappingsAsync(command.TemplateId, ct)
            ?? throw NotFoundException.For<Template>(command.TemplateId.Value);

        if (template.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        // 1. Upload immutable snapshot asset to MinIO BEFORE opening any DB transaction
        var storageKey = $"templates/{template.ProjectId.Value}/{template.Id.Value}/v{template.Version + 1}.docx";
        await objectStorageClient.UploadAsync(storageKey, command.Stream, command.ContentType, ct);

        // 2. Mutate domain state and commit atomically in a single fast (< 5 ms) DB transaction
        var publishedVersion = template.PublishNewVersion(storageKey, command.EngineVersion, timeProvider.GetUtcNow());
        await unitOfWork.CommitAsync(ct);

        return TemplateVersionDto.FromDomain(publishedVersion);
    }
}
```

---

### `TRAP-MEM-003` — LOH Byte-Array Buffering & Premature Stream Disposal (`FATAL`)

Allocating `byte[]` buffers $\ge 85\text{ KB}$ places objects directly onto the Large Object Heap (`LOH`), causing GC fragmentation and `OutOfMemoryException`. Additionally, wrapping a stream returned to `FileStreamResult` in `await using` disposes the stream before Kestrel writes the response body.

```csharp
// - FORBIDDEN: Buffering full PDF into byte[] on the LOH and disposing the stream before Kestrel sends it
[HttpPost("render")]
public async Task<IActionResult> BadRenderAsync(
    [FromBody] RenderDocumentRequest request,
    CancellationToken ct)
{
    await using var memoryStream = new MemoryStream();
    await renderUseCase.RenderIntoAsync(request, memoryStream, ct);

    // Allocates multi-megabyte byte[] on LOH + memoryStream is disposed when method exits!
    var pdfBytes = memoryStream.ToArray();
    return new FileStreamResult(memoryStream, "application/pdf");
}

// + MANDATORY: Stream via HybridSpoolStream / RecyclableMemoryStream and transfer ownership to FileStreamResult
[HttpPost("render")]
public async Task<IActionResult> RenderAsync(
    [FromBody] RenderDocumentRequest request,
    CancellationToken ct)
{
    var result = await renderDocumentUseCase.ExecuteAsync(request.ToCommand(), ct);

    Response.Headers.ContentLength = result.ContentLength;
    Response.Headers["Document-Id"] = result.DocumentId.Value.ToString();

    // Do NOT wrap result.ContentStream in 'await using'; ASP.NET Core FileStreamResult owns and disposes it
    return File(result.ContentStream, result.ContentType, result.FileName, enableRangeProcessing: false);
}
```

---

### `TRAP-DATA-004` — Stripping Template Layout HTML vs. Unencoded Data Binding & Gotenberg Egress (`FATAL`)

Running an HTML tag whitelist sanitizer on a document template strips `<style>`, `<colgroup>`, and `@page` rules, destroying invoice/policy layouts. Conversely, interpolating raw tenant JSON values directly into HTML templates allows Stored/Reflected XSS inside Headless Chromium (`<script>fetch('http://169.254.169.254/...')</script>`).

```csharp
// - FORBIDDEN: Destroying template CSS with an HTML sanitizer OR injecting raw unencoded JSON values
public sealed class BadHtmlTemplateBinder
{
    public string Bind(string rawHtmlTemplate, IReadOnlyDictionary<string, string> values)
    {
        var output = rawHtmlTemplate;
        foreach (var (key, rawValue) in values)
        {
            // XSS / SSRF vector inside Chromium: rawValue can inject <script> or <iframe src="file:///etc/passwd">
            output = output.Replace($"{{{{{key}}}}}", rawValue, StringComparison.Ordinal);
        }
        return output;
    }
}

// + MANDATORY: Preserve template layout structure, HTML-encode every bound data value, and isolate Gotenberg network egress
public sealed class SafeHtmlTemplateBinder(HtmlEncoder htmlEncoder)
{
    public string Bind(string validatedTemplateHtml, IReadOnlyDictionary<string, string?> values)
    {
        var builder = new StringBuilder(validatedTemplateHtml.Length + 1024);
        builder.Append(validatedTemplateHtml);

        foreach (var (key, rawValue) in values)
        {
            var safeValue = rawValue is null ? string.Empty : htmlEncoder.Encode(rawValue);
            builder.Replace($"{{{{{key}}}}}", safeValue);
        }

        return builder.ToString();
    }
}
// Combined with Gotenberg container flags (--chromium-disable-javascript --chromium-deny-list=^file:///.* --chromium-allow-list=^data:.*)
```

---

### `TRAP-LEAK-005` — ORM & Transport Leakage into Domain / Application (`CRITICAL`)

Domain entities and repository ports must remain 100% persistence-ignorant. Decorating entities with EF Core attributes or exposing `repository.SetExpectedVersion(entity, version)` leaks ORM change-tracker mechanics into the core layers.

```csharp
// - FORBIDDEN: EF Core attributes, HttpContext, and ORM change-tracker methods in Domain/Application
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

[Table("templates")]
public class BadTemplate
{
    public Guid Id { get; set; }
    public IFormFile? UploadedFile { get; set; } // Transport leak!
}

public interface IBadTemplateRepository
{
    void SetExpectedVersion(BadTemplate template, uint xmin); // PostgreSQL / EF Core leak!
}

// + MANDATORY: Pure Domain Aggregate implementing IConcurrencyAware; EF mapping isolated in Infrastructure
public sealed class Template : AggregateRoot<TemplateId>, IConcurrencyAware
{
    public ProjectId ProjectId { get; private set; }
    public TemplateSlug Slug { get; private set; } = null!;

    public void Rename(TemplateSlug newSlug, DateTimeOffset now)
    {
        Slug = newSlug;
        SetUpdated(now); // Advances UpdatedAt and increments Version (long)
    }
}
```

---

### `TRAP-TIME-006` — Non-Deterministic Time/UUIDs, `new HttpClient()` & Captive Dependencies (`CRITICAL`)

Hardcoding `DateTime.UtcNow` or `Guid.NewGuid()` makes unit tests non-deterministic and fragments PostgreSQL B-tree indexes. Instantiating `new HttpClient()` exhausts ephemeral OS sockets (`TIME_WAIT`), and injecting a `Scoped` service (`IProjectContext`) into a `Singleton` service captures the first request's tenant forever (**cross-tenant data bleed**).

```csharp
// - FORBIDDEN: Captive Scoped dependency inside Singleton, new HttpClient(), and static DateTime/Guid
public sealed class BadWebhookDispatcher(IProjectContext captiveProjectContext) // FATAL if registered as Singleton!
{
    public async Task SendAsync(Uri callbackUri, CancellationToken ct)
    {
        using var httpClient = new HttpClient(); // Socket exhaustion!
        var payload = new { EventId = Guid.NewGuid(), SentAt = DateTime.UtcNow, ProjectId = captiveProjectContext.ProjectId };
        await httpClient.PostAsJsonAsync(callbackUri, payload, ct);
    }
}

// + MANDATORY: Inject IHttpClientFactory and TimeProvider; pass tenant/correlation metadata explicitly in the payload
public sealed class WebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : IWebhookDispatcher
{
    public async Task SendAsync(
        ProjectId projectId,
        Uri validatedCallbackUri,
        ReadOnlyMemory<byte> payloadUtf8,
        CancellationToken ct)
    {
        var httpClient = httpClientFactory.CreateClient("TenantWebhookClient");
        using var request = new HttpRequestMessage(HttpMethod.Post, validatedCallbackUri)
        {
            Content = new ReadOnlyMemoryContent(payloadUtf8)
        };
        request.Headers.Add("Webhook-Id", Guid.CreateVersion7().ToString());
        request.Headers.Add("Webhook-Timestamp", timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("Project-Id", projectId.Value.ToString());

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

---

### `TRAP-NULL-007` — Null-Forgiving (`!`) Abuse, Primitive Obsession & Public Setters (`HIGH`)

Suppressing compiler warnings with `user!.Profile!.Name!` turns compile-time safety into production `NullReferenceException` crashes. Using raw `string` or `Guid` parameters allows accidental argument swapping (`Swap(projectId, templateId)`).

```csharp
// - FORBIDDEN: Null-forgiving operator (!) on domain logic, primitive types, and public setters
public sealed class BadDocumentService
{
    public void Approve(Guid projectId, Guid documentId, string status)
    {
        var doc = FindDocument(documentId)!; // Throws NullReferenceException if missing
        doc.Status = status;                 // Any arbitrary string ("APPROVED_TYPO") compiles!
    }
}

// + MANDATORY: Strongly-Typed IDs, explicit null guard throwing Domain/Application exception, and rich state transitions
public sealed class ApproveDocumentUseCase(
    IDocumentRepository documentRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IUseCase<ApproveDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> ExecuteAsync(
        ApproveDocumentCommand command,
        CancellationToken ct)
    {
        var document = await documentRepository.FindByIdAsync(command.DocumentId, ct)
            ?? throw NotFoundException.For<Document>(command.DocumentId.Value);

        document.Approve(timeProvider.GetUtcNow());
        await unitOfWork.CommitAsync(ct);

        return DocumentDto.FromDomain(document);
    }
}
```

---

### `TRAP-VOID-008` — Swallowing Exceptions, Log-and-Rethrow Noise & Catching Cancellation (`CRITICAL`)

Logging an exception in a Repository, logging it again in a UseCase, and logging it a third time in `GlobalExceptionHandler` floods structured logs with duplicate alerts for a single failure. Catching `OperationCanceledException` when a browser disconnects pollutes 5xx error rates.

```csharp
// - FORBIDDEN: Log-and-rethrow inside inner layers or swallowing OperationCanceledException as a 500 error
public async Task<TemplateDto> BadExecuteAsync(GetTemplateQuery query, CancellationToken ct)
{
    try
    {
        return await templateReadModel.FindByIdAsync(query.TemplateId, ct)
            ?? throw NotFoundException.For<Template>(query.TemplateId.Value);
    }
    catch (Exception ex)
    {
        // Duplicate log noise + catches normal client disconnects (OperationCanceledException)!
        logger.LogError(ex, "Failed to get template");
        throw;
    }
}

// + MANDATORY: Let exceptions bubble cleanly to GlobalExceptionHandler; filter OperationCanceledException in background loops
public async Task RunWorkerIterationAsync(CancellationToken stoppingToken)
{
    try
    {
        await jobProcessor.ProcessNextBatchAsync(stoppingToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogError(ex, "Unhandled worker iteration failure; backing off before retry.");
    }
}
```

---

### `TRAP-AUDIT-009` — Unbounded OLTP Audit Payloads, `string.Length` vs. UTF-8 Bytes & Raw PII (`CRITICAL`)

Thai UTF-8 characters consume `3 bytes` per character. Checking `rawJson.Length <= 16_384` can allow nearly `48 KB` of UTF-8 bytes into an OLTP `jsonb` column, bloating PostgreSQL TOAST tables.

```csharp
// - FORBIDDEN: Checking string.Length instead of UTF-8 byte count and storing multi-MB payloads in OLTP rows
public GenerationLog BadCreateLog(Guid documentId, string rawJsonPayload)
{
    // Bug: 16,000 Thai characters = 48,000 UTF-8 bytes! Also logs raw unredacted PII into PostgreSQL.
    var inlineJson = rawJsonPayload.Length <= 16_384 ? rawJsonPayload : null;
    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(rawJsonPayload)));
    return new GenerationLog(documentId, inlineJson, hash);
}

// + MANDATORY: Measure exact UTF-8 byte count without extra string allocation, hash UTF-8 bytes, and offload > 16 KB payloads
public sealed class GenerationAuditRecorder(IOutboxPayloadOffloader payloadOffloader)
{
    private const int MaxInlinePayloadBytes = 16_384;

    public GenerationPayloadSnapshot PrepareSnapshot(
        ProjectId projectId,
        DocumentId documentId,
        string sanitizedJsonPayload)
    {
        var utf8ByteCount = Encoding.UTF8.GetByteCount(sanitizedJsonPayload);
        Span<byte> hashBuffer = stackalloc byte[SHA256.HashSizeInBytes];

        var rentedBytes = ArrayPool<byte>.Shared.Rent(utf8ByteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(sanitizedJsonPayload.AsSpan(), rentedBytes);
            SHA256.HashData(rentedBytes.AsSpan(0, written), hashBuffer);
            var payloadSha256 = Convert.ToHexStringLower(hashBuffer);

            if (utf8ByteCount <= MaxInlinePayloadBytes)
            {
                return new GenerationPayloadSnapshot(sanitizedJsonPayload, OverflowStorageKey: null, payloadSha256, utf8ByteCount);
            }

            var overflowKey = payloadOffloader.EnqueueArchive(projectId, documentId, rentedBytes.AsMemory(0, written));
            return new GenerationPayloadSnapshot(InlineJson: null, overflowKey, payloadSha256, utf8ByteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBytes, clearArray: true);
        }
    }
}
```

---

### `TRAP-EVENT-010` — In-Process Post-Commit Domain Event Dispatch (`CRITICAL`)

Publishing domain events via an in-memory mediator *after* `await unitOfWork.CommitAsync(ct)` introduces a fatal dual-write gap: if the pod crashes or restarts milliseconds after the database commit, downstream side effects (webhooks, audit archival, cache invalidation) are permanently lost.

```csharp
// - FORBIDDEN: Dispatching domain events in-memory after CommitAsync (Dual-Write / Lost Event Vulnerability)
public async Task BadCommitAndPublishAsync(Template template, CancellationToken ct)
{
    await unitOfWork.CommitAsync(ct);

    // If the pod crashes or is killed here, these domain events are lost forever!
    foreach (var domainEvent in template.DomainEvents)
    {
        await inMemoryPublisher.PublishAsync(domainEvent, ct);
    }
    template.ClearDomainEvents();
}

// + MANDATORY: Transactional Outbox Pattern — OutboxInterceptor persists domain events in the SAME DB transaction
public sealed class OutboxSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        if (eventData.Context is not { } dbContext)
        {
            return base.SavingChangesAsync(eventData, result, ct);
        }

        var aggregates = dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var now = timeProvider.GetUtcNow();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                dbContext.Set<OutboxMessage>().Add(OutboxMessage.CreateFrom(domainEvent, now));
            }
            aggregate.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

---

### `TRAP-SQL-011` — SSRF, DNS Rebinding & Unbounded Queries on External Tenant Databases (`FATAL`)

Allowing tenants to configure external SQL data sources without IP validation enables attackers to probe internal cloud metadata (`169.254.169.254`), local PostgreSQL (`127.0.0.1`), or internal Kubernetes services (`10.0.0.0/8`), and execute destructive `DROP TABLE` or runaway cross-join queries.

```csharp
// - FORBIDDEN: Raw string connection building, unvalidated hostname (SSRF), and read-write query execution
public async Task<IEnumerable<dynamic>> BadQueryTenantDatabaseAsync(
    string host,
    string database,
    string rawSql,
    CancellationToken ct)
{
    // 1. Connection-string injection & SSRF to 169.254.169.254 or 127.0.0.1!
    await using var connection = new NpgsqlConnection($"Host={host};Database={database};Username=postgres;Password=secret");
    await connection.OpenAsync(ct);

    // 2. Executes arbitrary DDL/DML with no timeout and no row limit!
    return await connection.QueryAsync(rawSql);
}

// + MANDATORY: Validate & pin IP via ConnectionHostValidator, use DbConnectionStringBuilder, enforce read-only & statement timeout
public sealed class ExternalPostgresQueryExecutor(ConnectionHostValidator hostValidator)
{
    private const int MaxRowLimit = 10_000;

    public async Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteReadOnlyQueryAsync(
        TenantDataSourceConfig config,
        string parameterizedSql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        // 1. Resolve DNS once, normalize IPv4-mapped IPv6, verify against platform deny-list + tenant allow-list CIDRs
        var pinnedIp = await hostValidator.ResolveAndValidateAsync(config.Hostname, config.AllowedEgressCidrs, ct);

        // 2. Construct connection string safely via DbConnectionStringBuilder pinning the validated IP address
        var connectionBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = pinnedIp.ToString(),
            Port = config.Port,
            Database = config.DatabaseName,
            Username = config.ReadOnlyUsername,
            Password = config.DecryptedPassword,
            SslMode = SslMode.VerifyFull,
            CommandTimeout = 5,
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(connectionBuilder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await connection.ExecuteAsync("SET LOCAL transaction_read_only = on; SET LOCAL statement_timeout = '5000ms';", transaction: transaction);

        var dynamicParams = new DynamicParameters(parameters);
        using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(parameterizedSql, dynamicParams, transaction, commandTimeout: 5, cancellationToken: ct));

        var rows = new List<IDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= MaxRowLimit)
            {
                throw UnprocessableEntityException.RowLimitExceeded(MaxRowLimit);
            }

            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }
}
```

---

### `TRAP-BATCH-012` — Synchronous HTTP Batch Loops & Unbounded Memory Queues (`FATAL`)

Rendering 500 PDFs inside an HTTP request inevitably hits gateway timeouts (`504 Gateway Timeout`), while queuing jobs into `Channel.CreateUnbounded<T>()` causes `OutOfMemoryException` spikes and loses all queued jobs on pod restart.

```csharp
// - FORBIDDEN: Synchronous loop over batch items in an HTTP request or Channel.CreateUnbounded<T>()
[HttpPost("batch-render")]
public async Task<IActionResult> BadBatchRenderAsync(
    [FromBody] BatchRenderRequest request,
    CancellationToken ct)
{
    var results = new List<string>();
    foreach (var item in request.Items) // Times out after 30s and blocks Kestrel workers!
    {
        var doc = await renderDocumentUseCase.ExecuteAsync(item, ct);
        results.Add(doc.DocumentId.Value.ToString());
    }
    return Ok(results);
}

// + MANDATORY: Pipeline C — Persist batch job to PostgreSQL document_jobs and return 202 Accepted immediately
[HttpPost("/api/v1/document-jobs")]
public async Task<ActionResult<ApiResponse<DocumentJobDto>>> EnqueueDocumentGenerationAsync(
    [FromBody] EnqueueDocumentGenerationRequest request,
    CancellationToken ct)
{
    var jobDto = await enqueueDocumentGenerationUseCase.ExecuteAsync(request.ToCommand(), ct);

    Response.Headers.Location = $"/api/v1/document-jobs/{jobDto.Id}";
    Response.Headers.RetryAfter = "5";
    return Accepted(new ApiResponse<DocumentJobDto>(jobDto));
}
```

---

### `TRAP-SQL-013` — Unbounded `Task.WhenAll` & Non-Compiling `await using SemaphoreSlim` (`CRITICAL`)

Spawning `Task.WhenAll(items.Select(...))` on 1,000 items launches 1,000 simultaneous Gotenberg/DB requests. Furthermore, writing `await using var semaphore = new SemaphoreSlim(4)` **does not compile (`CS8410`)** because `SemaphoreSlim` only implements `IDisposable`, not `IAsyncDisposable`.

```csharp
// - FORBIDDEN: Unbounded Task.WhenAll OR non-compiling 'await using var semaphore = new SemaphoreSlim(4)'
public async Task BadProcessChunkAsync(IReadOnlyList<DocumentJobItem> items, CancellationToken ct)
{
    // 1. 'await using' on SemaphoreSlim fails to compile with CS8410!
    // await using var semaphore = new SemaphoreSlim(4);

    // 2. Unbounded Task.WhenAll DDoSes Gotenberg and exhausts the Npgsql connection pool
    await Task.WhenAll(items.Select(item => RenderSingleItemAsync(item, ct)));
}

// + MANDATORY: Use Parallel.ForEachAsync with explicit MaxDegreeOfParallelism and CancellationToken
public async Task ProcessChunkAsync(
    IReadOnlyList<DocumentJobItem> items,
    CancellationToken ct)
{
    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = 4,
        CancellationToken = ct
    };

    await Parallel.ForEachAsync(items, parallelOptions, async (item, itemCt) =>
    {
        await RenderSingleItemAsync(item, itemCt);
    });
}
```

---

### `TRAP-SCALE-014` — Local In-Memory Idempotency & Rate Limiting on Multi-Pod Deployments (`CRITICAL`)

*(Consolidates former `TRAP-POD-014` and `TRAP-SCALE-016`.)*
Storing `Idempotency-Key` entries in `IMemoryCache` or `ConcurrentDictionary` fails as soon as a load balancer routes a retry to Pod B while Pod A is rendering, resulting in duplicate documents. Similarly, failing to verify the request payload SHA-256 hash allows a client to reuse the same `Idempotency-Key` with different parameters.

```csharp
// - FORBIDDEN: Local ConcurrentDictionary / IMemoryCache idempotency that breaks across multiple pods
public sealed class BadInMemoryIdempotencyFilter
{
    private static readonly ConcurrentDictionary<string, object> Cache = new();

    public bool TryGetResponse(string idempotencyKey, out object? cachedResponse) =>
        Cache.TryGetValue(idempotencyKey, out cachedResponse);
}

// + MANDATORY: Durable PostgreSQL idempotency_keys scoped by (project_id, scope, key) with SHA-256 payload fingerprint
public sealed class DurableIdempotencyGuard(
    IIdempotencyRepository idempotencyRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IdempotencyDecision> AcquireOrReplayAsync(
        ProjectId projectId,
        string credentialScope,
        string idempotencyKey,
        string requestPayloadSha256,
        CancellationToken ct)
    {
        var existing = await idempotencyRepository.FindAsync(projectId, credentialScope, idempotencyKey, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestPayloadSha256, StringComparison.Ordinal))
            {
                throw new IdempotencyKeyReusedException(idempotencyKey);
            }

            return existing.IsCompleted
                ? IdempotencyDecision.Replay(existing.ResponseStatusCode, existing.ResultReferenceId!)
                : throw ConflictException.IdempotencyInProgress(idempotencyKey);
        }

        var record = IdempotencyRecord.StartInFlight(
            projectId,
            credentialScope,
            idempotencyKey,
            requestPayloadSha256,
            timeProvider.GetUtcNow());
        idempotencyRepository.Add(record);
        await unitOfWork.CommitAsync(ct);

        return IdempotencyDecision.Proceed(record.Id);
    }
}
```

---

### `TRAP-SYNC-015` — Blind Overwrite (`Last-Write-Wins`) & ORM-Coupled Concurrency (`CRITICAL`)

Updating mutable aggregates (`Template`, `Project`, `SchemaDefinition`, `WebhookEndpoint`) without verifying the client's `If-Match` version token silently overwrites concurrent edits made by other users.

```csharp
// - FORBIDDEN: Blind PUT overwrite ignoring client's If-Match version token
public async Task<TemplateResultDto> BadUpdateTemplateAsync(RenameTemplateCommand command, CancellationToken ct)
{
    var template = await templateRepository.FindByIdAsync(command.TemplateId, ct)
        ?? throw NotFoundException.For<Template>(command.TemplateId.Value);

    template.Rename(TemplateSlug.Create(command.Slug), timeProvider.GetUtcNow());
    await unitOfWork.CommitAsync(ct); // Silently clobbers any concurrent update!
    return template.ToResultDto();
}

// + MANDATORY: Two-Phase Explicit Concurrency — Step 2 Domain Version check + Step 4 EF Core ConcurrencyToken
public async Task<TemplateResultDto> ExecuteAsync(RenameTemplateCommand command, CancellationToken ct)
{
    var template = await templateRepository.FindByIdAsync(command.TemplateId, ct)
        ?? throw NotFoundException.For<Template>(command.TemplateId.Value);

    // Phase 1 (Application/Domain): Immediate deterministic check against the bound If-Match version ("v{version}")
    if (template.Version != command.ExpectedVersion)
    {
        throw PreconditionFailedException.VersionMismatch();
    }

    template.Rename(TemplateSlug.Create(command.Slug), timeProvider.GetUtcNow());

    // Phase 2 (Persistence): EF Core emits 'WHERE id = @p0 AND version = @p1'; throws DbUpdateConcurrencyException -> 412 on race
    await unitOfWork.CommitAsync(ct);

    return template.ToResultDto();
}
```

---

### `TRAP-SSOT-017` — Mutable Live Template Resolution & Snapshot Fallback in Historical/Batch Renders (`CRITICAL`)

If a legal/banking document generated 6 months ago is re-verified or a batch job runs while an admin edits the template, falling back to live `template.FieldMappings` violates audit reproducibility.

```csharp
// - FORBIDDEN: Falling back to mutable live template mappings when rendering a published version
public IReadOnlyList<FieldMappingSnapshot> BadResolveMappings(Template template, TemplateVersion version)
{
    // Fatal audit bug: if MappingsSnapshot is null or empty, silently uses mutable live FieldMappings!
    return version.MappingsSnapshot ?? template.FieldMappings.Select(FieldMappingSnapshot.FromLive).ToList();
}

// + MANDATORY: Enforce strict immutability on TemplateVersion; fail fast if frozen snapshot state is missing
public IReadOnlyList<FieldMappingSnapshot> ResolveImmutableMappings(TemplateVersion publishedVersion)
{
    if (publishedVersion.MappingsSnapshot is not { Count: > 0 } frozenMappings ||
        string.IsNullOrWhiteSpace(publishedVersion.FrozenAssetStorageKey) ||
        string.IsNullOrWhiteSpace(publishedVersion.EngineVersion))
    {
        throw BusinessRuleViolationException.CorruptedTemplateVersionSnapshot(publishedVersion.Id.Value);
    }

    return frozenMappings;
}
```

---

### `TRAP-WRAPPER-018` — Pure Pass-Through Wrapper Classes & Ad-Hoc `200 OK` Error Envelopes (`HIGH`)

*(Consolidates former `TRAP-WRAPPER-018` and `TRAP-PIPELINE-021`.)*
Creating pass-through `*Wrapper` or `*SubPipeline` classes that merely forward arguments to another service adds indirection without cohesion. Conversely, collaborators that own a real domain or infrastructure responsibility (`IDocumentRenderer`, `DataBindingEngine`) are encouraged so UseCases stay under cognitive-complexity limits. At the HTTP boundary, returning `200 OK` with `{ "isError": true }` instead of `ApiResponse<T>` (`{ "data": T }`) and RFC 9457 `ProblemDetails` violates the wire contract.

```csharp
// - FORBIDDEN: Pure pass-through wrapper class with zero domain logic AND ad-hoc 200 OK error body
public sealed class BadTemplateLookupWrapper(ITemplateReadModel templateReadModel)
{
    // Pure pass-through indirection with zero added value
    public Task<TemplateResultDto?> FindAsync(TemplateId id, CancellationToken ct) =>
        templateReadModel.FindByIdAsync(id, ct);
}

[HttpGet("{templateId:guid}")]
public async Task<IActionResult> BadGetAsync(Guid templateId, CancellationToken ct)
{
    var dto = await badWrapper.FindAsync(new TemplateId(templateId), ct);
    if (dto is null)
    {
        return Ok(new { isError = true, message = "Not found" }); // Breaks HTTP & RFC 9457 semantics!
    }
    return Ok(dto); // Missing ApiResponse<T> { "data": T } envelope!
}

// + MANDATORY: Direct UseCase execution, standard ApiResponse<T> on 2xx, and RFC 9457 ProblemDetails on 4xx/5xx
[HttpGet("{templateId:guid}")]
[ProducesResponseType<ApiResponse<TemplateResultDto>>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<TemplateResultDto>>> GetByIdAsync(
    Guid templateId,
    CancellationToken ct)
{
    var resultDto = await getTemplateByIdUseCase.ExecuteAsync(
        new GetTemplateByIdQuery(new TemplateId(templateId)),
        ct);

    return Ok(new ApiResponse<TemplateResultDto>(resultDto));
}
```

---

### `TRAP-TENANT-022` — Route/Body Tenant Spoofing, `X-Project-Id` & `403` Existence Enumeration Oracle (`FATAL`)

Accepting `projectId` from the URL route, query string, or JSON body on tenant endpoints allows an attacker to spoof another tenant's ID. Additionally, returning `403 Forbidden` when a user queries a `templateId` belonging to another tenant leaks that the UUID exists (**Existence Enumeration Oracle**).

```csharp
// - FORBIDDEN: Reading projectId from query/body, using legacy X-Project-Id, or returning 403 on cross-tenant lookup
[HttpGet("/api/v1/templates/{templateId:guid}")]
public async Task<IActionResult> BadGetTemplateAsync(
    Guid templateId,
    [FromQuery] Guid projectId, // Spoofable channel!
    CancellationToken ct)
{
    var template = await dbContext.Templates.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == new TemplateId(templateId), ct);
    if (template is null) return NotFound();
    if (template.ProjectId.Value != projectId) return Forbid(); // 403 reveals the UUID exists in another tenant!
    return Ok(template);
}

// + MANDATORY: Tenant resolved exclusively from 'Project-Id' header + credential; cross-tenant lookup returns 404 Not Found
public sealed class GetTemplateByIdUseCase(ITemplateReadModel templateReadModel)
    : IUseCase<GetTemplateByIdQuery, TemplateDto>
{
    public async Task<TemplateDto> ExecuteAsync(GetTemplateByIdQuery query, CancellationToken ct)
    {
        // Global Query Filter + PostgreSQL RLS automatically scopes query to the active ProjectId.
        // If templateId belongs to Tenant B, FindByIdAsync returns null -> indistinguishable 404 NotFoundException!
        return await templateReadModel.FindByIdAsync(query.TemplateId, ct)
            ?? throw NotFoundException.For<Template>(query.TemplateId.Value);
    }
}
```

---

### `TRAP-TENANT-023` — Archetype B Redundant Context Checks & Query Filter Bypass (`FATAL`)

In **Archetype B** (operating on an existing aggregate by ID), injecting `IProjectContext` to write manual `if (entity.ProjectId != projectContext.ProjectId)` checks duplicates infrastructure logic across 50+ UseCases. Worse, using `.IgnoreQueryFilters()` or interpolated `FromSqlRaw($"...")` completely disables tenant isolation.

```csharp
// - FORBIDDEN: Bypassing Global Query Filters or cluttering Archetype B UseCases with manual IProjectContext checks
public async Task BadDeleteTemplateAsync(DeleteTemplateCommand command, CancellationToken ct)
{
    // FATAL: IgnoreQueryFilters disables Layer 2 tenant isolation!
    var template = await dbContext.Templates
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == command.TemplateId, ct);

    if (template is not null && template.ProjectId == projectContext.ProjectId)
    {
        dbContext.Templates.Remove(template);
        await dbContext.SaveChangesAsync(ct);
    }
}

// + MANDATORY: Archetype B has ZERO IProjectContext injection; 5-Layer Defense-in-Depth enforces isolation automatically
public sealed class ArchiveTemplateUseCase(
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IUseCase<ArchiveTemplateCommand, Unit>
{
    public async Task<Unit> ExecuteAsync(ArchiveTemplateCommand command, CancellationToken ct)
    {
        var template = await templateRepository.FindByIdAsync(command.TemplateId, ct)
            ?? throw NotFoundException.For<Template>(command.TemplateId.Value);

        if (template.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        template.Archive(timeProvider.GetUtcNow());
        await unitOfWork.CommitAsync(ct);
        return Unit.Value;
    }
}
```

---

### `TRAP-TENANT-024` — Archetype A Missing `IProjectContext` & Un-Normalized Uniqueness Checks (`CRITICAL`)

In **Archetype A** (creating a new aggregate or checking tenant-scoped uniqueness), omitting `IProjectContext` leaves `ProjectId` unset (`Guid.Empty`, which is caught by `TenantWriteEnforcementInterceptor`), while checking `SlugExistsAsync` with a raw string before Value Object normalization (`" Invoice-01 "` vs. `"invoice-01"`) allows duplicate slugs past the pre-check.

```csharp
// - FORBIDDEN: Checking uniqueness with raw un-normalized string before Value Object creation
public async Task<TemplateDto> BadCreateTemplateAsync(CreateTemplateCommand command, CancellationToken ct)
{
    // Bug: command.Slug is " Invoice-01 " (not yet trimmed/lowercased), so SlugExistsAsync misses existing "invoice-01"!
    if (await templateRepository.SlugExistsAsync(projectContext.ProjectId!.Value, command.Slug, ct))
    {
        throw ConflictException.DuplicateSlug(command.Slug);
    }

    var slug = TemplateSlug.Create(command.Slug); // Normalizes to "invoice-01" too late!
    var template = Template.Create(projectContext.ProjectId.Value, slug, timeProvider.GetUtcNow());
    templateRepository.Add(template);
    await unitOfWork.CommitAsync(ct);
    return TemplateDto.FromDomain(template);
}

// + MANDATORY: Resolve required ProjectId, normalize Value Object FIRST, then check uniqueness + rely on DB uq_ constraint
public sealed class CreateTemplateUseCase(
    IProjectContext projectContext,
    ITemplateRepository templateRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct)
    {
        // Step 1: Retrieve verified ProjectId (throws TenantContextNotResolvedException if absent)
        var projectId = projectContext.GetRequiredProjectId();

        // Step 2: Construct and normalize Value Object FIRST (" Invoice-01 " -> "invoice-01")
        var slug = TemplateSlug.Create(command.Slug);

        // Step 3: Check tenant-scoped uniqueness using the normalized Value Object
        if (await templateRepository.SlugExistsAsync(projectId, slug, ct))
        {
            throw ConflictException.DuplicateSlug((string)slug);
        }

        // Step 4: Create aggregate and persist (backed by PostgreSQL uq_templates_project_id_slug)
        var template = Template.Create(projectId, slug, command.DisplayName, timeProvider.GetUtcNow());
        templateRepository.Add(template);
        await unitOfWork.CommitAsync(ct);

        return template.ToResultDto();
    }
}
```

---

### `TRAP-TENANT-025` — Standing SuperAdmin Query Filter Bypass (`FATAL`)

Adding `|| currentUser.IsSuperAdmin` to EF Core Global Query Filters grants standing cross-tenant read/write power on every ordinary `/api/v1/**` endpoint. A single compromised admin session or UI bug can then dump or mutate every customer's documents.

```csharp
// - FORBIDDEN: Standing SuperAdmin bypass inside ProjectDbContext Global Query Filter
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // FATAL VIOLATION: Disables tenant isolation across the entire application whenever an admin is logged in!
    modelBuilder.Entity<Template>()
        .HasQueryFilter(e => e.ProjectId == projectContext.ProjectId || currentUser.IsSuperAdmin);
}

// + MANDATORY: Fail-closed Global Query Filter on ProjectDbContext; explicit BreakGlassGrant on /api/v1/management/**
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Fail-closed: if HasProject is false, evaluates to false and returns ZERO rows. Never checks IsSuperAdmin.
    modelBuilder.Entity<Template>()
        .HasQueryFilter(e => projectContext.HasProject && e.ProjectId == projectContext.ProjectId);
}
```

---

### `TRAP-FE-026` — Frontend XSS, `localStorage` Token Exposure, Root `'use client'` & Blob Leaks (`HIGH`)

Storing bearer tokens or API keys in `localStorage` or `NEXT_PUBLIC_*` exposes credentials to every script on the page. Putting `'use client'` at the top of `app/layout.tsx` de-optimizes the entire Next.js App Router tree into a client bundle, and forgetting `URL.revokeObjectURL` when previewing PDFs leaks multi-megabyte blobs in browser memory.

```tsx
// - FORBIDDEN: Root 'use client', localStorage token access, dangerouslySetInnerHTML, and unrevoked Blob URL
'use client'; // De-optimizes the entire route subtree!

export function BadDocumentPreview({ htmlPreview, pdfBlob }: { htmlPreview: string; pdfBlob: Blob }) {
  const token = localStorage.getItem('access_token'); // Vulnerable to token theft!
  const blobUrl = URL.createObjectURL(pdfBlob);       // Memory leak: never revoked!

  return (
    <div>
      <div dangerouslySetInnerHTML={{ __html: htmlPreview }} />
      <iframe src={`${blobUrl}#token=${token}`} />
    </div>
  );
}

// + MANDATORY: Scoped Client Leaf Component, deterministic URL.revokeObjectURL cleanup, and sandboxed PDF preview
'use client';

import { useEffect, useState } from 'react';

export interface PdfBlobPreviewProps {
  readonly pdfBlob: Blob;
  readonly documentTitle: string;
}

export function PdfBlobPreview({ pdfBlob, documentTitle }: PdfBlobPreviewProps) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  useEffect(() => {
    const url = URL.createObjectURL(pdfBlob);
    setObjectUrl(url);

    return () => {
      URL.revokeObjectURL(url);
    };
  }, [pdfBlob]);

  if (!objectUrl) {
    return <div role="status">กำลังเตรียมตัวอย่างเอกสาร...</div>;
  }

  return (
    <iframe
      src={objectUrl}
      title={documentTitle}
      className="h-full w-full border-0"
      sandbox="allow-same-origin"
    />
  );
}
```

---

## 4. Automated Static Enforcement Configuration (`BannedSymbols.txt`)

To enforce these traps at compile time, `Microsoft.CodeAnalysis.BannedApiAnalyzers` is configured across `SmkDoc.Domain`, `SmkDoc.Application`, `SmkDoc.Infrastructure`, and `SmkDoc.Api` with the following `BannedSymbols.txt` baseline:

```text
# TRAP-TIME-006: Determinism & UUIDv7
P:System.DateTime.Now;Use injected TimeProvider.GetUtcNow() instead (TRAP-TIME-006).
P:System.DateTime.UtcNow;Use injected TimeProvider.GetUtcNow() instead (TRAP-TIME-006).
P:System.DateTimeOffset.Now;Use injected TimeProvider.GetUtcNow() instead (TRAP-TIME-006).
P:System.DateTimeOffset.UtcNow;Use injected TimeProvider.GetUtcNow() instead (TRAP-TIME-006).
M:System.Guid.NewGuid;Use Guid.CreateVersion7() for time-ordered UUIDs (TRAP-TIME-006).
M:System.Net.Http.HttpClient.#ctor;Use IHttpClientFactory instead of instantiating HttpClient directly (TRAP-TIME-006).

# TRAP-MEM-003: Zero-LOH Streaming
M:System.IO.MemoryStream.ToArray;Forbidden LOH allocation; stream via HybridSpoolStream or RecyclableMemoryStream (TRAP-MEM-003).
M:System.IO.File.ReadAllBytes(System.String);Forbidden LOH allocation; use FileStream with pooled 64 KB buffers (TRAP-MEM-003).
M:System.IO.File.ReadAllBytesAsync(System.String,System.Threading.CancellationToken);Forbidden LOH allocation; use FileStream with pooled 64 KB buffers (TRAP-MEM-003).

# TRAP-BATCH-012: Unbounded Queues
M:System.Threading.Channels.Channel.CreateUnbounded``1;Unbounded in-memory channels are forbidden; use PostgreSQL document_jobs or Channel.CreateBounded (TRAP-BATCH-012).
M:System.Threading.Channels.Channel.CreateUnbounded``1(System.Threading.Channels.UnboundedChannelOptions);Unbounded in-memory channels are forbidden; use PostgreSQL document_jobs or Channel.CreateBounded (TRAP-BATCH-012).

# TRAP-TENANT-023 & TRAP-SQL-011: Multi-Tenant & SQL Injection Bypass
M:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters``1(System.Linq.IQueryable{``0});Forbidden in Domain/Application/ProjectDbContext; tenant isolation must never be bypassed (TRAP-TENANT-023).
M:Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlRaw``1(Microsoft.EntityFrameworkCore.DbSet{``0},System.String,System.Object[]);Forbidden SQL injection risk; use LINQ or FromSqlInterpolated (TRAP-SQL-011).
```
