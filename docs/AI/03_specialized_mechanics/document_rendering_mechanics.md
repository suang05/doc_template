# [MECH-002]: document_rendering_mechanics.md — Rendering Engines, OpenXML & Thai Formatting SSoT

<context>
Scope: Polymorphic Rendering Engines, 2-Track Rendering Pipeline, Gotenberg Chromium Microservice, Zero-LOH Bounded Streaming, OpenXML & ClosedXML Mechanics.
SSoT_For: Render Engine Resolution, Gotenberg Response Streaming, Storage Perimeter Egress, Thai Formatting, OpenXML DrawingML Fix, and ClosedXML Print Fit.
Out_Of_Scope: Presentation layer routing and RFC 9457 exceptions (see: ../01_clean_arch_patterns/04_api_presentation_layer.md), General Security and Performance bounds (see: ../00_architecture_core/03_security_and_performance.md).
Target: `backend-v2/` (`SmkDoc.Infrastructure/Engines/`, `SmkDoc.Infrastructure/Pdf/`, `SmkDoc.Application/Common/Helpers/`).
TriggerSignals: [SIGNAL_TOPOLOGY_MUTATION], [SIGNAL_API_MUTATION]
</context>

---

## 1. Level 1: Document Rendering Invariants Matrix

### Architectural Wiring & Rendering Contracts
| Direction | Upstream Origin | Current Role (Document Synthesis Core) | Downstream Destination |
|---|---|---|---|
| **Render Request** | Application UseCase (`RenderDocumentUseCase`)| Resolves polymorphic engine via resolver | `IRenderEngine` Implementation |
| **Data Enrichment**| Raw JSON Payload | Applies Buddhist Era date & Baht text | Centralized `ThaiDataTransformer` |
| **HTML / Chromium**| HTML Template + CSS + Google Fonts | Synthesizes PDF via Gotenberg 8 | Gotenberg HTTP Microservice |
| **DOCX Synthesis** | OpenXML Document Package | Fixes DrawingML `nvPr.Id` graphic IDs | Native Word Document Stream |
| **XLSX Synthesis** | ClosedXML Workbook Package | Configures A4 portrait 1-page fit | Native Excel Document Stream |
| **Streaming Egress**| Engine Render Result | Direct Stream pass-through with bounded buffer | HTTP Wire Response (`Response.Body`) |
| **Storage Egress** | MinIO S3 Object Store | Server-side reverse-proxy streaming via Gateway | Client Download (Zero 307 Redirects) |

### Document Rendering Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Buffer Allocations & Port Purity** | ! Exposing `byte[]` / `Task<byte[]>` on `IRenderEngine`/`IPdfRenderer` or copying packages into unbounded `MemoryStream` | >> Pure `Task<Stream>` port contracts; direct HTTP response streaming (`81,920`-byte buffer), `Stream.Null` dry-run validation, and `HybridSpoolStream` (`<= 64 KB` pooled `ArrayPool<byte>`; `> 64 KB` `DeleteOnClose` temp `FileStream`) for seekable packages |
| **Engine Resolution** | ! Hardcoded `if/else` or `switch` chains for engine selection | >> Polymorphic dispatch via `IRenderEngine` & `RenderEngineResolver` |
| **Storage Perimeter Egress** | ! Emitting HTTP 307 redirects or raw MinIO URLs to clients | >> Server-side bounded streaming pass-through (`81,920` buffer) keeping MinIO private inside isolated Docker VPC |
| **2-Track Rendering Pipeline**| ! Forcing high-volume batch renders through synchronous 30 req/min endpoint causing 429 rejections | >> 2-Track Architecture: 1) Sync Pipeline for Monaco Live Preview (`30 req/min`), 2) Async Queue (`POST /api/v1/documents/generate-async`, `202 Accepted` + `Location: /api/v1/documents/jobs/{jobId}`, `DocumentRenderQueueWorker` with `SemaphoreSlim(4)` Gotenberg backpressure and Redis/Memory `IDocumentRenderQueue`) |
| **Resource Protection** | ! Allowing unthrottled rendering requests to saturate Gotenberg Chromium | >> Enforce dedicated `rendering` rate-limiting policy (`30 req/min` per tenant) returning RFC 9457 ProblemDetails (`429 Too Many Requests`) |
| **OpenXML DrawingML** | ! Leaving graphic frame property IDs as default `0` | >> Explicit positive non-zero integers ($\ge 1$) via `DrawingMlIdFixer` |
| **Excel Printing** | ! Spreadsheets clipping content on print or PDF export | >> Automatic A4 portrait fit: `worksheet.PageSetup.FitToPages(1, 0)` |
| **Thai Formatting** | ! Inline date / Baht text conversions inside Handlebars | >> Centralized `ThaiDataTransformer` helper methods |
| **Thai Font Marks** | ! Missing Thai tone marks or floating vowels in PDF | >> Automated Google Fonts Sarabun injection in `HtmlLayoutProcessor` |
| **Storage Retention & Auto-Purge** | ! Retaining generated document outputs in MinIO indefinitely, causing unbounded storage growth | >> Periodic background batch purging via `DocumentRetentionWorker` consuming `TimeProvider` to delete expired MinIO objects from `StorageBuckets.Outputs` and mark logs as purged |
| **Immutable Payload Validation** | ! Mutating, computing, or transforming JSON payloads during rendering pipeline | >> Pure payload ingestion: JSON Schema Draft-07 validation with exact RFC 9457 error details; zero payload mutation |
| **Synchronous Rendering**| ! Blocking on network/disk I/O (`.Result`, `.Wait()`) | >> 100% asynchronous execution with forwarded `CancellationToken` |

---

## 2. Level 2: Core Rendering SSoT ASTs & Cognitive Rendering Engine

### Engineering Rationale & Architectural Decisions
* **Why 2-Track Rendering Architecture:** Live preview in Monaco Studio requires sub-second synchronous turnaround to provide instant visual feedback. High-volume ERP batch generation (e.g. 5,000 invoices at month-end) would exhaust Chromium memory pods if submitted synchronously. Decoupling batch into an asynchronous job queue (`202 Accepted`) allows background workers to drain tasks at a concurrency rate matched to Gotenberg pod capacity.
* **Why Banning HTTP 307 Redirects to Internal MinIO:** MinIO operates inside an internal container network (`http://minio:9000`). Emitting a 307 redirect exposes private infrastructure endpoints to outside clients, breaks corporate firewalls, and leaks storage topology. Streaming through the API gateway enforces Zero-Trust isolation.
* **Why 81,920-Byte Buffer Threshold:** The .NET runtime allocates objects $\ge 85,000$ bytes on the Large Object Heap (LOH). The LOH is never compacted during Gen 0/1 sweeps; continuous multi-MB PDF buffering produces severe LOH fragmentation and costly Gen 2 stop-the-world GC pauses. Direct streaming bounded at 81,920 bytes completely avoids LOH allocation.
* **Why Polymorphic Engine Resolver over Switch Statements:** Allows new document engines (e.g., Markdown, Typst, PDF Forms) to be introduced by simply registering a new `IRenderEngine` implementation in DI, honoring the Open/Closed Principle.
* **Why OpenXML DrawingML Graphic Frame Non-Zero IDs:** Microsoft Word strictly rejects OpenXML packages where image or chart graphic frame IDs are `0` or duplicated, triggering file corruption warnings on user workstations.
* **Why Centralized Thai Formatting:** Thai Buddhist Era dates and Baht text currency require specialized linguistic rules (tone mark positioning, grammatical edge cases, year +543); centralized transformers guarantee 100% deterministic outputs across PDF, DOCX, and XLSX exports.

### Cognitive Rendering Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: High-Throughput Deterministic Rendering, Zero-LOH Memory Bounds & Gotenberg Pod Resilience
  TradeOffPriority: MemorySafety (Zero-LOH) > GotenbergPodResilience > TypographicDeterminism (Thai Fonts) > CustomEngineExtensibility

ProactiveRenderingGuards:
  - Scope: SmkDoc.Infrastructure.Engines
    Trigger: Detection of MemoryStream.ToArray() or byte[] buffer on document outputs
    CanonicalAction: Enforce Stream pass-through with bounded 81,920 buffer >> Ref: [RULE-MECH-001]

  - Scope: SmkDoc.Api.Controllers
    Trigger: Returning HTTP 307 Redirect to internal MinIO storage URL
    CanonicalAction: Reject redirect; stream content directly through gateway using File(stream, ...) >> Ref: [RULE-MECH-006]

  - Scope: SmkDoc.Infrastructure.Engines.Word
    Trigger: Assignment of nvPr.Id = 0 or missing non-zero ID enforcement
    CanonicalAction: Invoke DrawingMlIdFixer.EnsureNonZeroGraphicFrameId(nvPr) >> Ref: [RULE-MECH-002]

  - Scope: SmkDoc.Infrastructure.Engines.Excel
    Trigger: Omitting PageSetup.FitToPages(1, 0) on A4 worksheets
    CanonicalAction: Call ExcelPageSetupExtensions.ApplyA4FitToWidth(worksheet) >> Ref: [RULE-MECH-003]

  - Scope: SmkDoc.Application.Common.Helpers
    Trigger: Inline date math (e.g. + 543) or ad-hoc currency text formatting in templates
    CanonicalAction: Route formatting through ThaiDataTransformer SSoT methods >> Ref: [RULE-MECH-004]

CoreExtensionSlots:
  RenderEngineSlot: IRenderEngine (Html, Docx, Excel polymorphic strategies)
  EngineResolverSlot: IRenderEngineResolver (Type-safe engine selector)
  ThaiTransformerSlot: ThaiDataTransformer (Centralized linguistic & currency transforms)
  DrawingFixerSlot: DrawingMlIdFixer (Thread-safe graphic frame ID generator)
```

### Polymorphic Rendering Engine Strategy Contract
```csharp
namespace SmkDoc.Application.Common.Interfaces;

using SmkDoc.Domain.Enums;

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

### Handlebars Authoritative Helpers Catalog
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

## 3. Level 3: Canonical Rendering Archetypes

### Gotenberg Direct Response Streaming Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-001]:
* [MUST] Use `HttpCompletionOption.ResponseHeadersRead` when invoking external Gotenberg rendering endpoints.
* [MUST] Wrap `response.Content.ReadAsStreamAsync(ct)` in a `ResponseDisposingStream` so `HttpResponseMessage` is deterministically disposed on stream close.
* [MUST] Spool unseekable engine streams via `ArrayPool<byte>` (81,920 bytes) + `IncrementalHash` (`SHA256`) into a `FileOptions.DeleteOnClose | FileOptions.Asynchronous` temporary `FileStream` before MinIO upload (Zero Large Object Heap allocation).
```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using SmkDoc.Application.Common.Exceptions;

namespace SmkDoc.Infrastructure.Pdf;

public sealed class GotenbergHtmlRenderer(HttpClient httpClient)
{
    public async Task<Stream> RenderHtmlToPdfStreamAsync(
        string html,
        string? headerHtml = null,
        string? footerHtml = null,
        CancellationToken ct = default)
    {
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
    public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

    public override void Flush() => innerStream.Flush();
    public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => innerStream.ReadAsync(buffer, offset, count, ct);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => innerStream.ReadAsync(buffer, ct);
    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
            response.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### 2-Track Rendering Pipeline Presentation Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-006]:
* [MUST] Provide Track 1 (Live Preview & Sync Generation) via `DocumentGenerationController`: synchronous streaming return via `File(stream, "application/pdf")`.
* [MUST] Provide Track 2 (Async Batch Queue): enqueue background render job and return `AcceptedAtAction` (`202 Accepted`) with `JobId` and polling status URL.
* [MUST] Provide Artifact Egress via `DocumentArtifactController`: pre-signed download URLs (`/download/{logId}/url`) or direct bounded stream pass-through (`/download/{logId}/file`).
* [MUST NOT] Return HTTP 307 redirects to internal MinIO S3 instances.
* [MUST NOT] Bundle generation, download, and version history into a single monolithic controller.
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Controllers.Rendering;

[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = SmkDocPolicies.CanGenerateDocuments)]
public sealed class DocumentGenerationController(
    IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto> generateUseCase,
    IUseCase<PreviewDocumentQuery, Stream> previewUseCase,
    IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto> enqueueJobUseCase,
    IUseCase<GetDocumentJobQuery, DocumentJobDto> getJobUseCase) : ControllerBase
{
    // Track 1: Synchronous Live Preview & Instant Generation (Dedicated 30 req/min rate limit)
    [HttpPost("generate/{slug}")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<GenerateDocumentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromRoute] string slug, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var command = new GenerateDocumentCommand(slug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation);
        var result = await generateUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<GenerateDocumentResultDto>(result));
    }

    [HttpPost("preview")]
    [EnableRateLimiting("rendering")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview([FromBody] PreviewDocumentRequest request, CancellationToken ct)
    {
        var query = new PreviewDocumentQuery(request.Payload, request.Html, null);
        var pdfStream = await previewUseCase.ExecuteAsync(query, ct);
        Response.Headers.ContentDisposition = "inline";
        return File(pdfStream, "application/pdf");
    }

    // Track 2: Asynchronous High-Volume Batch Queue (202 Accepted + Location Header)
    [HttpPost("generate-async/{slug}")]
    [HttpPost("generate-async")]
    [Idempotent]
    [EnableRateLimiting("rendering")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> GenerateAsync([FromBody] GenerateDocumentRequest request, [FromRoute] string? slug, [FromQuery] string? templateSlug, CancellationToken ct)
    {
        var finalSlug = !string.IsNullOrWhiteSpace(slug) ? slug : templateSlug;
        if (string.IsNullOrWhiteSpace(finalSlug)) throw new DomainValidationException("Template slug is required.");
        var command = new EnqueueDocumentGenerationCommand(finalSlug, request.Payload, request.OutputFormat, request.DocumentRef, request.ChangeNote, request.SkipValidation);
        var result = await enqueueJobUseCase.ExecuteAsync(command, ct);
        Response.Headers.Location = $"/api/v1/documents/jobs/{result.JobId}";
        return StatusCode(StatusCodes.Status202Accepted, new ApiResponse<DocumentJobDto>(result));
    }

    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus([FromRoute] Guid jobId, CancellationToken ct)
    {
        var query = new GetDocumentJobQuery(jobId);
        var result = await getJobUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<DocumentJobDto>(result));
    }
}
```

### Ephemeral Storage Retention & Auto-Purge Worker Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-007]:
* [MUST] Purge expired output files from MinIO `StorageBuckets.Outputs` on a background schedule (default 24h TTL, 30m worker interval).
* [MUST] Injected `TimeProvider` must be used for all timestamp evaluations (`timeProvider.GetUtcNow()`).
* [MUST] Repository query must bypass tenant filter via `.IgnoreQueryFilters()` to allow system-wide cross-tenant purging.
* [MUST] Mark log entity state via expressive domain verb `log.MarkOutputPurged(now)`.
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Modules.Rendering.Documents.Services;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

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
```

### OpenXML DrawingML Non-Zero ID Fix Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-002]:
* [MUST] Intercept all `NonVisualDrawingProperties` (`nvPr`) during OpenXML DOCX manipulation.
* [MUST] Ensure `nvPr.Id` is assigned an explicit, thread-safe non-zero positive integer (`>= 1`).
* [MUST NOT] Allow duplicate or zero (`0`) graphic frame IDs which cause Microsoft Word corruption alerts.
```csharp
using DocumentFormat.OpenXml.Drawing;

namespace SmkDoc.Infrastructure.Engines.Word;

public static class DrawingMlIdFixer
{
    private static uint _nextId = 1;

    public static uint GenerateNonZeroId() => Interlocked.Increment(ref _nextId);

    public static void EnsureNonZeroGraphicFrameId(NonVisualDrawingProperties nvPr)
    {
        if (nvPr.Id == null || nvPr.Id.Value == 0)
        {
            nvPr.Id = GenerateNonZeroId();
        }
    }
}
```

### ClosedXML A4 Auto-Fit Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-003]:
* [MUST] Configure `worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper` and `PageOrientation = XLPageOrientation.Portrait` (or Landscape per template).
* [MUST] Call `worksheet.PageSetup.FitToPages(1, 0)` so columns fit horizontally on a single page width without horizontal overflow.
```csharp
using ClosedXML.Excel;

namespace SmkDoc.Infrastructure.Engines.Excel;

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

### Centralized ThaiDataTransformer Implementation
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-004]:
* [MUST] Format dates with Buddhist Era offset (+543 years) and Thai month names, normalizing Buddhist Era input years (`year > 2400 => year - 543`) BEFORE Gregorian calendar validation.
* [MUST] Convert numerical currency amounts to standardized Thai Baht text (`บาทถ้วน` or satang suffix).
* [MUST] Format phone numbers to `0xx-xxx-xxxx` and national ID cards to `x-xxxx-xxxxx-xx-x`.
```csharp
using System.Text;

namespace SmkDoc.Application.Common.Helpers;

public static class ThaiDataTransformer
{
    private static readonly string[] ThaiMonths =
    [
        "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    ];

    private static readonly string[] ThaiDigits = ["ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];
    private static readonly string[] ThaiUnits = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน", "ล้าน"];

    public static string FormatThaiDate(DateTimeOffset date)
    {
        var day = date.Day;
        var month = ThaiMonths[date.Month - 1];
        var buddhistYear = date.Year + 543;
        return $"{day} {month} {buddhistYear}";
    }

    public static string ToThaiBahtText(decimal amount)
    {
        if (amount == 0) return "ศูนย์บาทถ้วน";

        var integerPart = (long)Math.Truncate(amount);
        var decimalPart = (int)Math.Round((amount - integerPart) * 100);

        var result = new StringBuilder();
        if (integerPart > 0)
        {
            result.Append(ConvertNumberToThaiText(integerPart)).Append("บาท");
        }

        if (decimalPart > 0)
        {
            result.Append(ConvertNumberToThaiText(decimalPart)).Append("สตางค์");
        }
        else
        {
            result.Append("ถ้วน");
        }

        return result.ToString();
    }

    public static string FormatPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            10 => $"{digits[..3]}-{digits.Substring(3, 3)}-{digits[6..]}",
            9 => $"{digits[..2]}-{digits.Substring(2, 3)}-{digits[5..]}",
            _ => phone
        };
    }

    public static string FormatThaiIdCard(string? idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard)) return string.Empty;
        var digits = new string(idCard.Where(char.IsDigit).ToArray());
        if (digits.Length != 13) return idCard;
        return $"{digits[0]}-{digits.Substring(1, 4)}-{digits.Substring(5, 5)}-{digits.Substring(10, 2)}-{digits[12]}";
    }

    private static string ConvertNumberToThaiText(long number)
    {
        if (number == 0) return string.Empty;
        var text = new StringBuilder();
        var digits = number.ToString();
        var len = digits.Length;

        for (var i = 0; i < len; i++)
        {
            var digit = digits[i] - '0';
            var unitIndex = (len - i - 1) % 6;

            if (digit != 0)
            {
                if (unitIndex == 0 && digit == 1 && len > 1 && digits[i - 1] != '0')
                    text.Append("เอ็ด");
                else if (unitIndex == 1 && digit == 2)
                    text.Append("ยี่");
                else if (unitIndex == 1 && digit == 1) { } // Ten in Thai does not precede "หนึ่ง"
                else
                    text.Append(ThaiDigits[digit]);

                text.Append(ThaiUnits[unitIndex]);
            }

            if (unitIndex == 0 && i < len - 1)
            {
                text.Append("ล้าน");
            }
        }

        return text.ToString();
    }
}
```

### Polymorphic Rendering Engine Resolver Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-005]:
* [MUST] Inject `IEnumerable<IRenderEngine>` into `RenderEngineResolver`.
* [MUST] Resolve target engine using `EngineType` match, throwing strongly-typed exception on unsupported engines.
```csharp
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Engines;

public sealed class RenderEngineResolver(IEnumerable<IRenderEngine> engines) : IRenderEngineResolver
{
    private readonly Dictionary<RenderEngineType, IRenderEngine> _engines =
        engines.ToDictionary(e => e.EngineType, e => e);

    public IRenderEngine Resolve(RenderEngineType engineType)
    {
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
