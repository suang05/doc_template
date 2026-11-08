# [MECH-002]: document_rendering_mechanics.md — Rendering Engines, OpenXML & Thai Formatting SSoT

<context>
Scope: Polymorphic Rendering Engines, 2-Track Rendering Pipeline, Gotenberg Chromium Microservice, Zero-LOH Bounded Streaming, OpenXML DrawingML Fix, Thai Formatting Transformers, and ClosedXML Print Fit.
SSoT_For: Render Engine Resolution, Gotenberg Response Streaming, Storage Perimeter Egress, Thai Formatting, OpenXML DrawingML Fix, and ClosedXML Print Fit.
Out_Of_Scope: Presentation layer routing and RFC 9457 exceptions (see: ../01_clean_arch_patterns/04_api_presentation_layer.md), General Security and Performance bounds (see: ../00_architecture_core/03_security_and_performance.md).
Target: `backend-v2/` (`SmkDoc.Infrastructure/Engines/`, `SmkDoc.Infrastructure/Pdf/`, `SmkDoc.Application/Common/Helpers/`, `SmkDoc.Api/Controllers/Rendering/`).
ArchitecturalSignals: [ARCH_EVOLUTION_RENDERING]
</context>

---

## 1. Level 1: Document Rendering Invariants Matrix

### 1.1 Architectural Wiring & Rendering Contracts
| Direction | Upstream Origin | Current Role (Document Synthesis Core) | Downstream Destination |
|---|---|---|---|
| **Render Request** | Application UseCase (`RenderDocumentUseCase`)| Resolves polymorphic engine via resolver | `IRenderEngine` Implementation |
| **Data Enrichment**| Raw JSON Payload | Applies Buddhist Era date & Baht text | Centralized `ThaiDataTransformer` |
| **HTML / Chromium**| HTML Template + CSS + Google Fonts | Synthesizes PDF via Gotenberg 8 | Gotenberg HTTP Microservice |
| **DOCX Synthesis** | OpenXML Document Package | Fixes DrawingML `nvPr.Id` graphic IDs | Native Word Document Stream |
| **XLSX Synthesis** | ClosedXML Workbook Package | Configures A4 portrait 1-page fit | Native Excel Document Stream |
| **Streaming Egress**| Engine Render Result | Direct Stream pass-through with bounded buffer | HTTP Wire Response (`Response.Body`) |
| **Storage Egress** | MinIO S3 Object Store | Server-side reverse-proxy streaming via Gateway | Client Download (Zero 307 Redirects) |

### 1.2 Affirmative Document Rendering Invariants
| Rendering Architectural Concern | Affirmative Rendering Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Zero-LOH Stream Purity** | Render and PDF contracts expose pure `Task<Stream>` types. Output streams are piped with bounded `81,920`-byte buffers, dry-run validations drain to `Stream.Null`, and seekable packages use `HybridSpoolStream`. Multi-MB `byte[]` arrays are prohibited. | Memory & GC Boundary |
| **Polymorphic Engine Resolution**| Document synthesis resolves engines dynamically via `IRenderEngineResolver` based on `RenderEngineType`. Hardcoded switch/if-else branching is prohibited. | Engine Extensibility |
| **Storage Perimeter Isolation** | Generated documents and templates stream server-side through API Gateway (`File(stream, ...)`). Emitting HTTP 307 redirects or raw internal MinIO URLs to clients is prohibited. | Network Perimeter |
| **2-Track Rendering Pipeline** | Rendering architecture is partitioned into Track 1 (Synchronous Monaco Live Preview with `rendering` rate limit 30 req/min) and Track 2 (Asynchronous Batch Queue via `POST generate-async`, `202 Accepted` + `Location` header, drained by `DocumentRenderQueueWorker` with `SemaphoreSlim(4)` backpressure). | Workload Partitioning |
| **Chromium Pod Protection** | Document rendering endpoints enforce the dedicated `rendering` rate-limiting policy (30 req/min per tenant) returning RFC 9457 ProblemDetails (`429 Too Many Requests`). | Service Resilience |
| **OpenXML Graphic Frame IDs** | All OpenXML drawing elements are intercepted via `DrawingMlIdFixer` to enforce explicit positive non-zero integers ($\ge 1$) for `nvPr.Id`. Prohibit zero or duplicate graphic IDs. | Document Integrity |
| **Excel Print-Width Clamping** | All ClosedXML spreadsheet worksheets apply A4 portrait fit: `worksheet.PageSetup.FitToPages(1, 0)` and `AdjustToContents()`. Horizontal content clipping is prohibited. | Spreadsheet Geometry |
| **Centralized Thai Typography** | Thai Buddhist Era calendar conversion (+543 years) and Baht text currency formatting execute exclusively through `ThaiDataTransformer`. Inline date math in templates is prohibited. | Linguistic Determinism |
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
* **Why Polymorphic Engine Resolver over Switch Statements:** Allows new document engines (e.g., Markdown, Typst, PDF Forms) to be introduced by simply registering a new `IRenderEngine` implementation in DI, honoring the Open/Closed Principle.
* **Why OpenXML DrawingML Graphic Frame Non-Zero IDs:** Microsoft Word strictly rejects OpenXML packages where image or chart graphic frame IDs are `0` or duplicated, triggering file corruption warnings on user workstations.
* **Why Centralized Thai Formatting:** Thai Buddhist Era dates and Baht text currency require specialized linguistic rules (tone mark positioning, grammatical edge cases, year +543); centralized transformers guarantee 100% deterministic outputs across PDF, DOCX, and XLSX exports.

### 2.2 Proactive Rendering Guards
```yaml
MemoryStreamProhibitionGuard:
  Invariant: "Render engines and controllers must never buffer document outputs in byte[]."
  ActionOnViolation: "Enforce live Stream pass-through with bounded 81,920 buffer."

MinioRedirectProhibitionGuard:
  Invariant: "Controllers must never return HTTP 307 redirects to internal MinIO instances."
  ActionOnViolation: "Stream binary content directly through gateway using File(stream, ...)."

OpenXmlZeroIdGuard:
  Invariant: "OpenXML NonVisualDrawingProperties.Id must not be 0 or null."
  ActionOnViolation: "Invoke DrawingMlIdFixer.EnsureNonZeroGraphicFrameId(nvPr)."

ExcelPrintFitGuard:
  Invariant: "ClosedXML worksheets must fit horizontally on a single page."
  ActionOnViolation: "Call worksheet.PageSetup.FitToPages(1, 0) and AdjustToContents()."

ThaiFormattingGuard:
  Invariant: "Templates must not perform inline date math or currency conversions."
  ActionOnViolation: "Route transformations through centralized ThaiDataTransformer."
```

### 2.3 Polymorphic Rendering Engine Strategy Contract
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

public interface IRenderEngineResolver
{
    IRenderEngine Resolve(RenderEngineType engineType);
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

## 3. Level 3: Canonical Rendering Archetypes (Surgical AST Recipes)

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
    // Invariant: Pure delegating stream forwarders (CanRead, ReadAsync, Flush, Seek, etc.) pass through directly to innerStream.

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
            response.Dispose(); // Invariant: Releases underlying socket connection deterministically
        }
        base.Dispose(disposing);
    }
}
```

### 3.2 2-Track Rendering Pipeline Presentation Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-006]:
* Track 1 (Live Preview & Sync Generation): synchronous streaming return via `File(stream, "application/pdf")`.
* Track 2 (Async Batch Queue): enqueue background render job and return `AcceptedAtAction` (`202 Accepted`) with `JobId` and polling status URL.
* Direct server-side streaming pass-through for downloads; zero HTTP 307 redirects to MinIO.

```csharp
[ApiController]
[Route("api/v1/{resources}")]
[Authorize(Policy = AuthorizationPolicies.CanGenerateDocuments)]
public sealed class {Resource}RenderingController(
    IUseCase<Generate{Resource}Command, {Resource}ResultDto> generateUseCase,
    IUseCase<Preview{Resource}Query, Stream> previewUseCase,
    IUseCase<Enqueue{Resource}JobCommand, DocumentJobDto> enqueueJobUseCase,
    IUseCase<Get{Resource}JobQuery, DocumentJobDto> getJobUseCase) : ControllerBase
{
    // Track 1: Synchronous Live Preview & Instant Generation (Dedicated "rendering" rate-limiting partition)
    [HttpPost("generate/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<{Resource}ResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] Generate{Resource}Request request, CancellationToken ct)
    {
        // Step 1: Translate Request to Command <Why: map transport payload to application command>
        var command = new Generate{Resource}Command(slug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation);

        // Step 2: Execute UseCase <Why: execute synchronous document generation>
        var result = await generateUseCase.ExecuteAsync(command, ct);

        // Step 3: Envelope Egress <Why: standardized envelope with generation metadata>
        return Ok(new ApiResponse<{Resource}ResultDto>(result));
    }

    [HttpPost("preview")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview([FromBody] Preview{Resource}Request request, CancellationToken ct)
    {
        // Step 1: Translate Request to Query <Why: build ephemeral preview query>
        var query = new Preview{Resource}Query(request.Payload, request.Html, null);

        // Step 2: Execute UseCase <Why: render ephemeral stream via Gotenberg>
        var result = await previewUseCase.ExecuteAsync(query, ct);

        // Step 3: Stream Live PDF Egress <Why: Zero-LOH stream return for browser display>
        Response.Headers.ContentDisposition = "inline";
        return File(result, "application/pdf");
    }

    // Track 2: Asynchronous High-Volume Batch Queue (202 Accepted + Location Header)
    [HttpPost("generate-async/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> GenerateAsync([FromRoute] string slug, [FromBody] Generate{Resource}Request request, CancellationToken ct)
    {
        // Step 1: Translate Request to Batch Command <Why: capture batch render request>
        var command = new Enqueue{Resource}JobCommand(slug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation);

        // Step 2: Enqueue Job <Why: place job in queue with backpressure>
        var result = await enqueueJobUseCase.ExecuteAsync(command, ct);

        // Step 3: Return REST 202 Accepted <Why: provide job status polling URI>
        return AcceptedAtAction(nameof(GetJobStatus), new { jobId = result.JobId }, new ApiResponse<DocumentJobDto>(result));
    }

    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus([FromRoute] Guid jobId, CancellationToken ct)
    {
        // Step 1: Query Job Status <Why: inspect background worker render progress>
        var query = new Get{Resource}JobQuery(jobId);
        var result = await getJobUseCase.ExecuteAsync(query, ct);

        // Step 2: Return Status Envelope <Why: client polling interface>
        return Ok(new ApiResponse<DocumentJobDto>(result));
    }
}
```

### 3.3 Ephemeral Storage Retention & Auto-Purge Worker Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-007]:
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
                // Step 1: Create Execution Scope <Why: resolve scoped persistence services in background worker>
                using var scope = scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<IDocumentRetentionService>();

                // Step 2: Execute Purge Batch <Why: delete expired MinIO objects and update audit logs>
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

            // Step 3: Delay with TimeProvider <Why: deterministic time control for unit and integration testing>
            await Task.Delay(_options.CheckInterval, timeProvider, stoppingToken);
        }
    }
}
```

### 3.4 OpenXML DrawingML Non-Zero ID Fix Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-002]:
* Intercepts all `NonVisualDrawingProperties` (`nvPr`) during OpenXML DOCX manipulation.
* Ensures `nvPr.Id` is assigned an explicit, thread-safe non-zero positive integer ($\ge 1$).

```csharp
public static class DrawingMlIdFixer
{
    private static uint _nextId = 1;

    public static uint GenerateNonZeroId() => Interlocked.Increment(ref _nextId);

    public static void EnsureNonZeroGraphicFrameId(NonVisualDrawingProperties nvPr)
    {
        // Step 1: Check ID Validity <Why: Word corrupts on 0 or null graphic IDs>
        if (nvPr.Id == null || nvPr.Id.Value == 0)
        {
            // Step 2: Assign Thread-Safe Non-Zero ID <Why: guarantees unique positive ID across threads>
            nvPr.Id = GenerateNonZeroId();
        }
    }
}
```

### 3.5 ClosedXML A4 Auto-Fit Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-003]:
* Configures `worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper` and `PageOrientation = XLPageOrientation.Portrait`.
* Calls `worksheet.PageSetup.FitToPages(1, 0)` so columns fit horizontally on a single page width.

```csharp
public static class ExcelPageSetupExtensions
{
    public static void ApplyA4FitToWidth(IXLWorksheet worksheet)
    {
        // Step 1: Configure A4 Paper Dimensions <Why: standardize target print sheet size>
        worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;

        // Step 2: Enforce 1-Page Width Clamping <Why: prevent horizontal table splitting and text clipping>
        worksheet.PageSetup.FitToPages(1, 0);

        // Step 3: Adjust Column Margins <Why: dynamic cell padding based on text length>
        worksheet.Columns().AdjustToContents();
    }
}
```

### 3.6 Centralized ThaiDataTransformer Contract & Linguistic Invariants
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-004]:
* Formats dates strictly with Buddhist Era offset (+543 years) and formal Thai month names.
* Converts numerical currency amounts to standardized Thai Baht text (`บาทถ้วน` or satang suffix).
* Enforces canonical mask formatting for phone numbers (`0xx-xxx-xxxx`) and national IDs (`x-xxxx-xxxxx-xx-x`).
* Pure stateless static transformer; templates must never perform inline arithmetic or currency conversions.

```csharp
public static class ThaiDataTransformer
{
    // Linguistic Invariants:
    // 1. Buddhist Era: Year + 543 with formal Thai month names (e.g. 21 ตุลาคม 2569).
    // 2. Baht Text: Deterministic integer and satang conversion terminating with "บาทถ้วน" or "สตางค์".
    // 3. Mask Enforcement: Pure numeric extraction before applying standard hyphenation delimiters.

    public static string FormatThaiDate(DateTimeOffset date)
    {
        var buddhistYear = date.Year + 543;
        var monthName = ResolveThaiMonthName(date.Month);
        return $"{date.Day} {monthName} {buddhistYear}";
    }

    public static string ToThaiBahtText(decimal amount)
    {
        // Contract: Converts decimal to authoritative Thai Baht text (e.g. 1200.00m => "หนึ่งพันสองร้อยบาทถ้วน")
        // Resolves integer baht words and decimal satang words, terminating with "ถ้วน" when satang is zero.
        if (amount == 0) return "ศูนย์บาทถ้วน";
        return ResolveBahtWords(amount);
    }

    public static string FormatPhone(string? phone)
    {
        // Contract: Extracts digits and standardizes to 0xx-xxx-xxxx (10 digits) or 0x-xxx-xxxx (9 digits).
        var digits = ExtractDigits(phone);
        return digits.Length switch
        {
            10 => $"{digits[..3]}-{digits.Substring(3, 3)}-{digits[6..]}",
            9 => $"{digits[..2]}-{digits.Substring(2, 3)}-{digits[5..]}",
            _ => phone ?? string.Empty
        };
    }

    public static string FormatThaiIdCard(string? idCard)
    {
        // Contract: Standardizes 13-digit Thai National ID to canonical mask x-xxxx-xxxxx-xx-x.
        var digits = ExtractDigits(idCard);
        return digits.Length == 13
            ? $"{digits[0]}-{digits.Substring(1, 4)}-{digits.Substring(5, 5)}-{digits.Substring(10, 2)}-{digits[12]}"
            : idCard ?? string.Empty;
    }

    // Concrete linguistic dictionary tables and digit-stepping loop algorithms are encapsulated
    // strictly in production implementation: SmkDoc.Application/Common/Helpers/ThaiDataTransformer.cs
    private static string ResolveThaiMonthName(int month) => throw new NotImplementedException();
    private static string ResolveBahtWords(decimal amount) => throw new NotImplementedException();
    private static string ExtractDigits(string? input) => throw new NotImplementedException();
}
```

### 3.7 Polymorphic Rendering Engine Resolver Archetype
>> MANDATORY CANONICAL BLUEPRINT [RULE-MECH-005]:
* Injects `IEnumerable<IRenderEngine>` into `RenderEngineResolver`.
* Resolves target engine using `EngineType` dictionary lookup, throwing strongly-typed exception on unsupported engines.

```csharp
public sealed class RenderEngineResolver(IEnumerable<IRenderEngine> engines) : IRenderEngineResolver
{
    private readonly Dictionary<RenderEngineType, IRenderEngine> _engines =
        engines.ToDictionary(e => e.EngineType, e => e);

    public IRenderEngine Resolve(RenderEngineType engineType)
    {
        // Step 1: Dictionary Lookup <Why: O(1) polymorphic engine dispatch without switch statements>
        if (_engines.TryGetValue(engineType, out var engine))
        {
            return engine;
        }

        throw new DocumentRenderException($"No rendering engine registered for engine type '{engineType}'.");
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

### [TRAP-MECH-002]: OpenXML Zero Graphic Frame ID Corruption
```diff
- nvPr.Id = 0; // REJECTED: Word file corruption prompt
+ DrawingMlIdFixer.EnsureNonZeroGraphicFrameId(nvPr);
```
*Rationale: Default OpenXML ID 0 or duplicates corrupt DOCX drawing XML parts, triggering repair prompts in Microsoft Word.*

### [TRAP-MECH-003]: Hardcoded Switch / If-Else Engine Selection
```diff
- if (type == "html") return new HtmlEngine(); else if (type == "docx") return new DocxEngine();
+ return engineResolver.Resolve(engineType);
```
*Rationale: Hardcoded switch branches violate the Open/Closed Principle and prevent adding new rendering plugins via DI.*

### [TRAP-MECH-004]: Inline Thai Transformation in Templates
```diff
- "{{date.Year + 543}}"
+ "{{thai_date date}}"
```
*Rationale: Inline arithmetic fails on edge cases and produces inconsistent date and currency strings across output formats.*

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
