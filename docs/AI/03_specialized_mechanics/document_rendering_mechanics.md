# [MECH-002]: document_rendering_mechanics.md — Rendering Engines, OpenXML & Thai Formatting SSoT

<context>
Scope: Polymorphic Rendering Engines, Gotenberg Chromium Microservice, OpenXML DrawingML Fix, ClosedXML A4 Fit & ThaiDataTransformer SSoT
Target: `SmkDoc.Infrastructure/Engines/` (`Gotenberg/`, `Word/`, `Excel/`, `Html/`) & `SmkDoc.Application/Common/Helpers/`
Authority: Absolute SSoT Invariant (100% deterministic output, zero LibreOffice shift, zero multi-MB buffering)
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_TOPOLOGY_MUTATION]
UpstreamKernel: AGENTS.md
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

### Document Rendering Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Buffer Allocations** | In-memory `byte[]` buffering of Gotenberg outputs | Direct streaming to `Response.Body` via `Stream.CopyToAsync(response.Body, 81920, ct)` |
| **Engine Resolution** | Hardcoded `if/else` or `switch` chains for engine selection | Polymorphic dispatch via `IRenderEngine` & `RenderEngineResolver` |
| **OpenXML DrawingML** | Leaving graphic frame property IDs as default `0` | Explicit positive non-zero integers ($\ge 1$) via `DrawingMlIdFixer` |
| **Excel Printing** | Spreadsheets clipping content on print or PDF export | Automatic A4 portrait fit: `worksheet.PageSetup.FitToPages(1, 0)` |
| **Thai Formatting** | Inline date / Baht text conversions inside Handlebars | Centralized `ThaiDataTransformer` helper methods |
| **Thai Font Marks** | Missing Thai tone marks or floating vowels in PDF | Automated Google Fonts Sarabun injection in `HtmlLayoutProcessor` |
| **Synchronous Rendering**| Blocking on network/disk I/O (`.Result`, `.Wait()`) | 100% asynchronous execution with forwarded `CancellationToken` |

---

## 2. Level 2: Core Rendering SSoT ASTs & Cognitive Rendering Engine

### Engineering Rationale & Architectural Decisions
* **Why Polymorphic Engine Resolver over Switch Statements:** Allows new document engines (e.g., Markdown, Typst, PDF Forms) to be introduced by simply registering a new `IRenderEngine` implementation in DI, honoring the Open/Closed Principle.
* **Why Direct Streaming with `ResponseHeadersRead`:** Multi-MB PDF documents generate significant GC pressure if loaded into `byte[]` arrays, pushing allocations into Large Object Heap (LOH) Gen 2 and inducing GC pauses. Direct streaming operates with bounded 81,920-byte buffers.
* **Why OpenXML DrawingML Graphic Frame Non-Zero IDs:** Microsoft Word strictly rejects OpenXML packages where image or chart graphic frame IDs are `0` or duplicated, triggering file corruption warnings on user workstations.
* **Why Centralized Thai Formatting:** Thai Buddhist Era dates and Baht text currency require specialized linguistic rules (tone mark positioning, grammatical edge cases, year +543); centralized transformers guarantee 100% deterministic outputs across PDF, DOCX, and XLSX exports.

### Cognitive Rendering Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: High-Throughput Deterministic Rendering & Zero-LOH Memory Streaming
  TradeOffPriority: MemorySafety (Zero-LOH) > TypographicDeterminism (Thai Fonts) > Extensibility > MicrosecondOverhead

ProactiveRenderingGuards:
  - Scope: SmkDoc.Infrastructure.Engines
    Trigger: Detection of MemoryStream.ToArray() or byte[] buffer on document outputs
    CanonicalAction: Enforce Stream pass-through with bounded 81,920 buffer >> Ref: [RULE-MECH-001]

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
* [MUST] Return raw `Stream` directly from `response.Content.ReadAsStreamAsync(ct)`.
* [MUST NOT] Buffer rendering payloads into in-memory `byte[]` arrays or unbounded `MemoryStream` instances (Zero Large Object Heap allocation).
```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using SmkDoc.Application.Common.Exceptions;

namespace SmkDoc.Infrastructure.Gotenberg;

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

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorDetail = await response.Content.ReadAsStringAsync(ct);
            throw new DocumentRenderException($"Gotenberg rendering failed with status {response.StatusCode}: {errorDetail}");
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }
}
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
* [MUST] Format dates with Buddhist Era offset (+543 years) and Thai month names.
* [MUST] Convert numerical currency amounts to standardized Thai Baht text (`บาทถ้วน` or satang suffix).
* [MUST] Format phone numbers to `0xx-xxx-xxxx` and national ID cards to `x-xxxx-xxxxx-xx-x`.
```csharp
using System.Globalization;
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
