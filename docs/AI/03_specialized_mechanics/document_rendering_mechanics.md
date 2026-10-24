# [MECH-002]: document_rendering_mechanics.md — Rendering Engines, OpenXML & Thai Formatting SSoT

<context>
Scope: Polymorphic Rendering Engines, Gotenberg Chromium Microservice, OpenXML DrawingML Fix, ClosedXML A4 Fit & ThaiDataTransformer SSoT
Target: `SmkDoc.Infrastructure/Engines/` (`Gotenberg/`, `Word/`, `Excel/`, `Html/`) & `SmkDoc.Application/Common/Helpers/`
Authority: Absolute SSoT Invariant (100% deterministic output, zero LibreOffice shift, zero multi-MB buffering)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_TOPOLOGY_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: Document Rendering Invariants Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Buffer Allocations** | In-memory `byte[]` buffering of Gotenberg outputs | Direct streaming to `Response.Body` via `Stream.CopyToAsync(response.Body, 81920, ct)` |
| **Engine Resolution** | Hardcoded `if/else` or `switch` chains for engine selection | Polymorphic dispatch via `IRenderingEngine` & `RenderingEngineResolver` |
| **OpenXML DrawingML** | Leaving graphic frame property IDs as default `0` | Explicit positive non-zero integers ($\ge 1$) via `DrawingMlIdFixer` |
| **Excel Printing** | Spreadsheets clipping content on print or PDF export | Automatic A4 portrait fit: `worksheet.PageSetup.FitToPages(1, 0)` |
| **Thai Formatting** | Inline date / Baht text conversions inside Handlebars | Centralized `ThaiDataTransformer` helper methods |
| **Thai Font Marks** | Missing Thai tone marks or floating vowels in PDF | Automated Google Fonts Sarabun injection in `HtmlLayoutProcessor` |

---

## 2. Level 2: Core Rendering SSoT ASTs & Matrices

### Polymorphic Rendering Engine Strategy Contract
```csharp
namespace SmkDoc.Application.Common.Interfaces;

public interface IRenderEngine
{
    RenderEngineType EngineType { get; }
    Task<byte[]> RenderAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default);
    Task<Stream> RenderStreamAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default);
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

### Centralized ThaiDataTransformer AST (`SmkDoc.Application.Common.Helpers`)
```csharp
namespace SmkDoc.Application.Common.Helpers;

public static class ThaiDataTransformer
{
    public static string FormatThaiDate(DateTimeOffset date);
    public static string ToThaiBahtText(decimal amount);
    public static string FormatPhone(string? phone);
    public static string FormatThaiIdCard(string? idCard);
}
```

---

## 3. Level 3: Canonical Rendering Archetypes

### Gotenberg Direct Response Streaming Archetype
>> MANDATORY CANONICAL TEMPLATE [RULE-MECH-001]:
* [MUST] Use `HttpCompletionOption.ResponseHeadersRead` when invoking external Gotenberg rendering endpoints.
* [MUST] Return raw `Stream` directly from `response.Content.ReadAsStreamAsync(ct)`.
* [MUST NOT] Buffer rendering payloads into in-memory `byte[]` arrays or unbounded `MemoryStream` instances (Zero Large Object Heap allocation).
```csharp
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

    using var request = new HttpRequestMessage(HttpMethod.Post, "/forms/chromium/convert/html") { Content = content };
    var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();

    return await response.Content.ReadAsStreamAsync(ct);
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

---

## 4. Level 4: Anti-Patterns & Micro-Diffs

### [TRAP-MECH-001]: Large Object Heap (LOH) Buffering
```csharp
// ! BANNED: Buffering Gotenberg binary output into multi-MB byte array
var ms = new MemoryStream();
await stream.CopyToAsync(ms, ct);
return File(ms.ToArray(), "application/pdf", "doc.pdf"); // FATAL: LOH Gen 2 Stalls!

// >> CANONICAL: Direct stream pipe to Response.Body
Response.ContentType = "application/pdf";
await stream.CopyToAsync(Response.Body, 81920, ct);
```

### [TRAP-MECH-002]: OpenXML Zero Graphic Frame ID Corruption
```csharp
// ! BANNED: Default OpenXML id=0 causes Microsoft Word file corruption prompts
nvPr.Id = 0; // REJECTED

// >> CANONICAL: Enforced non-zero positive integer
DrawingMlIdFixer.EnsureNonZeroGraphicFrameId(nvPr);
```

### [TRAP-MECH-003]: Inline Thai Transformation in Templates
```csharp
// ! BANNED: Inline template transformation logic
"{{date.Year + 543}}"

// >> CANONICAL: Routing via centralized ThaiDataTransformer SSoT
"{{thai_date date}}"
```
