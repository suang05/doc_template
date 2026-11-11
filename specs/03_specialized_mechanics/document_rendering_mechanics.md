# [MECH-002]: document_rendering_mechanics.md — Rendering Engines, OpenXML & Thai Formatting SSoT

<context>
Category: 03_specialized_mechanics
Domain: Document Rendering Engine, Template Manifest SSoT & Thai Typography
SoleResponsibilities:
  - Polymorphic Render Engine Factory (`IRenderEngineFactory`) and Gotenberg Chromium PDF synthesis
  - Template Version Manifest SSoT (`TemplateVersion.MappingsSnapshot` immutable runtime deserialization)
  - Direct pipeline DataBinding integration (`IDataBindingEngine`) and Unified Preview Gateway execution
  - Open/Closed linguistic localization strategy (`ILocalizedDataFormatter`, `ThaiDataFormatter`, BE +543, Baht text, Sarabun font)
  - OpenXML DrawingML graphic ID normalization and ClosedXML A4 fit-to-page automation
Target: `backend-v2/` (`SmkDoc.Infrastructure/Engines/`, `SmkDoc.Infrastructure/Pdf/`, `SmkDoc.Application/Modules/Rendering/`, `SmkDoc.Application/Modules/DataBinding/`)
Signal: [ARCH_EVOLUTION_RENDERING]
</context>

---

## 1. Level 1: Document Rendering Invariants Matrix

### 1.1 Architectural Wiring & Rendering Contracts
| Direction | Upstream Origin | Current Role (Document Synthesis Core) | Downstream Destination |
|---|---|---|---|
| **Render Request** | Application UseCase (`RenderDocumentUseCase`)| Resolves polymorphic engine via factory | `IRenderEngine` Implementation |
| **Data Binding** | Raw JSON Payload + Version Manifest | Binds external datasets & transforms data | `IDataBindingEngine` |
| **Data Enrichment**| Bound Data JSON | Applies localized formatting (BE +543, currency words) | Strategy `ILocalizedDataFormatter` |
| **HTML / Chromium**| HTML Template + CSS + Google Fonts | Synthesizes PDF via Gotenberg 8 | Gotenberg HTTP Microservice |
| **DOCX Synthesis** | OpenXML Document Package | Fixes DrawingML `nvPr.Id` graphic IDs via document-scoped generator | Native Word Document Stream |
| **XLSX Synthesis** | ClosedXML Workbook Package | Configures A4 portrait 1-page fit | Native Excel Document Stream |
| **Streaming Egress**| Engine Render Result | Direct Stream pass-through with bounded buffer | HTTP Wire Response (`Response.Body`) |
| **Storage Egress** | MinIO S3 Object Store | Server-side reverse-proxy streaming via Gateway | Client Download (Zero 307 Redirects) |

### 1.2 Affirmative Document Rendering Invariants
| Rendering Architectural Concern | Affirmative Rendering Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Zero-LOH Stream Purity** | Render and PDF contracts expose pure `Task<Stream>` types. Output streams are piped with bounded `81,920`-byte buffers, dry-run validations drain to `Stream.Null`, and seekable packages use `HybridSpoolStream`. Multi-MB `byte[]` arrays are prohibited. | Memory & GC Boundary |
| **Template Manifest SSoT** | Runtime document generation strictly deserializes the immutable version manifest (`TemplateVersion.MappingsSnapshot`), bypassing relational `field_mappings` queries to guarantee version immutability and zero-drift execution. Version creation workflows (`CreateTemplate`, `SaveTemplateHtml`, `CommitTemplateDraft`) must guarantee non-null frozen `MappingsSnapshot` persistence. | Data Binding Boundary |
| **Unified Preview Gateway** | Live preview requests (Monaco raw HTML, RAM-cached template drafts, template slug, or template UUID) route exclusively through the unified `PreviewDocumentUseCase` with fail-fast JSON Schema Draft-07 validation. Duplicate preview use cases are prohibited. | Preview Perimeter |
| **Polymorphic Engine Resolution**| Document synthesis resolves engines dynamically via `IRenderEngineFactory.GetEngine(engineType)` using an immutable `FrozenDictionary`. Hardcoded switch/if-else branching is prohibited. | Engine Extensibility |
| **Storage Perimeter Isolation** | Generated documents and templates stream server-side through API Gateway (`File(stream, ...)`). Emitting HTTP 307 redirects or raw internal MinIO URLs to clients is prohibited. | Network Perimeter |
| **2-Track Rendering Pipeline** | Rendering architecture is partitioned into Track 1 (Synchronous Monaco Live Preview with `rendering` rate limit 30 req/min) and Track 2 (Asynchronous Batch Queue via `POST generate-async`, `202 Accepted` + `Location` header, backed by Redis Streams with Consumer Groups `XADD`/`XREADGROUP` and drained by `DocumentRenderQueueWorker` with `SemaphoreSlim(4)` backpressure). | Workload Partitioning |
| **Chromium Pod Protection** | Document rendering endpoints enforce the dedicated `rendering` rate-limiting policy (30 req/min per tenant) returning RFC 9457 ProblemDetails (`429 Too Many Requests`). | Service Resilience |
| **OpenXML Graphic Frame IDs** | All OpenXML drawing elements are intercepted via document-scoped `IDrawingMlIdGenerator` to enforce explicit positive non-zero integers ($\ge 1$) for `nvPr.Id`. Prohibit global static mutable counters or zero/duplicate graphic IDs. | Document Integrity |
| **Excel Print-Width Clamping** | All ClosedXML spreadsheet worksheets apply A4 portrait fit: `worksheet.PageSetup.FitToPages(1, 0)` and `AdjustToContents()`. Horizontal content clipping is prohibited. | Spreadsheet Geometry |
| **Linguistic Localization Determinism** | Thai Buddhist Era calendar conversion (+543 years), Baht text currency, and international formatting execute exclusively through injectable `ILocalizedDataFormatter` strategies (default: `ThaiDataFormatter` for `th-TH`). Inline date math in templates is prohibited. | Linguistic Determinism |
| **Thai Font Injection** | HTML render templates inject Google Fonts Sarabun font declarations via `HtmlLayoutProcessor` to guarantee accurate tone mark positioning in headless Chromium. | Font Rendering |
| **Ephemeral Storage Purging** | Expired MinIO document outputs in `StorageBuckets.Outputs` are purged on a background schedule via `DocumentRetentionWorker` using `TimeProvider.GetUtcNow()`. | Storage Lifecycle |
| **Pure Payload Ingestion** | JSON payloads are validated against JSON Schema Draft-07 fail-fast during generation without mutating payload data or duplicating validation endpoints. | Ingestion Pipeline |
| **Asynchronous Streaming IO** | All rendering, network piping, and storage interactions execute 100% asynchronously with forwarded `CancellationToken`. Synchronous blocking (`.Result`, `.Wait()`) is prohibited. | Concurrency Safety |

---

## 2. Level 2: Core Rendering SSoT ASTs & Cognitive Rendering Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why 2-Track Rendering Architecture:** Live preview in Monaco Studio requires sub-second synchronous turnaround to provide instant visual feedback. High-volume ERP batch generation (e.g. 5,000 invoices at month-end) would exhaust Chromium memory pods if submitted synchronously. Decoupling batch into an asynchronous job queue (`202 Accepted`) allows background workers to drain tasks at a concurrency rate matched to Gotenberg pod capacity.
* **Why Banning HTTP 307 Redirects to Internal MinIO:** MinIO operates inside an internal container network (`http://minio:9000`). Emitting a 307 redirect exposes private infrastructure endpoints to outside clients, breaks corporate firewalls, and leaks storage topology. Streaming through the API gateway enforces Zero-Trust isolation.
* **Why 81,920-Byte Buffer Threshold:** The .NET runtime allocates objects $\ge 85,000$ bytes on the Large Object Heap (LOH). The LOH is never compacted during Gen 0/1 sweeps; continuous multi-MB PDF buffering produces severe LOH fragmentation and costly Gen 2 stop-the-world GC pauses. Direct streaming bounded at 81,920 bytes completely avoids LOH allocation.
* **Why Polymorphic Engine Factory with FrozenDictionary:** Using `IRenderEngineFactory` with `FrozenDictionary<RenderEngineType, IRenderEngine>` provides $O(1)$ zero-allocation engine lookup while honoring the Open/Closed Principle.
* **Why OpenXML DrawingML Graphic Frame Non-Zero IDs:** Microsoft Word strictly rejects OpenXML packages where image or chart graphic frame IDs are `0` or duplicated, triggering file corruption warnings. Scoping sequential ID generation per document render session guarantees ECMA-376 compliance while preserving stateless horizontal scalability.
* **Why Pluggable Linguistic Localization Strategy:** Thai Buddhist Era dates, Baht text, and international formatting require specialized grammatical rules; isolating formatters behind `ILocalizedDataFormatter` ensures 100% deterministic outputs across formats while honoring the Open/Closed Principle for global enterprise expansion.
* **Why Template Version Manifest over Relational Field Mappings in Runtime:** If the runtime engine queries the mutable `field_mappings` table, edits made in authoring or draft rollback sessions immediately alter the output of previously published versions. Freezing mappings into `TemplateVersion.MappingsSnapshot` at commit/publish time guarantees 100% deterministic reproducibility and enables zero-DB high-throughput caching.
* **Why Redis Streams over Simple List Queues (RPOP):** Lossy RPOP drops jobs if a worker crashes before completion. Redis Streams (`XADD`, `XREADGROUP`, `XACK`, `XDEL`) with Consumer Groups provide at-least-once delivery, consumer failure detection, and automatic pending job reclamation (`XCLAIM`).

### 2.2 Proactive Rendering Guards
```yaml
MemoryStreamProhibitionGuard:
  Invariant: "Render engines and controllers must never buffer document outputs in byte[]."
  ActionOnViolation: "Enforce live Stream pass-through with bounded 81,920 buffer."

MinioRedirectProhibitionGuard:
  Invariant: "Controllers must never return HTTP 307 redirects to internal MinIO instances."
  ActionOnViolation: "Stream binary content directly through gateway using File(stream, ...)."

OpenXmlZeroIdGuard:
  Invariant: "OpenXML NonVisualDrawingProperties.Id must not be 0, null, or shared globally across requests."
  ActionOnViolation: "Invoke drawingMlIdGenerator.EnsureNonZeroGraphicFrameId(nvPr) using document-scoped generator."

ExcelPrintFitGuard:
  Invariant: "ClosedXML worksheets must fit horizontally on a single page."
  ActionOnViolation: "Call worksheet.PageSetup.FitToPages(1, 0) and AdjustToContents()."

LinguisticFormattingGuard:
  Invariant: "Templates must not perform inline date math or currency conversions."
  ActionOnViolation: "Route transformations through injectable ILocalizedDataFormatter strategy."
```

### 2.3 Polymorphic Rendering Engine Strategy Contracts
```csharp
public interface IRenderEngine
{
    RenderEngineType EngineType { get; }
    Task<Stream> RenderStreamAsync(
        Stream templateStream,
        string inputDataJson,
        OutputFormat outputFormat,
        CancellationToken ct = default);
}

public interface IRenderEngineFactory
{
    IRenderEngine GetEngine(RenderEngineType engineType);
}
```

### 2.4 Handlebars Authoritative Helpers Catalog
| Helper Name | Usage in Template | Output Description |
|---|---|---|
| `thai_date` | `{{thai_date date_field}}` | Formats date into Buddhist Era (e.g. `21 ตุลาคม 2569`) |
| `thai_baht_text` | `{{thai_baht_text amount}}` | Converts decimal to Thai Baht text (e.g. `หนึ่งพันสองร้อยบาทถ้วน`) |
| `format_number` | `{{format_number amount 2}}` | Formats number with commas and decimal places (`1,200.00`) |
| `format_phone` | `{{format_phone phone}}` | Formats 10-digit phone number (`081-234-5678`) |
| `format_id_card` | `{{format_id_card id_card}}` | Formats 13-digit Thai National ID (`1-2345-67890-12-3`) |
| `eq` | `{{#if (eq status "Published")}}` | Equality comparator helper |
| `neq` | `{{#if (neq status "Draft")}}` | Inequality comparator helper |
| `lookup` | `{{lookup items @index}}` | Dynamic dictionary or array lookup helper |

---

## 3. Level 3: Canonical Working Archetypes (100% Fully-Hydrated Recipes)

### 3.1 Gotenberg Direct Response Streaming Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-001]:
* Uses `HttpCompletionOption.ResponseHeadersRead` when invoking external Gotenberg rendering endpoints.
* Wraps `response.Content.ReadAsStreamAsync(ct)` in a `ResponseDisposingStream` so `HttpResponseMessage` is deterministically disposed on stream close.
* Spools unseekable engine streams via `HybridSpoolStream` (81,920 bytes) before storage upload (Zero Large Object Heap allocation).

```csharp
public sealed class GotenbergHtmlRenderer(HttpClient httpClient)
{
    public async Task<Stream> RenderHtmlToPdfStreamAsync(
        string html,
        string? headerHtml = null,
        string? footerHtml = null,
        CancellationToken ct = default)
    {
        // Step 1: Prepare Form Content <Why: pass HTML/CSS parts to Chromium headless>
        using var content = new MultipartFormDataContent();
        var htmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(htmlContent, "files", "index.html");

        if (!string.IsNullOrWhiteSpace(headerHtml))
        {
            var hContent = new ByteArrayContent(Encoding.UTF8.GetBytes(headerHtml));
            hContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(hContent, "files", "header.html");
        }

        if (!string.IsNullOrWhiteSpace(footerHtml))
        {
            var fContent = new ByteArrayContent(Encoding.UTF8.GetBytes(footerHtml));
            fContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            content.Add(fContent, "files", "footer.html");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html")
        {
            Content = content
        };

        // Step 2: Send with ResponseHeadersRead <Why: avoid whole-body RAM buffering>
        var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!httpResponse.IsSuccessStatusCode)
        {
            try
            {
                var errorDetail = await httpResponse.Content.ReadAsStringAsync(ct);
                throw new DocumentRenderException($"Gotenberg rendering failed ({httpResponse.StatusCode}): {errorDetail}");
            }
            finally
            {
                httpResponse.Dispose();
            }
        }

        // Step 3: Wrap in ResponseDisposingStream <Why: releases HTTP socket when consumer closes stream>
        try
        {
            var innerStream = await httpResponse.Content.ReadAsStreamAsync(ct);
            return new ResponseDisposingStream(innerStream, httpResponse);
        }
        catch
        {
            httpResponse.Dispose();
            throw;
        }
    }
}

public sealed class ResponseDisposingStream(Stream innerStream, HttpResponseMessage response) : Stream
{
    public override bool CanRead => innerStream.CanRead;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => innerStream.CanWrite;
    public override long Length => innerStream.Length;
    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush() => innerStream.Flush();
    public override Task FlushAsync(CancellationToken ct) => innerStream.FlushAsync(ct);

    public override int Read(byte[] buffer, int offset, int count) =>
        innerStream.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        innerStream.ReadAsync(buffer, offset, count, ct);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
        innerStream.ReadAsync(buffer, ct);

    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        innerStream.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        innerStream.WriteAsync(buffer, offset, count, ct);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) =>
        innerStream.WriteAsync(buffer, ct);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
            response.Dispose(); // Invariant: Releases underlying socket connection deterministically
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await innerStream.DisposeAsync();
        response.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

### 3.2 Unified Live Preview Gateway Archetype (`Preview{Resource}UseCase` & `Preview{Resource}Query`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-002]:
* Unified preview entry-point supporting: 1) Monaco raw HTML, 2) RAM-cached Template Draft ID, 3) Template Slug, 4) Template UUID.
* Zero persistence: NEVER uploads to MinIO output bucket, NEVER logs to audit tables, NEVER increments version counters.
* Direct in-memory stream return to browser (`application/pdf`) with `inline` disposition.

```csharp
public sealed record Preview{Resource}Query(
    JsonElement Payload,
    string? Html = null,
    string? Slug = null,
    Guid? {Aggregate}Id = null,
    string? DraftId = null
);

public sealed class Preview{Resource}UseCase(
    I{Aggregate}Repository repository,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IDataBindingEngine? dataBindingEngine = null,
    ITemplateDraftCache? draftCache = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<Preview{Resource}Query, Stream>
{
    public async Task<Stream> ExecuteAsync(Preview{Resource}Query query, CancellationToken ct = default)
    {
        Stream templateStream;
        RenderEngineType engineType;
        string dataJson;

        // Path 1: Monaco editor raw HTML preview
        if (!string.IsNullOrWhiteSpace(query.Html))
        {
            templateStream = await HybridSpoolStream.CreateFromUtf8StringAsync(query.Html, ct: ct);
            engineType = RenderEngineType.Html;
            dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";
        }
        // Path 2: RAM-cached template draft preview (authoring studio)
        else if (!string.IsNullOrWhiteSpace(query.DraftId))
        {
            var projectId = projectContext.EnsureResolved("Previewing a draft requires an active project context.");
            if (draftCache is null)
                throw new BusinessRuleViolationException("Draft cache is not available for preview.", "DRAFT_CACHE_UNAVAILABLE");

            var draft = await draftCache.GetAsync(projectId, query.DraftId, ct)
                ?? throw new DraftExpiredException(query.DraftId);
            if (!TemplateFormat.TryFromExtension(draft.FileExtension, out var format))
                throw new DomainValidationException($"Unsupported template file format: '{draft.FileExtension}'.");

            engineType = format.DefaultEngineType;
            templateStream = await draftCache.OpenStreamAsync(draft, ct);
            dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";
        }
        // Path 3 & 4: Published template by ID or Slug
        else
        {
            (templateStream, engineType, dataJson) = await ResolvePublishedTemplateStreamAsync(query, ct);
        }

        // Render Ephemeral Stream <Why: zero MinIO persistence, zero generation log mutations>
        try
        {
            var engine = engineFactory.GetEngine(engineType);
            return await engine.RenderStreamAsync(templateStream, dataJson, OutputFormat.Pdf, ct);
        }
        finally
        {
            await templateStream.DisposeAsync();
        }
    }

    private async Task<(Stream Stream, RenderEngineType Engine, string DataJson)> ResolvePublishedTemplateStreamAsync(
        Preview{Resource}Query query, CancellationToken ct)
    {
        var aggregate = query.{Aggregate}Id.HasValue
            ? await repository.GetByIdAsync(query.{Aggregate}Id.Value, ct)
            : await repository.GetBySlugAsync(query.Slug!, ct);

        if (aggregate == null)
            throw new NotFoundException($"Template not found.");

        var version = aggregate.GetPublishedVersion();
        string dataJson;
        if (dataBindingEngine != null)
        {
            var boundData = await dataBindingEngine.BindAndValidateAsync(aggregate, version, query.Payload, skipValidation: false, ct);
            if (boundData.ValidationResult is { IsValid: false })
            {
                throw new SchemaValidationException(
                    aggregate.Slug.Value, version.Version, boundData.ValidationResult.Errors);
            }
            dataJson = boundData.DataJson;
        }
        else
        {
            dataJson = query.Payload.ValueKind != JsonValueKind.Undefined ? query.Payload.GetRawText() : "{}";
        }

        var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        return (stream, version.GetRenderEngineType(), dataJson);
    }
}
```

### 3.3 Template Version Manifest SSoT & Data Binding Engine Archetype (`DataBindingEngine`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-003]:
* Prioritizes the immutable `{Aggregate}Version.MappingsSnapshot` manifest over relational `field_mappings` tables.
* Resolves external SQL dataset configurations via `IDatasetResolver` (4 dependencies: repo, data protection, validator).
* Executes dataset queries in parallel with bounded semaphore concurrency (Max 4) and SHA-256 fingerprint caching via `IDatasetBatchExecutor`.
* Applies field mappings, JSON path resolution, and math expressions directly without intermediate wrapper indirection.
* Enforces fail-fast Draft-07 JSON Schema validation.

```csharp
public sealed class DataBindingEngine(
    IDatasetResolver datasetResolver,
    IDatasetBatchExecutor datasetExecutor,
    IMathExpressionResolver mathResolver,
    IJsonSchemaValidationService schemaValidation,
    TimeProvider timeProvider) : IDataBindingEngine
{
    public async Task<BoundDocumentData> BindAndValidateAsync(
        {Aggregate} aggregate,
        {Aggregate}Version version,
        JsonElement rawPayload,
        bool skipValidation,
        CancellationToken ct = default)
    {
        // Step 1: Resolve Field Mappings from Manifest SSoT <Why: version snapshot prevents relational drift>
        var mappings = !string.IsNullOrWhiteSpace(version.MappingsSnapshot)
            ? FieldMappingSnapshotSerializer.Deserialize(aggregate.Id, version.MappingsSnapshot, timeProvider.GetUtcNow())
            : aggregate.FieldMappings.ToList();

        // Step 2: Resolve External SQL Dataset Configurations <Why: decrypt connections and validate queries>
        var aliasMap = aggregate.TemplateDatasets.Count > 0
            ? await datasetResolver.ResolveAssignmentsAsync(aggregate.TemplateDatasets, ct)
            : new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        // Step 3: Direct In-Memory Mapping, Batch Query Execution & Math Evaluation
        var boundDict = InitializePayloadDict(rawPayload);
        BindJsonMappings(rawPayload, mappings, boundDict);

        if (aliasMap.Count > 0)
        {
            var aliasResults = await datasetExecutor.ExecuteBatchAsync(aliasMap, boundDict, aggregate.ProjectId.Value, ct);
            BindSqlMappings(aliasResults, mappings, boundDict);
        }

        EvaluateMathExpressions(mappings, boundDict);

        string dataJson = boundDict.Count > 0
            ? JsonSerializer.Serialize(boundDict, JsonOptions)
            : (rawPayload.ValueKind != JsonValueKind.Undefined ? rawPayload.GetRawText() : "{}");

        // Step 4: Fail-Fast JSON Schema Gate <Why: prevent rendering invalid data against Draft-07 schema>
        var validation = !skipValidation && !string.IsNullOrWhiteSpace(version.DataSchema)
            ? schemaValidation.Validate(version.DataSchema, dataJson)
            : null;

        return new BoundDocumentData(dataJson, validation);
    }
}
```

### 3.4 Linear PEMR Generation Pipeline Archetype (`Generate{Resource}UseCase`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-004]:
* Direct linear 4-step PEMR execution pipeline inside `Generate{Resource}UseCase` (Zero intermediate pipeline wrappers).
* Connects Application directly to `IDataBindingEngine` and `IRenderEngineFactory`.
* Enforces fail-fast JSON Schema Draft-07 validation with immediate validation failure auditing and rollback.
* Spools unseekable engine outputs to `HybridSpoolStream` (bounded at 81,920 bytes) before MinIO S3 upload.
* Provides full transactional resilience: presigned download URL generation, document versioning, success audit, atomic commit, and compensating storage deletion if post-upload commit fails.

```csharp
public sealed class Generate{Resource}UseCase(
    I{Aggregate}Repository repository,
    IStorageService storageService,
    IRenderEngineFactory engineFactory,
    IDataBindingEngine dataBindingEngine,
    IDocumentAuditService auditService,
    IDocumentVersioningService versioningService,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<Generate{Resource}Command, Generate{Resource}ResultDto>
{
    public async Task<Generate{Resource}ResultDto> ExecuteAsync(
        Generate{Resource}Command command, CancellationToken ct = default)
    {
        // Step 1: Parse & Guard (Tier 2 Application Guard: 1-liner typed tenant resolution)
        var projectId = projectContext.EnsureResolved("Document generation requires an active project context.");

        var outputFormat = ParseOutputFormat(command.OutputFormat);

        // Step 2: Ensure <Why: precondition aggregate check & version resolution>
        var aggregate = await repository.GetBySlugWithDetailsAsync(command.Slug, ct);
        if (aggregate == null || !aggregate.IsActive)
            throw new NotFoundException($"Template '{command.Slug}' not found or inactive.");

        var currentVersion = aggregate.GetPublishedVersion();
        var generationId = Guid.CreateVersion7();
        var tenantId = projectContext.ProjectId.Value;
        string ext = outputFormat.Extension;
        string outputKey = $"outputs/{tenantId:N}/{timeProvider.GetUtcNow():yyyy/MM/dd}/{command.Slug}_{generationId:N}.{ext}";

        // Step 3: Mutate & Render/Upload <Why: streaming synthesis and bounded spooling>
        var render = await RenderAndUploadAsync(
            aggregate, currentVersion, command.Payload, command.SkipValidation, outputFormat, outputKey, ct);

        // Step 4: Finalize & Return <Why: atomic commit, audit, and compensating storage deletion>
        return await FinalizeDocumentAsync(
            command, aggregate, currentVersion, generationId, outputKey, outputFormat, render, ct);
    }

    private async Task<RenderOutput> RenderAndUploadAsync(
        {Aggregate} aggregate,
        {Aggregate}Version currentVersion,
        JsonElement payload,
        bool skipValidation,
        OutputFormat outputFormat,
        string outputKey,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var engine = engineFactory.GetEngine(currentVersion.GetRenderEngineType());
        string? preparedDataJson = null;

        try
        {
            var boundData = await dataBindingEngine.BindAndValidateAsync(
                aggregate, currentVersion, payload, skipValidation, ct);
            preparedDataJson = boundData.DataJson;

            if (boundData.ValidationResult is { IsValid: false })
            {
                sw.Stop();
                var errorMsg = $"{boundData.ValidationResult.Errors.Count} schema violation(s) detected.";
                await auditService.LogValidationFailureAsync(
                    aggregate.Id.Value, currentVersion.Id, boundData.DataJson, outputFormat, (int)sw.ElapsedMilliseconds, errorMsg, ct);
                await unitOfWork.CommitAsync(ct);

                throw new SchemaValidationException(
                    aggregate.Slug.Value, currentVersion.Version, boundData.ValidationResult.Errors);
            }

            using var templateStream = await storageService.DownloadAsync(StorageBuckets.Templates, currentVersion.StorageKey, ct);
            var rawOutputStream = await engine.RenderStreamAsync(templateStream, boundData.DataJson, outputFormat, ct);

            await using var spooled = await HybridSpoolStream.CreateAndDisposeSourceAsync(rawOutputStream, ct);
            sw.Stop();

            await storageService.UploadAsync(
                StorageBuckets.Outputs, outputKey, spooled.ContentStream, outputFormat.MimeType, ct);

            return new RenderOutput(boundData.DataJson, spooled.FileSizeBytes, spooled.Sha256Hex, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not SchemaValidationException and not OperationCanceledException)
        {
            sw.Stop();
            try
            {
                var fallbackPayload = preparedDataJson ?? (payload.ValueKind != JsonValueKind.Undefined ? payload.GetRawText() : "{}");
                await auditService.LogFailureAsync(
                    aggregate.Id.Value, currentVersion.Id, fallbackPayload, outputFormat, (int)sw.ElapsedMilliseconds, ex.Message, CancellationToken.None);
                await unitOfWork.CommitAsync(CancellationToken.None);
            }
            catch (Exception auditEx)
            {
                Trace.TraceWarning("Failed to persist FAILED audit log: {0}", auditEx.Message);
            }

            throw;
        }
    }

    private async Task<Generate{Resource}ResultDto> FinalizeDocumentAsync(
        Generate{Resource}Command command,
        {Aggregate} aggregate,
        {Aggregate}Version currentVersion,
        Guid generationId,
        string outputKey,
        OutputFormat outputFormat,
        RenderOutput render,
        CancellationToken ct)
    {
        try
        {
            var expiry = TimeSpan.FromHours(24);
            string downloadUrl = await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

            await auditService.LogSuccessAsync(
                generationId, aggregate.Id.Value, currentVersion.Id, render.PreparedDataJson, outputKey, outputFormat, render.FileSizeBytes, render.ElapsedMilliseconds, ct);

            if (!string.IsNullOrWhiteSpace(command.DocumentRef))
            {
                await versioningService.RecordVersionAsync(
                    command.DocumentRef, aggregate.Id.Value, currentVersion.Id, generationId, command.ChangeNote, ct);
            }

            await unitOfWork.CommitAsync(ct);

            return new Generate{Resource}ResultDto(
                generationId, command.DocumentRef, outputFormat.Extension, render.FileSizeBytes, render.Sha256Hex, downloadUrl, timeProvider.GetUtcNow().Add(expiry));
        }
        catch (Exception primaryEx)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Outputs, outputKey, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                Trace.TraceWarning("Failed to delete orphaned output '{0}': {1}", outputKey, cleanupEx.Message);
            }

            throw;
        }
    }

    private static OutputFormat ParseOutputFormat(string format) =>
        format.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _ => OutputFormat.Pdf
        };

    private readonly record struct RenderOutput(
        string PreparedDataJson, long FileSizeBytes, string Sha256Hex, int ElapsedMilliseconds);
}
```

### 3.5 Polymorphic Render Engine Factory Archetype (`RenderEngineFactory`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-005]:
* Injects all `IRenderEngine` implementations via DI into `RenderEngineFactory`.
* Stores engines in an immutable `FrozenDictionary<RenderEngineType, IRenderEngine>` for zero-allocation $O(1)$ dispatch.

```csharp
public sealed class RenderEngineFactory(IEnumerable<IRenderEngine> engines) : IRenderEngineFactory
{
    private readonly FrozenDictionary<RenderEngineType, IRenderEngine> _engines =
        engines.ToFrozenDictionary(e => e.EngineType);

    public IRenderEngine GetEngine(RenderEngineType engineType)
    {
        if (_engines.TryGetValue(engineType, out var engine))
        {
            return engine;
        }

        throw new RenderException(
            templateSlug: "system",
            engineType: engineType.ToString(),
            reason: $"No render engine registered for engine type '{engineType}'.");
    }
}
```

### 3.6 Open/Closed Linguistic Localization Strategy (`ILocalizedDataFormatter` & `ThaiDataFormatter`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-006]:
* Formats dates, numbers, currency text words, and identification masks via injectable `ILocalizedDataFormatter` strategies conforming to the Open/Closed Principle.
* `ThaiDataFormatter` formats dates with Buddhist Era offset (+543 years), formal Thai months, and standardized Thai Baht text (`บาทถ้วน`).
* Pure stateless strategy instances registered via DI (`IEnumerable<ILocalizedDataFormatter>`); templates must never perform inline arithmetic or currency conversions.

```csharp
public interface ILocalizedDataFormatter
{
    string Locale { get; }
    string FormatDate(DateTimeOffset date);
    string FormatCurrencyWords(decimal amount);
    string FormatPhoneNumber(string? phone);
    string FormatNationalId(string? nationalId);
}

public sealed class ThaiDataFormatter : ILocalizedDataFormatter
{
    public string Locale => "th-TH";

    public string FormatDate(DateTimeOffset date)
    {
        var buddhistYear = date.Year + 543;
        var monthName = ResolveThaiMonthName(date.Month);
        return $"{date.Day} {monthName} {buddhistYear}";
    }

    public string FormatCurrencyWords(decimal amount) =>
        amount == 0 ? "ศูนย์บาทถ้วน" : ResolveBahtWords(amount);

    public string FormatPhoneNumber(string? phone)
    {
        var digits = ExtractDigits(phone);
        return digits.Length switch
        {
            10 => $"{digits[..3]}-{digits.Substring(3, 3)}-{digits[6..]}",
            9 => $"{digits[..2]}-{digits.Substring(2, 3)}-{digits[5..]}",
            _ => phone ?? string.Empty
        };
    }

    public string FormatNationalId(string? idCard)
    {
        var digits = ExtractDigits(idCard);
        return digits.Length == 13
            ? $"{digits[0]}-{digits.Substring(1, 4)}-{digits.Substring(5, 5)}-{digits.Substring(10, 2)}-{digits[12]}"
            : idCard ?? string.Empty;
    }

    private static string ResolveThaiMonthName(int month) => throw new NotImplementedException();
    private static string ResolveBahtWords(decimal amount) => throw new NotImplementedException();
    private static string ExtractDigits(string? input) => throw new NotImplementedException();
}
```

### 3.7 OpenXML DrawingML Document-Scoped Graphic Frame ID Generator (`IDrawingMlIdGenerator`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-007]:
* Intercepts all `NonVisualDrawingProperties` (`nvPr`) during OpenXML DOCX manipulation.
* Instantiates `DocumentDrawingMlIdGenerator` scoped strictly to each DOCX package render session.
* Ensures `nvPr.Id` is assigned a sequential, non-zero positive integer ($\ge 1$) within the document scope conforming to ECMA-376, eliminating application-wide static counters and wraparound corruption.

```csharp
public interface IDrawingMlIdGenerator
{
    uint NextId();
    void EnsureNonZeroGraphicFrameId(NonVisualDrawingProperties nvPr);
}

public sealed class DocumentDrawingMlIdGenerator(uint initialId = 1) : IDrawingMlIdGenerator
{
    private uint _currentId = initialId;

    public uint NextId()
    {
        if (_currentId == uint.MaxValue)
        {
            _currentId = 1; // Wraparound protection strictly within local document scope
        }
        return _currentId++;
    }

    public void EnsureNonZeroGraphicFrameId(NonVisualDrawingProperties nvPr)
    {
        if (nvPr.Id == null || nvPr.Id.Value == 0)
        {
            nvPr.Id = NextId();
        }
    }
}
```

### 3.8 ClosedXML A4 Auto-Fit Archetype (`ExcelPageSetupExtensions`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-008]:
* Configures `worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper` and `PageOrientation = XLPageOrientation.Portrait`.
* Calls `worksheet.PageSetup.FitToPages(1, 0)` so columns fit horizontally on a single page width.

```csharp
public static class ExcelPageSetupExtensions
{
    public static void ApplyA4FitToWidth(IXLWorksheet worksheet)
    {
        worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        worksheet.PageSetup.FitToPages(1, 0);
        worksheet.Columns().AdjustToContents();
    }
}
```

### 3.9 Ephemeral Storage Retention & Auto-Purge Worker Archetype (`DocumentRetentionWorker`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-009]:
* Purges expired output files from MinIO `StorageBuckets.Outputs` on a background schedule.
* Uses injected `TimeProvider` for all timestamp evaluations (`timeProvider.GetUtcNow()`).

```csharp
public sealed class DocumentRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DocumentRetentionWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentRetentionWorker> logger) : BackgroundService
{
    private readonly DocumentRetentionWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<IDocumentRetentionService>();

                var purgedCount = await retentionService.PurgeExpiredOutputsAsync(
                    _options.RetentionPeriod,
                    _options.BatchSize,
                    stoppingToken);

                if (purgedCount > 0)
                {
                    logger.LogInformation("Purged {Count} expired document outputs.", purgedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error occurred during document retention purge cycle.");
            }

            await Task.Delay(_options.CheckInterval, timeProvider, stoppingToken);
        }
    }
}
```

---

## 4. Level 4: High-Salience Fatal Traps & Unified Micro-Diffs

### [TRAP-MECH-001]: Large Object Heap (LOH) Buffering
```diff
- var ms = new MemoryStream(); await stream.CopyToAsync(ms, ct); return File(ms.ToArray(), "application/pdf");
+ response.ContentType = "application/pdf"; await stream.CopyToAsync(response.Body, 81920, ct);
```
*Rationale: Buffering Gotenberg binary outputs into multi-MB byte arrays forces allocations into LOH Gen 2, inducing GC stalls.*

### [TRAP-MECH-002]: OpenXML Zero Graphic Frame ID & Global Counter State
```diff
- nvPr.Id = 0; // REJECTED: Word file corruption prompt
- // or: DrawingMlIdFixer.EnsureNonZeroGraphicFrameId(nvPr); // REJECTED: Global static mutable counter
+ drawingMlIdGenerator.EnsureNonZeroGraphicFrameId(nvPr); // Scoped per document render package
```
*Rationale: Default OpenXML ID 0 or duplicates corrupt DOCX drawing XML parts, triggering repair prompts in Microsoft Word. Using an application-wide static counter violates stateless horizontal scaling and risks wraparound. Document-scoped `IDrawingMlIdGenerator` guarantees ECMA-376 compliance safely.*

### [TRAP-MECH-003]: Hardcoded Switch / If-Else Engine Selection
```diff
- if (type == RenderEngineType.Html) return new HtmlEngine(); else if (type == RenderEngineType.Docx) return new DocxEngine();
+ return engineFactory.GetEngine(engineType);
```
*Rationale: Hardcoded switch branches violate the Open/Closed Principle and prevent adding new rendering plugins via DI.*

### [TRAP-MECH-004]: Inline Linguistic Arithmetic vs Open/Closed Formatter Strategy
```diff
- "{{date.Year + 543}}"
+ "{{localized_date date}}" // Routed to ILocalizedDataFormatter strategy (ThaiDataFormatter for th-TH)
```
*Rationale: Inline arithmetic fails on edge cases and produces inconsistent date and currency strings across output formats. Hardcoding static transformers violates OCP. Route linguistic transformations through injectable `ILocalizedDataFormatter` strategies.*

### [TRAP-MECH-005]: Spreadsheets Clipping Content on Print / PDF
```diff
- worksheet.Columns().AdjustToContents(); // REJECTED: Clips on print/PDF
+ ExcelPageSetupExtensions.ApplyA4FitToWidth(worksheet);
```
*Rationale: Without `FitToPages(1, 0)`, wide tables split columns across multiple pages, breaking executive readability.*

### [TRAP-MECH-006]: HTTP 307 Redirect Leaking Internal MinIO Network URL
```diff
- return RedirectPreserveMethod("http://minio:9000/documents/doc-123.pdf");
+ var stream = await storageService.OpenReadStreamAsync(objectKey, ct);
+ return File(stream, "application/pdf", "doc-123.pdf");
```
*Rationale: Returning HTTP 307 redirects to internal Docker hostnames fails for external clients, violates Zero-Trust boundaries, and exposes private network infrastructure.*

### [TRAP-MECH-007]: Relational Table Querying During Versioned Runtime Execution
```diff
- var mappings = template.FieldMappings.ToList();
+ List<FieldMapping> mappings = !string.IsNullOrWhiteSpace(version.MappingsSnapshot)
+     ? FieldMappingSnapshotSerializer.Deserialize(template.Id, version.MappingsSnapshot, now)
+     : template.FieldMappings.ToList();
```
*Rationale: Querying mutable relational `field_mappings` during document generation breaks version immutability. An author editing field mappings in the studio causes historical published versions to immediately drift and produce altered outputs. The frozen `TemplateVersion.MappingsSnapshot` is the authoritative runtime manifest.*
*Authoring Invariant: To guarantee snapshot fidelity, every version creation operation must populate non-null frozen snapshots:*
*1. `CreateTemplateUseCase`: Generates Draft-07 DataSchema, default SamplePayload, and initializes `MappingsSnapshot = "[]"`.*
*2. `SaveTemplateHtmlUseCase`: Preserves prior version's `MappingsSnapshot` (defaulting to `"[]"` if none existed) alongside inferred Draft-07 schema.*
*3. `CommitTemplateDraftUseCase`: Discovers fallback placeholders from `draft.Placeholders` if `command.Mappings` is empty, freezes `MappingsSnapshot`, and generates schema.*

### [TRAP-MECH-008]: Pass-Through Indirection Wrapper or Intermediate Pipeline in Rendering
```diff
- // Artificial intermediate render pipeline wrapper between UseCase and engines:
- public class DocumentRenderPipeline(IRenderEngineFactory f, IDataBindingEngine d, ...) : IDocumentRenderPipeline { ... }
- var renderResult = await renderPipeline.RenderAndUploadAsync(template, version, ...);
+ // Direct PEMR orchestration in GenerateDocumentUseCase connecting directly to domain engines:
+ var bound = await dataBindingEngine.BindAndValidateAsync(template, version, payload, skip, ct);
+ var rawStream = await engine.RenderStreamAsync(templateStream, bound.DataJson, format, ct);
```
*Rationale: Concrete pipeline violation of system-wide CodebaseSSoTAndDeduplicationFirewall. Intermediate pipeline classes (e.g. `DocumentRenderPipeline`) or pass-through wrappers that merely forward calls and convert DTOs split error handling across classes, obscure stack traces, and introduce artificial maintenance overhead without adding domain value. Orchestrate generation directly in `Generate{Resource}UseCase` following 4-step PEMR.*
