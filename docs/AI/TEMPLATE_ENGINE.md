# TEMPLATE_ENGINE.md — Template Engines & Rendering Specifications

> **Purpose:** Authoritative rendering engine pipelines, Handlebars helpers, Gotenberg Chromium/LibreOffice, and Thai typography.  
> **Related Docs:** [ARCHITECTURE.md](ARCHITECTURE.md) (Gotenberg integration), [PATTERNS.md](PATTERNS.md) (Strategy pattern).

<ai_directive>
CRITICAL ATTENTION ROUTING: 
This document defines the strict rules for Template Engines, Document Rendering, and Output generation.
When generating or modifying code related to rendering (Handlebars, OpenXML, ClosedXML, Gotenberg), you MUST strictly apply the rules outlined here.
</ai_directive>

<template_engine_scope>

### ⚡ Quick-Lookup: Rendering Engines & Capabilities

| Format | Engine | Key Pipeline Steps | PDF Renderer |
|---|---|---|---|
| **HTML** | `HtmlTemplateEngine` | Handlebars.Net merge → Sarabun font injection → Thai word-breaking | Gotenberg Chromium (`:3000`) |
| **DOCX** | `DocxTemplateEngine` | OpenXML `WordTextReplacer` → `WordTableExpander` → `WordMediaInjector` | Gotenberg LibreOffice (`:3000`) |
| **XLSX** | `ExcelTemplateEngine` | ClosedXML `ExcelTableExpander` → formula calculation | Direct XLSX or Gotenberg LibreOffice |

---

## 1. Engine Overview (Strategy Pattern)

ระบบเลือก Engine ตาม `TemplateFormat` smart enum (ผ่าน `RenderEngineType` property):

| `TemplateFormat` | Engine Class | Output |
|---|---|---|
| `Html` | `HtmlTemplateEngine` | Handlebars merge → Gotenberg Chromium → PDF |
| `Docx` | `DocxTemplateEngine` | OpenXML pipeline → DOCX หรือ Gotenberg LibreOffice → PDF |
| `Xlsx` | `ExcelTemplateEngine` | ClosedXML → XLSX หรือ Gotenberg LibreOffice → PDF |

---

## 2. HTML Template Engine (Handlebars.Net)

### 2.1 Placeholder Syntax

HTML Templates ใช้ **Handlebars.Net** สำหรับ variable interpolation:

```html
<!-- Basic variable -->
<p>{{customerName}}</p>

<!-- Nested object -->
<p>{{customer.address.street}}</p>

<!-- Loop -->
{{#each items}}
  <tr><td>{{no}}</td><td>{{description}}</td><td>{{amount}}</td></tr>
{{/each}}
```

### 2.2 Registered Handlebars Helpers

ทุก Helper ลงทะเบียนผ่าน `HtmlHelperRegistry` (สืบทอดจาก `IHtmlHelperRegistry`):

| Helper | ตัวอย่าง | Output |
|---|---|---|
| `{{thai_baht_text value}}` | `{{thai_baht_text total}}` | `สองล้านห้าแสนบาทถ้วน` |
| `{{thai_date value}}` | `{{thai_date contractDate}}` | `15 กันยายน 2569` |
| `{{thai_date_short value}}` | `{{thai_date_short date}}` | `15 ก.ย. 2569` |
| `{{format_number value}}` | `{{format_number price}}` | `2,500,000.00` |
| `{{thaitransform value type}}` | `{{thaitransform amount "baht"}}` | Generic transform |
| `{{addOne index}}` / `{{inc index}}` | `{{addOne @index}}` | 1-based loop counter |
| `{{#ifEquals a b}}...{{/ifEquals}}` | `{{#ifEquals status "active"}}` | Conditional block |
| `{{qr value}}` / `{{qrcode value}}` | `{{qr refNumber}}` | `<img>` 150×150px QR code (data URI) |
| `{{barcode value}}` | `{{barcode sku}}` | `<img>` 300×100px barcode (data URI) |

### 2.3 HTML Layout Processor (Header/Footer)

`HtmlLayoutProcessor` แยก `<template id="header">` และ `<template id="footer">` ออกจาก Body ก่อนส่งไป Gotenberg:

```html
<!-- template หัวกระดาษ (optional) -->
<template id="header">
  <div style="font-size: 8pt;">{{companyName}} - {{reportTitle}}</div>
</template>

<!-- template ท้ายกระดาษ (optional) -->
<template id="footer">
  <div style="text-align: right;">หน้าที่ <span class="pageNumber"></span></div>
</template>

<!-- เนื้อหาหลัก -->
<body>...</body>
```

### 2.4 Font Injection (Base64)

**กฎ:** ห้ามสร้าง Custom Gotenberg Docker Image สำหรับ Font — ใช้ Base64 Injection แทนเสมอ

- Font ไฟล์ (WOFF2/TTF) จัดเก็บใน MinIO bucket: `fonts`
- `HtmlLayoutProcessor` ดึง Font binary → แปลงเป็น Base64 → inject `@font-face` ใน `<style>` tag ก่อนส่งไป Gotenberg
- ทำให้รองรับ **Thai Sarabun font** สำหรับ PDF ที่มีข้อความภาษาไทยถูกต้อง

### 2.5 Compiled Template Caching (`ICompiledTemplateCache`)

- เพื่อลดภาระของ CPU ในการ parse Handlebars AST และ compile delegate ซ้ำๆ:
  - คำนวณ SHA-256 Hash ของ Normalized HTML เป็น Cache Key
  - เก็บ Compiled Evaluation Function ใน `MemoryCompiledTemplateCache` (IMemoryCache พร้อม 1-hour Sliding Expiration)
  - รองรับ Throughput สูงโดยคืนค่า compiled delegate ทันที ลดเวลาเรนเดอร์ลง 40-50%

---

## 3. DOCX Template Engine (OpenXML Pipeline)

### 3.1 Placeholder Syntax

DOCX Templates ใช้ `{{placeholder}}` syntax ในเอกสาร Word:

```
{{customerName}}     ← Text replacement
{{items.no}}         ← Table row expansion (array)
{{items.amount}}     ← Table row expansion
{{signature_img}}    ← Image injection (Base64 data URI หรือ URL)
```

**SSoT Regex:** `PlaceholderHelper.Pattern` — ห้ามเขียน Regex เอง

### 3.2 Pipeline Steps (Composable)

```
Stream (DOCX template)
  → WordTextReplacer    (แทนที่ {{placeholder}} ด้วยค่าจาก JSON)
  → WordTableExpander   (ขยาย Table rows สำหรับ array data)
  → WordMediaInjector   (inject รูปภาพจาก URL หรือ Base64)
  → byte[] output
```

**กฎ:** ห้ามรวม logic ทั้งหมดไว้ใน class เดียว (God Class violation) — ใช้ Pipeline pattern เสมอ

### 3.3 Drawing ML Id Rule

```csharp
// ❌ WRONG — Microsoft Word Desktop จะปฏิเสธ node นี้
new Pic.NonVisualDrawingProperties { Id = 0 }

// ✅ CORRECT — Id ต้องเป็น positive integer เสมอ
new Pic.NonVisualDrawingProperties { Id = (uint)counter + 1 }
```

---

## 4. Excel Template Engine (ClosedXML)

### 4.1 Placeholder Syntax

XLSX Templates ใช้ `{{placeholder}}` syntax ในเซลล์ Excel:

```
A1: {{companyName}}          ← Single value replacement
A5: {{reportTitle}}
A6: {{items.no}}             ← Row expansion (array row template)
B6: {{items.description}}
E6: {{items.amount}}
```

### 4.2 Pipeline Steps

```
Stream (XLSX template)
  → Smart PageSetup       (เคารพ Orientation เดิม, กำหนด A4 ถ้าค่าเดิมเป็น Letter/Default, ไม่ override PagesWide ถ้าผู้ใช้ตั้งค่าไว้)
  → ExcelTableExpander    (ขยาย rows สำหรับ array data)
  → ExcelMediaInjector    (inject รูปภาพ)
  → byte[] output
```

### 4.3 Performance Benchmark

จาก `PerformanceBenchmarkTests.cs`:
- 1,000 rows Excel expansion: **< 5 seconds** (SLA)
- 1,000 rows Word expansion: **< 5 seconds** (SLA)
- 1,000 rows HTML Handlebars merge: **< 1 second** (SLA)
- 50 concurrent HTML renders: all must succeed

---

## 5. Security Scanning (DOCX Upload)

ทุก DOCX ที่อัปโหลดผ่าน Template Management ต้องผ่าน `IDocxSecurityScanner.Scan()` ก่อน:

```csharp
using var scanCopy = new MemoryStream();
await fileStream.CopyToAsync(scanCopy, ct);
scanCopy.Position = 0;
fileStream.Position = 0; // Reset สำหรับ upload ต่อ

var scanResult = securityScanner.Scan(scanCopy);
if (!scanResult.IsSafe)
    throw new InvalidOperationException($"DOCX failed security scan: {string.Join("; ", scanResult.Threats)}");
```

**หมายเหตุ:** ต้อง Scan จาก Copy ของ stream เสมอ (OpenXML จะ dispose stream ต้นทาง)

---

## 6. MinIO Storage Buckets

| Bucket | เนื้อหา | ตัวอย่าง Key |
|---|---|---|
| `templates` | Template files (HTML, DOCX, XLSX) | `templates/invoice.html`, `templates/archive/invoice_v3.docx` |
| `outputs` | Generated documents | `outputs/2026/09/{logId}.pdf` |
| `fonts` | Custom fonts สำหรับ HTML engine | `fonts/Sarabun-Regular.woff2` |

**Presigned URLs:** 24 ชั่วโมง expiry — สร้างจาก `PublicEndpoint` config เสมอ (ห้าม string-replace หลัง sign)

</template_engine_scope>
