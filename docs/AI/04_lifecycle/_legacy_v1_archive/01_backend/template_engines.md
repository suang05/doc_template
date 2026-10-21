# template_engines.md — Template Engines & Document Rendering Pipelines

<cognitive_priming>
> [PURPOSE_SSOT]: Authoritative Single Source of Truth (SSoT) for document rendering engine pipelines, Handlebars helpers, OpenXML/ClosedXML composable steps, Gotenberg Chromium/LibreOffice gateways, Thai typography injection, and template security scanning for `smk-doc-server`.
> [OPERATIONAL_MODES]:
> - `[MODE: ENGINE_EXTENSION]`: Implement `IRenderEngine` using polymorphic strategy dispatch.
> - `[MODE: PIPELINE_EXECUTION]`: Execute Zero-LOH streaming and stream Gotenberg payloads directly to HTTP response bodies.
> [BEACONS: Gotenberg 8, Chromium, LibreOffice, Handlebars.Net, OpenXML, ClosedXML, DrawingML Id, Sarabun, MinIO]
</cognitive_priming>

<prior_inversion_map>
| Rendering Context | Defective Prior (Banned) | Canonical Standard (Mandatory Substitution) | Invariant Logic |
|---|---|---|---|
| **Engine Selection** | Hardcoded `switch` or `if-else` | Polymorphic Strategy via `IEnumerable<IRenderEngine>` | Open-Closed Principle (OCP) |
| **Document Delivery** | Gen 2 LOH buffering (`byte[]`) | Stream directly to HTTP response (`RenderStreamAsync`) | Zero-LOH memory scalability |
| **Placeholder Parsing** | Ad-hoc inline regex matching | Centralized `PlaceholderHelper.Parse()` SSoT | Deterministic placeholder extraction |
| **Word Image Id** | `NonVisualDrawingProperties.Id = 0` | `NonVisualDrawingProperties.Id = (uint)counter + 1` | Microsoft Word Desktop crash prevention |
| **Thai Typography** | Hardcoded local font dependencies | Automatic Sarabun web font injection into `<head>` | Deterministic Thai vowel alignment |
| **Excel Page Fit** | Default unconstrained Letter print | `PaperSize = A4Paper`, `PagesWide = 1`, `PagesTall = 0` | Single-page horizontal alignment |
| **Security Scanning** | Unchecked template binary upload | Pre-storage inspection via `IDocxSecurityScanner` | VBA macro and OLE threat neutralization |
</prior_inversion_map>

<severity_hierarchy>
- `[TIER-1-FATAL]`: DrawingML Id must be non-zero positive integer (`(uint)counter + 1`), stream position rewind in `finally` during security scans, zero Gen 2 LOH multi-megabyte `byte[]` buffering.
- `[TIER-2-INTEGRITY]`: Polymorphic engine dispatch (`IEnumerable<IRenderEngine>`), Sarabun web font presence verification, template upload macro/OLE rejection.
- `[TIER-3-PERF]`: Handlebars AST compilation caching (SHA-256 hash key, 1-hour TTL), ClosedXML A4 fit-to-width setup, MinIO presigned URL 24h expiration.
</severity_hierarchy>

<template_engine_scope>

## 1. Quick-Lookup: Rendering Engines & Capabilities Matrix

| Format | Engine Class | Syntax & Expression Style | Core Pipeline Processing Steps | Dynamic Media Support | Typography & Fonts | Output Deliverables | PDF Gateway |
|---|---|---|---|---|---|---|---|
| **HTML** | `HtmlTemplateEngine` | Handlebars.Net (`{{var}}`, `{{#each}}`, `{{#ifEquals}}`) | AST Cache >> Handlebars Merge >> Header/Footer Split >> Sarabun Injection | QR Code (`{{qr}}`), Barcode (`{{barcode}}`) | Automatic Sarabun web font injection | Raw HTML, PDF | Gotenberg Chromium (`:3000/forms/chromium/convert/html`) |
| **DOCX** | `DocxTemplateEngine` | OpenXML placeholders (`{{field}}`, `{{field:transform}}`) | Media Injection >> Dynamic Table Row Expansion >> Text Replacement | Embedded PNG DrawingML (`{{qr:field}}`, `{{barcode:field}}`) | Embedded template styles & system fonts | Raw DOCX, PDF | Gotenberg LibreOffice (`:3000/forms/libreoffice/convert`) |
| **XLSX** | `ExcelTemplateEngine` | ClosedXML cell tags (`{{field}}`, `{{field:transform}}`) | Smart PageSetup (A4) >> Media Injection >> Table Expansion >> Cell Text | Embedded worksheet pictures (`{{qr:field}}`, `{{barcode:field}}`) | Excel cell styles & font formatting | Raw XLSX, PDF | Gotenberg LibreOffice (`:3000/forms/libreoffice/convert`) |

---

## 2. Polymorphic Engine Strategy Contract

Every document rendering engine implements `IRenderEngine`:

```csharp
public interface IRenderEngine
{
    RenderEngineType EngineType { get; }

    Task RenderAsync(
        Stream templateStream,
        JsonElement data,
        RenderOptions options,
        Stream outputStream,
        CancellationToken ct = default);
}

public readonly record struct RenderOptions(
    OutputFormat OutputFormat,
    bool EmbedFonts = true,
    int TimeoutSeconds = 30);
```

> [INVARIANT]: Introducing a new engine format (e.g., Markdown, Typst, or PPTX) requires creating a new `IRenderEngine` implementation and registering it in DI. The UseCase orchestration pipeline remains untouched.

---

## 3. HTML & Gotenberg Chromium Pipeline (`HtmlTemplateEngine`)

### 3.1 Pipeline Sequence

```
1. Input Payload >> JsonDataParser.ToHierarchy(json)
2. Template HTML >> HtmlPlaceholderTransformer.Transform(rawHtml)
3. AST Cache Lookup >> SHA-256 Hash Key Hit ? Cached Delegate : Compile & Cache (1h TTL)
4. Evaluation >> Evaluate Compiled Delegate with Hierarchical Data
5. Layout Processing >> HtmlLayoutProcessor extracts <template id="header"> and <template id="footer">
6. Thai Typography >> Inject Sarabun web font link into <head> if font-family missing
7. Gotenberg Gateway >> POST /forms/chromium/convert/html (index.html, header.html, footer.html)
8. Output Stream >> Streamed Gotenberg response piped directly to outputStream via CopyToAsync (zero byte[] / LOH buffering)
```

### 3.2 Authoritative Handlebars Helper Catalog

Centrally registered in `HtmlHelperRegistry`:

| Helper Name | Syntax Example | Description & Output Behavior |
|---|---|---|
| `thaitransform` | `{{thaitransform amount "baht"}}` | Generic Thai data transformation router calling `ThaiDataTransformer.Transform`. |
| `thai_baht_text` | `{{thai_baht_text total}}` | Formats numeric/currency string into formal Thai Baht text (e.g., `สองล้านห้าแสนบาทถ้วน`). |
| `thai_date` | `{{thai_date issueDate}}` | Formats ISO-8601 date into full Thai Buddhist Era date (e.g., `10 ตุลาคม 2569`). |
| `format_number` | `{{format_number price}}` | Formats numeric value with commas and 2 decimal places (e.g., `2,500,000.00`). |
| `addOne` / `inc` | `{{addOne @index}}` | 1-based loop index counter for table numbering (converts `0` to `1`). |
| `ifEquals` | `{{#ifEquals status "active"}}...{{/ifEquals}}` | Block helper for case-insensitive string equality comparison with `{{else}}` support. |
| `qr` / `qrcode` | `{{qr invoiceNo}}` | Generates a 150×150px embedded QR Code as a self-contained Base64 Data URI `<img>` tag. |
| `barcode` | `{{barcode sku}}` | Generates a 300×100px embedded 1D Barcode as a self-contained Base64 Data URI `<img>` tag. |

### 3.3 Supported `ThaiDataTransformer` Formats via `thaitransform`

- `"baht"` / `"thai_baht_text"`: Thai Baht text (`หนึ่งร้อยบาทถ้วน`).
- `"currency"` / `"number"`: Standard currency format with 2 decimal places (`1,250.00`).
- `"currency0"`: Currency format without decimals (`1,250`).
- `"date"` / `"thai_date"`: Full Thai Buddhist Era date (`10 ตุลาคม 2569`).
- `"thaidate_short"`: Abbreviated Thai date (`10 ต.ค. 2569`).
- `"thaidatetime"`: Thai date with timestamp (`10 ตุลาคม 2569 14:30 น.`).
- `"phone"`: Formats 10-digit Thai phone numbers (`081-234-5678`).
- `"idcard"`: Formats 13-digit Thai National ID card (`1-2345-67890-12-3`).
- `"upper"`: Converts text to uppercase.
- `"lower"`: Converts text to lowercase.

### 3.4 Layout Processor & Header/Footer Separation

`HtmlLayoutProcessor` scans the rendered HTML and separates Gotenberg Chromium header/footer fragments:

```html
<!-- Header template (Optional) -->
<template id="header">
  <div style="font-size: 8pt; width: 100%; display: flex; justify-content: space-between;">
    <span>{{companyName}}</span>
    <span>Report: {{reportTitle}}</span>
  </div>
</template>

<!-- Footer template with Chromium page numbering (Optional) -->
<template id="footer">
  <div style="font-size: 8pt; width: 100%; text-align: right;">
    <span>Page <span class="pageNumber"></span> of <span class="totalPages"></span></span>
  </div>
</template>

<!-- Main Document Body -->
<body>
  <h1>Invoice #{{invoiceNumber}}</h1>
</body>
```

### 3.5 Thai Typography & Web Font Injection

If an existing `font-family` declaration or local font reference is not detected in `<head>`, `HtmlLayoutProcessor` automatically injects local `@font-face` definitions from internal container asset storage. External URL injection (such as Google Fonts) is STRICTLY PROHIBITED to preserve Gotenberg SSRF network isolation (`system_topology.md` §5.2):

```html
<style>
@font-face {
  font-family: 'Sarabun';
  font-style: normal;
  font-weight: 400;
  src: local('Sarabun'), url('/fonts/Sarabun-Regular.ttf') format('truetype');
}
@font-face {
  font-family: 'Sarabun';
  font-style: normal;
  font-weight: 700;
  src: local('Sarabun Bold'), url('/fonts/Sarabun-Bold.ttf') format('truetype');
}
body { font-family: 'Sarabun', sans-serif; }
</style>
```

### 3.6 Compiled AST Cache (`ICompiledTemplateCache`)

`MemoryCompiledTemplateCache` caches compiled execution delegates keyed by the deterministic SHA-256 hash of the normalized template HTML (`Convert.ToHexString(SHA256.HashData(...))`), achieving sub-millisecond AST rehydration and reducing rendering latency by 40–50%.

---

## 4. DOCX & OpenXML Step-Pipeline (`DocxTemplateEngine`)

### 4.1 Composable Step Sequence

```
1. JsonDataParser.Flatten(json) >> (replacements, tables)
2. Open WordprocessingDocument in MemoryStream
3. WordMediaInjector.InjectMedia(mainPart, replacements)
   >> Generates QR/Barcode DrawingML with DrawingML Id > 0
4. WordTableExpander.ExpandTables(mainPart, tables)
   >> Clones template rows for array elements
5. WordTextReplacer.ReplaceTexts(mainPart, replacements)
   >> Substitutes {{field}} and executes Thai transforms
6. mainPart.Document.Save()
7. Output Routing:
   >> OutputFormat.Pdf: POST /forms/libreoffice/convert to Gotenberg
   >> OutputFormat.Docx: Direct DOCX Stream
```

### 4.2 SSoT Placeholder Syntax (`PlaceholderHelper`)

```text
{{customerName}}          <- Flat string replacement
{{contractDate:thai_date}}<- Text replacement with inline Thai transform
{{items.no}}              <- Dynamic table row expansion (array key: "items")
{{items.amount:currency}} <- Dynamic table row expansion with currency formatting
{{qr:invoiceNo}}          <- Embedded DrawingML QR code image injection
{{barcode:skuCode}}       <- Embedded DrawingML Barcode image injection
```

### 4.3 DrawingML Id > 0 Invariant (Word Desktop Crash Prevention)

Microsoft Word desktop strictly validates DrawingML elements. If `NonVisualDrawingProperties.Id` is `0`, Word rejects the package with unreadable content errors.

```csharp
// [FATAL]: NonVisualDrawingProperties.Id = 0 corrupts DOCX in desktop Word
// [CANONICAL]: Must generate positive non-zero integer
new Pic.NonVisualDrawingProperties
{
    Id = (uint)imageCounter + 1,
    Name = $"Picture_{imageCounter + 1}"
};
```

### 4.4 Zero-LOH Streaming Execution Flow
`DocxTemplateEngine.RenderAsync` executes OpenXML packaging via pooled `RecyclableMemoryStream` or direct stream writing into caller-provided `outputStream`. For PDF conversion, the modified package is streamed directly to Gotenberg LibreOffice endpoint and the HTTP response stream is piped into `outputStream` via `await responseStream.CopyToAsync(outputStream, ct)`. Calling `.ToArray()` or buffering as `byte[]` is strictly prohibited.

---

## 5. Excel & ClosedXML Smart-Spreadsheet Pipeline (`ExcelTemplateEngine`)

### 5.1 Worksheet Processing Steps

```
1. JsonDataParser.FlattenNamed(json) >> (replacements, arrays)
2. Open XLWorkbook in MemoryStream
3. For Each Worksheet:
   a. Apply Smart PageSetup: Letter -> A4, PagesWide = 1, PagesTall = 0
   b. ExcelMediaInjector.InjectMedia(worksheet, replacements)
   c. ExcelTableExpander.ExpandTables(worksheet, arrays)
   d. Replace flat text placeholders & Thai transforms
4. workbook.SaveAs(outputStream)
5. Output Routing:
   >> OutputFormat.Pdf: POST /forms/libreoffice/convert to Gotenberg
   >> OutputFormat.Xlsx: Direct XLSX Stream
```

### 5.2 Smart PageSetup Invariants

1. **A4 Standardization:** If worksheet paper size is `LetterPaper`, automatically change to `worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper`.
2. **Fit-to-1-Page Width:** If print page limits are unconfigured (`PagesWide == 0 && PagesTall == 0`):
   ```csharp
   worksheet.PageSetup.PagesWide = 1;
   worksheet.PageSetup.PagesTall = 0; // Unbounded vertical flow
   ```

### 5.3 Zero-LOH Streaming Execution Flow
`ExcelTemplateEngine.RenderAsync` writes directly into caller-provided `outputStream` via `workbook.SaveAs(outputStream)`. For PDF output, the spreadsheet stream is sent to Gotenberg LibreOffice conversion and piped directly into `outputStream` via `await responseStream.CopyToAsync(outputStream, ct)`. Calling `.ToArray()` or returning `byte[]` is strictly prohibited.

---

## 6. Security Scanning & Template Ingestion (`DocxSecurityScannerService`)

Uploaded DOCX files MUST pass inspection by `IDocxSecurityScanner` before MinIO storage:

```
Upload Stream >> DocxSecurityScannerService.Scan()
  >> Check 1: VBA Macros (mainPart.VbaProjectPart)? >> Reject with Security Exception
  >> Check 2: Embedded OLE Objects (EmbeddedObjectPart)? >> Reject with Security Exception
  >> Check 3: Embedded Packages (EmbeddedPackagePart)? >> Reject with Security Exception
  >> Check 4: Suspicious External Relationships (attachedtemplate, frame, oleobject)? >> Reject
  >> All Clean >> IsSafe = true >> Accept Template
```

> [INVARIANT]: Security scanning MUST restore stream position in a `finally` block (`if (docxStream.CanSeek) docxStream.Position = startPos;`) so subsequent MinIO storage uploads receive the full uncorrupted stream.

---

## 7. MinIO Storage Topology & Output Delivery

| Bucket Name | Content Type | Retention / Lifecycle Policy | Key Naming Pattern |
|---|---|---|---|
| `templates` | Template binaries (HTML, DOCX, XLSX) | Permanent (Soft deleted via `is_active`) | `templates/{projectId:N}/{slug}/{version}_{format}` |
| `outputs` | Rendered output documents (PDF, DOCX, XLSX) | 30-Day Lifecycle auto-expiry | `outputs/{projectId:N}/{year}/{month}/{generationLogId}.{ext}` |
| `fonts` | Custom corporate & web fonts (WOFF2, WOFF, TTF) | Permanent Asset Storage | `fonts/{fontFileName}` |

- **Presigned URLs:** Delivered to consumers via Presigned GET URLs with standard 24-hour expiration window (`TimeSpan.FromHours(24)`). Signed using the configured `PublicEndpoint` setting.

---

## 8. Performance SLAs & Benchmarks

| Pipeline Scenario | Input Complexity | Target SLA | Verification Test |
|---|---|---|---|
| **HTML Handlebars Merge** | 1,000 array rows | **< 1.0 second** | `PerformanceBenchmarkTests` |
| **DOCX OpenXML Pipeline** | 1,000 dynamic table rows | **< 5.0 seconds** | `PerformanceBenchmarkTests` |
| **XLSX ClosedXML Pipeline** | 1,000 spreadsheet rows | **< 5.0 seconds** | `PerformanceBenchmarkTests` |
| **Concurrent HTML Renders** | 50 concurrent requests | **100% Success Rate** | `PerformanceBenchmarkTests` |

</template_engine_scope>

<verification_gateway>
- [ ] Engine dispatch implemented exclusively via `IEnumerable<IRenderEngine>` (Polymorphic Strategy)
- [ ] Word DrawingML image properties explicitly configure non-zero positive ID: `Id = (uint)counter + 1`
- [ ] Security scanner rewinds stream position in `finally` block: `docxStream.Position = startPos`
- [ ] HTML pipeline verifies presence of Sarabun web font in `<head>`
- [ ] ClosedXML engine enforces `PagesWide = 1` and `PagesTall = 0` for PDF conversions
- [ ] MinIO output bucket keys adhere to `outputs/{projectId:N}/{year}/{month}/{generationLogId}.{ext}`
</verification_gateway>
