# SMK Document Server — System Test Cases

**Version:** Sprint 10  
**Backend:** ASP.NET Core .NET 10 · PostgreSQL 15 · MinIO · Gotenberg 8  
**Frontend:** Next.js 15 · React 19  
**Base URL (local):** `http://localhost:8080`  
**Default API Key:** `secret-smk-key-2026`

---

## สารบัญ

1. [Authentication & Security](#1-authentication--security)
2. [Project Management](#2-project-management)
3. [Template Management](#3-template-management)
4. [Document Generation](#4-document-generation)
5. [Document Preview](#5-document-preview)
6. [Field Mapping](#6-field-mapping)
7. [DataConnection](#7-dataconnection)
8. [Audit & Logs](#8-audit--logs)
9. [Unit Tests — MathExpressionResolver](#9-unit-tests--mathexpressionresolver)
10. [E2E Scenarios](#10-e2e-scenarios)

---

## 1. Authentication & Security

**Middleware:** `ApiKeyMiddleware` — ตรวจ header `X-API-Key` ทุก request

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| SEC-01 | Request ไม่มี API Key | ไม่มี header `X-API-Key` | 401 Unauthorized |
| SEC-02 | API Key ผิด | `X-API-Key: wrong-key` | 401 Unauthorized |
| SEC-03 | API Key ถูก (active) | `X-API-Key: secret-smk-key-2026` | 200 (endpoint ปกติ) |
| SEC-04 | API Key ที่ถูก revoke | Key ที่ `IsActive = false` | 401 Unauthorized |
| SEC-05 | API Key หมดอายุ | Key ที่ `ExpiresAt` ผ่านไปแล้ว | 401 Unauthorized |
| SEC-06 | Health check ไม่ต้องใช้ Key | `GET /` | 200 OK — `{ service, status, version, docs }` |
| SEC-07 | Multi-tenant isolation | Key ของ Project A ขอ template Project B | แสดงเฉพาะ template ของ Project A |
| SEC-08 | SMK_DEFAULT เห็นทุก project | Key ของ `SMK_DEFAULT` list template | เห็นทุก project |

---

## 2. Project Management

**Route prefix:** `/api/project`

### 2.1 List Projects

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| PRJ-01 | List ทั้งหมด | `GET /api/project` | 200 · array ของ project objects |
| PRJ-02 | Response fields | — | ทุก item มี `id`, `code`, `name`, `isActive`, `createdAt` |

### 2.2 Create Project

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| PRJ-03 | สร้าง project ใหม่ | `{ "code": "SALES", "name": "Sales App" }` | 200 · project object พร้อม `id` |
| PRJ-04 | Code ซ้ำ | `{ "code": "SALES", ... }` (มีอยู่แล้ว) | 409 Conflict |
| PRJ-05 | ไม่มี Code | `{ "name": "No Code" }` | 400 Bad Request |
| PRJ-06 | ไม่มี Name | `{ "code": "NO_NAME" }` | 400 Bad Request |
| PRJ-07 | Code มี special char | `{ "code": "SALE$%", ... }` | ตามการ validate (ควร reject หรือ sanitize) |

### 2.3 API Key Management

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| PRJ-08 | สร้าง API Key ใหม่ | `POST /api/project/{id}/keys` · `{ "name": "Web App Key" }` | 200 · `{ id, key, name, isActive }` |
| PRJ-09 | สร้าง Key สำหรับ project ไม่มีอยู่ | projectId ไม่ถูกต้อง | 404 Not Found |
| PRJ-10 | Revoke API Key | `DELETE /api/project/keys/{keyId}` | 200 OK |
| PRJ-11 | Revoke Key ไม่มีอยู่ | keyId สุ่ม | 404 Not Found |
| PRJ-12 | ชื่อ Key เว้นว่าง | `{ "name": "" }` | 400 Bad Request |

---

## 3. Template Management

**Route prefix:** `/api/template`

### 3.1 Upload Template (Binary)

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-01 | Upload .docx | multipart form · ไฟล์ .docx | 200 · `{ id, fileName, engineType: "OpenXML" }` |
| TPL-02 | Upload .xlsx | multipart form · ไฟล์ .xlsx | 200 · `{ engineType: "OpenXML" }` |
| TPL-03 | Upload .pdf (ไม่รองรับ) | multipart form · ไฟล์ .pdf | 400 Bad Request |
| TPL-04 | isGlobal โดย non-default project | Key ของ `SALES` · `isGlobal=true` | 403 Forbidden |
| TPL-05 | isGlobal โดย SMK_DEFAULT | Key ของ `SMK_DEFAULT` · `isGlobal=true` | 200 OK |
| TPL-06 | Upload ไฟล์ซ้ำชื่อ | ชื่อเดิม ไฟล์ใหม่ | 200 · version เพิ่ม (currentVersion+1) |

### 3.2 Save Web Editor Templates

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-07 | Save Tiptap (HTML) | `POST /api/template/tiptap` · `{ "fileName": "invoice.html", "content": "<p>...</p>" }` | 200 · `{ fileName, engineType: "Tiptap" }` |
| TPL-08 | Save ReportBro (JSON) | `POST /api/template/reportbro` · `{ "fileName": "report.json", "content": "{...}" }` | 200 · `{ fileName, engineType: "ReportBro" }` |
| TPL-09 | Tiptap ไม่มี fileName | `{ "content": "..." }` | 400 Bad Request |
| TPL-10 | Tiptap ไม่มี content | `{ "fileName": "x.html" }` | 400 Bad Request |
| TPL-11 | ReportBro ไม่มี fileName | `{ "content": "..." }` | 400 Bad Request |

### 3.3 List Templates

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-12 | List ของ project ตัวเอง | `GET /api/template` (key ของ SALES) | 200 · เฉพาะ template ของ SALES |
| TPL-13 | List กรอง projectId | `GET /api/template?projectId={id}` (SMK_DEFAULT key) | 200 · template ของ project นั้น |
| TPL-14 | Response fields | — | `id`, `fileName`, `engineType`, `currentVersion`, `isGlobal`, `uploadedAt` |

### 3.4 Template Versioning

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-15 | ดู versions | `GET /api/template/{fileName}/versions` | 200 · array เรียง versionNumber |
| TPL-16 | Rollback ไป version ก่อน | `POST /api/template/{fileName}/rollback/1` | 200 · currentVersion = 1 |
| TPL-17 | Rollback version ไม่มีอยู่ | rollback ไป version 999 | 400 Bad Request |
| TPL-18 | Rollback template ไม่มีอยู่ | fileName ผิด | 404 Not Found |

### 3.5 Template Schema & Download

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-19 | ดู placeholder schema | `GET /api/template/{fileName}/schema` | 200 · `{ placeholders: ["buyerName", "total", ...] }` |
| TPL-20 | Schema ของ template ไม่มีอยู่ | fileName ผิด | 404 Not Found |
| TPL-21 | Download .docx | `GET /api/template/{fileName}/download` | 200 · bytes · `Content-Type: application/vnd.openxmlformats...` |
| TPL-22 | Download .xlsx | — | 200 · `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| TPL-23 | Download ไม่มีอยู่ | fileName ผิด | 404 Not Found |
| TPL-24 | HEAD request | `HEAD /api/template/{fileName}/download` | 200 · header เท่านั้น ไม่มี body |

### 3.6 Delete Template

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| TPL-25 | ลบ template | `DELETE /api/template/{fileName}` | 200 OK |
| TPL-26 | ลบไม่มีอยู่ | fileName ผิด | 404 Not Found |

---

## 4. Document Generation

**Route:** `POST /api/document/generate`  
**ผลลัพธ์:** URL ชั่วคราว (1 ชั่วโมง) ชี้ไฟล์ใน MinIO · บันทึก GenerationLog

### 4.1 Basic Generation

| TC | Test Case | Template | Data | Expected |
|----|-----------|----------|------|----------|
| GEN-01 | Generate .docx | invoice.docx (OpenXML) | `{ "buyerName": "บริษัท A", "total": "5000" }` | 200 · `{ url, fileName, fileSizeBytes }` |
| GEN-02 | Generate .xlsx | report.xlsx (OpenXML) | `{ "month": "กันยายน" }` | 200 · signed URL (.xlsx) |
| GEN-03 | Generate HTML → PDF | page.html (Tiptap) | `{ "title": "ข้อความ" }` | 200 · signed URL (.pdf) |
| GEN-04 | Generate ReportBro → PDF | report.json (ReportBro) | `{ ... }` | 200 · signed URL (.pdf) |
| GEN-05 | OutputFormat = "docx" บังคับ | template ใด | `outputFormat: "docx"` | 200 · ไฟล์ .docx |
| GEN-06 | OutputFormat = "pdf" | template .docx | `outputFormat: "pdf"` | 200 · แปลงเป็น PDF ผ่าน Gotenberg |
| GEN-07 | OutputFormat = "original" | template ใด | `outputFormat: "original"` | 200 · ไฟล์ extension เดิม |

### 4.2 Validation & Error

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| GEN-08 | ไม่มี templateName | `{ "data": {} }` | 400 Bad Request |
| GEN-09 | Template ไม่มีอยู่ | `{ "templateName": "ghost.docx" }` | 404 Not Found |
| GEN-10 | Data = null | `{ "templateName": "invoice.docx" }` | 200 · document ที่ placeholder ว่างเปล่า |
| GEN-11 | Data มี key ที่ template ไม่มี | extra keys | 200 · ไม่ error (extra keys ถูก ignore) |

### 4.3 Advanced Data Types

| TC | Test Case | Data Structure | Expected |
|----|-----------|---------------|----------|
| GEN-12 | Table data | `{ "table": [{ "rows": [...] }] }` | 200 · ตาราง render ได้ |
| GEN-13 | QR Code | `{ "qrcode": { "ref": { "text": "https://..." } } }` | 200 · QR code ใน document |
| GEN-14 | Barcode | `{ "barcode": { "barcode": { "text": "0123456789", "format": "Code128" } } }` | 200 · barcode ใน document |
| GEN-15 | Image (URL) | `{ "image": { "logo": { "src": "https://..." } } }` | 200 · รูปภาพแทรกลงใน document |
| GEN-16 | PDF Watermark | `{ "watermark": { "text": "DRAFT", "opacity": 0.3 } }` | 200 · ไฟล์ PDF มี watermark |
| GEN-17 | PDF Password | `{ "pdfPassword": { "userPassword": "1234" } }` | 200 · PDF เปิดต้องใส่รหัส |

### 4.4 Thai Transforms (Auto-derive เมื่อไม่มี FieldMapping)

| TC | Test Case | Key/Value | Expected Transform |
|----|-----------|-----------|-------------------|
| GEN-18 | Thai Baht text | key ลงท้าย `amount`/`price`/`total` | auto-generate `key:baht` = ตัวอักษรบาทไทย |
| GEN-19 | Thai Date | key ลงท้าย `date`/`วันที่` | auto-generate `key:thaidate` = วันที่ภาษาไทย |
| GEN-20 | Thai DateTime | key ลงท้าย `datetime`/`timestamp` | auto-generate `key:thaidatetime` |
| GEN-21 | Phone | key ลงท้าย `phone`/`tel`/`mobile` | auto-generate `key:phone` = `0x-xxxx-xxxx` |
| GEN-22 | Thai ID Card | key ลงท้าย `id`/`idcard`/`citizenid` | auto-generate `key:idcard` = `x-xxxx-xxxxx-xx-x` |

### 4.5 Audit Log Verification

| TC | Test Case | Expected |
|----|-----------|----------|
| GEN-23 | หลัง generate สำเร็จ | GenerationLog บันทึก: status=SUCCESS, templateName, outputFormat, fileSizeBytes, executionTimeMs |
| GEN-24 | หลัง generate ล้มเหลว | GenerationLog บันทึก: status=FAILED, errorMessage |

---

## 5. Document Preview

**Route:** `POST /api/document/preview`  
**ผลลัพธ์:** PDF bytes stream · ไม่บันทึก MinIO · ไม่บันทึก GenerationLog

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| PRV-01 | Preview .docx | template + data | 200 · bytes · `Content-Type: application/pdf` |
| PRV-02 | Preview .html (Tiptap) | template + data | 200 · PDF bytes |
| PRV-03 | Preview ReportBro | template + data | 200 · PDF bytes |
| PRV-04 | ไม่มี templateName | `{}` | 400 Bad Request |
| PRV-05 | Template ไม่มีอยู่ | fileName ผิด | 404 Not Found |
| PRV-06 | Zero side effects | preview แล้วดู audit logs | **ไม่มี** GenerationLog เพิ่ม |
| PRV-07 | Zero side effects | preview แล้วดู MinIO | **ไม่มีไฟล์ถูก upload** |

---

## 6. Field Mapping

**Route prefix:** `/api/template/{fileName}/mappings`

### 6.1 CRUD

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| FM-01 | บันทึก mappings | `PUT /api/template/{fileName}/mappings` · `[{...}]` | 200 · mappings ที่บันทึกแล้ว |
| FM-02 | ดู mappings | `GET /api/template/{fileName}/mappings` | 200 · array เรียงตาม SortOrder |
| FM-03 | บันทึก mappings ซ้ำ (replace) | PUT อีกครั้ง | 200 · ของเดิมถูกแทนที่ทั้งหมด |
| FM-04 | template ไม่มี mapping | GET ไฟล์ที่ไม่มี mapping | 200 · `[]` |
| FM-05 | Template ไม่มีอยู่ | GET/PUT fileName ผิด | 404 Not Found |

### 6.2 DataSourceType = "json" (ค่า default)

| TC | Test Case | SourcePath | JSON Input | Expected PlaceholderType |
|----|-----------|-----------|-----------|--------------------------|
| FM-06 | Text ธรรมดา | `customer.name` | `{ "customer": { "name": "สมชาย" } }` | Replace["name"] = "สมชาย" |
| FM-07 | Array index | `items[0].quantity` | `{ "items": [{ "quantity": "5" }] }` | Replace["qty"] = "5" |
| FM-08 | Nested path หลายชั้น | `order.billing.address.street` | — | ค่าที่ nested สุด |
| FM-09 | Path ไม่มีอยู่ | `nonexistent.path` | `{}` | DefaultValue ถ้ามี หรือ ว่าง |
| FM-10 | Table type | `items` | `{ "items": [{"name":"A","qty":"2"}] }` | Table[0].Rows = 1 row |
| FM-11 | QR Code type | `qrUrl` | `{ "qrUrl": "https://smk.co.th" }` | Qrcode["ref"].Text = URL |
| FM-12 | Barcode type | `barcode` | `{ "barcode": "0123456789" }` | Barcode["ref"].Text = "0123456789" |
| FM-13 | Image type | `logoUrl` | `{ "logoUrl": "https://..." }` | Image["logo"].Src = URL |

### 6.3 DataSourceType = "expr" (Math Expression)

> SortOrder ของ `expr` ต้องมากกว่า mapping source ที่อ้างถึง

| TC | Test Case | Expression | Variables ก่อนหน้า | Expected |
|----|-----------|-----------|-------------------|---------|
| FM-14 | คูณ qty × price | `qty * unitPrice` | qty="10", unitPrice="500" | `"5000"` |
| FM-15 | VAT 7% | `subtotal * 0.07` | subtotal="5000" | `"350"` |
| FM-16 | รวมสุทธิ | `subtotal + vat` | subtotal="5000", vat="350" | `"5350"` |
| FM-17 | IIF conditional | `IIF(qty > 100, subtotal * 0.9, subtotal)` | qty="50", subtotal="5000" | `"5000"` |
| FM-18 | IIF ผ่าน threshold | `IIF(qty > 100, subtotal * 0.9, subtotal)` | qty="150", subtotal="5000" | `"4500"` |
| FM-19 | Precedence ถูกต้อง | `a + b * c` | a="2", b="3", c="4" | `"14"` (ไม่ใช่ 20) |
| FM-20 | Variable เป็น null | `qty * price` | qty=null, price="100" | graceful fallback ไม่ throw |
| FM-21 | Variable ไม่ใช่ตัวเลข | `qty * 100` | qty="abc" | fallback expression text |
| FM-22 | ROUND (ไม่รองรับ) | `ROUND(total, 2)` | total="100" | fallback (ไม่ throw) |
| FM-23 | SortOrder ผิด | expr อยู่ก่อน json source | — | ค่าว่าง (variable ยังไม่ถูก resolve) |

### 6.4 DataSourceType = "sql"

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| FM-24 | SQL ดึง column ที่มีอยู่ | `sourcePath = "name"` · `sqlQuery = "SELECT name FROM customers WHERE id=1"` | Replace["customerName"] = ค่าจาก DB |
| FM-25 | SQL query ที่ไม่มีผลลัพธ์ | query return 0 rows | DefaultValue หรือ ว่าง |
| FM-26 | 2 mapping ใช้ query เดิม | query เหมือนกัน 100% | SQL execute ครั้งเดียว (SQL cache hit) |
| FM-27 | DataConnectionId ไม่มีอยู่ | GUID ผิด | graceful fallback ไม่ throw |
| FM-28 | SQL syntax error | query ผิด syntax | graceful fallback ไม่ throw |
| FM-29 | Connection ใช้งานไม่ได้ | DB down | graceful fallback ไม่ throw |

### 6.5 Transform

| TC | Test Case | Value | Transform | Expected |
|----|-----------|-------|-----------|----------|
| FM-30 | Thai Baht | `"5000"` | `formatThaiBaht` | `"ห้าพันบาทถ้วน"` |
| FM-31 | Thai Date | `"2026-09-13"` | `formatThaiDate` | `"13 กันยายน 2569"` |
| FM-32 | Thai DateTime | `"2026-09-13T14:30:00"` | `formatThaiDateTime` | วันที่-เวลาภาษาไทย |
| FM-33 | Currency format | `"1234567.89"` | `formatCurrency` | `"1,234,567.89"` |
| FM-34 | Phone | `"0812345678"` | `formatPhone` | `"081-234-5678"` |
| FM-35 | Thai ID Card | `"1234567890123"` | `formatThaiId` | `"1-2345-67890-12-3"` |
| FM-36 | Transform ไม่รู้จัก | `"hello"` | `"unknown_transform"` | คืนค่าเดิม unchanged |

### 6.6 Mapping Preview

**Route:** `POST /api/template/{fileName}/mappings/preview`

| TC | Test Case | DataSourceType | Expected |
|----|-----------|----------------|---------|
| FM-37 | Preview json mapping | json | TransformedValue = ค่าจาก sampleJson |
| FM-38 | Preview expr mapping | expr | TransformedValue = ผลการคำนวณ |
| FM-39 | Preview sql mapping | sql | TransformedValue = `"(sql — ไม่สามารถดูตัวอย่างได้)"` |
| FM-40 | Preview path ไม่มีอยู่ | json | Found = false · TransformedValue = DefaultValue |

---

## 7. DataConnection

**Route prefix:** `/api/dataconnection`

### 7.1 CRUD

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| DC-01 | สร้าง DataConnection | `{ "name": "ERP DB", "databaseType": "postgresql", "connectionString": "Host=db;..." }` | 200 · `{ id, name, databaseType, connectionString: null }` |
| DC-02 | Response ไม่ return ConnectionString | หลัง create/update | `connectionString` field = `null` เสมอ |
| DC-03 | List ทั้งหมด | `GET /api/dataconnection` | 200 · array · ทุก item `connectionString = null` |
| DC-04 | List กรอง projectId | `GET /api/dataconnection?projectId={id}` | 200 · เฉพาะของ project นั้น |
| DC-05 | อัปเดตชื่อ (ไม่เปลี่ยน CS) | `PUT` · `{ "name": "New Name", "connectionString": "" }` | 200 · CS ยังคงเป็น encrypted เดิม |
| DC-06 | อัปเดตพร้อมเปลี่ยน CS | `PUT` · `{ "connectionString": "Host=new-host;..." }` | 200 · CS ถูก re-encrypt ใหม่ |
| DC-07 | ลบ DataConnection | `DELETE /api/dataconnection/{id}` | 200 · `{ message: "Deleted." }` |
| DC-08 | ลบ ID ไม่มีอยู่ | GUID สุ่ม | 404 Not Found |
| DC-09 | อัปเดต ID ไม่มีอยู่ | GUID สุ่ม | 404 Not Found |

### 7.2 Validation

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| DC-10 | ไม่มี Name | `{ "connectionString": "..." }` | 400 · `"Name is required."` |
| DC-11 | Name ว่าง | `{ "name": "", "connectionString": "..." }` | 400 · `"Name is required."` |
| DC-12 | ไม่มี ConnectionString | `{ "name": "DB" }` | 400 · `"ConnectionString is required."` |
| DC-13 | CS ว่างตอน create | `{ "name": "DB", "connectionString": "" }` | 400 · `"ConnectionString is required."` |

### 7.3 Test Connectivity

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| DC-14 | Test connection สำเร็จ | `POST /api/dataconnection/{id}/test` · DB พร้อมใช้งาน | 200 · `{ "success": true, "message": "Connection successful." }` |
| DC-15 | Test connection ล้มเหลว | DB ปิดหรือ CS ผิด | 200 · `{ "success": false, "message": "Connection failed." }` |
| DC-16 | Test ID ไม่มีอยู่ | GUID ผิด | 404 Not Found |

### 7.4 Security (Encryption)

| TC | Test Case | Expected |
|----|-----------|----------|
| DC-17 | CS เก็บใน DB | ค่าใน column `ConnectionStringEncrypted` เป็น ciphertext — ไม่ใช่ plain text |
| DC-18 | CS ไม่เคย return ใน API | ทุก response มี `connectionString = null` |
| DC-19 | Decrypt สำหรับ internal use | `GetDecryptedConnectionStringAsync` return plain CS สำหรับ SQL execution เท่านั้น |

---

## 8. Audit & Logs

**Route prefix:** `/api/audit`

| TC | Test Case | Input | Expected |
|----|-----------|-------|----------|
| AUD-01 | List logs | `GET /api/audit/logs` | 200 · paginated array |
| AUD-02 | List กรอง projectId | `GET /api/audit/logs?projectId={id}` | 200 · เฉพาะ project นั้น |
| AUD-03 | List กรอง status | `GET /api/audit/logs?status=SUCCESS` | 200 · เฉพาะ SUCCESS |
| AUD-04 | Pagination | `GET /api/audit/logs?page=2&pageSize=10` | 200 · หน้าที่ 2 |
| AUD-05 | Default pageSize | `GET /api/audit/logs` | pageSize default = 50 |
| AUD-06 | Metrics | `GET /api/audit/metrics` | 200 · `{ totalCount, successCount, failedCount, avgExecutionTimeMs }` |
| AUD-07 | Log fields | หลัง generate สำเร็จ | มี: `templateName`, `outputFormat`, `status`, `fileSizeBytes`, `executionTimeMs`, `createdAt` |

---

## 9. Unit Tests — MathExpressionResolver

**Run:** `cd backend.Tests && dotnet test`  
**Result:** ✅ 16/16 Passed

| # | Test Name | Expression | Input | Expected | Pass |
|---|-----------|-----------|-------|----------|------|
| 1 | Addition | `a + b` | a=10, b=5 | `"15"` | ✅ |
| 2 | Subtraction | `total - discount` | total=100, discount=15 | `"85"` | ✅ |
| 3 | Multiplication | `qty * unitPrice` | qty=3, unitPrice=250.50 | `"751.5"` | ✅ |
| 4 | Division | `total / n` | total=100, n=4 | `"25"` | ✅ |
| 5 | Invoice subtotal (multi-item) | `qty1 * price1 + qty2 * price2` | qty1=2, p1=500, qty2=5, p2=120 | `"1600"` | ✅ |
| 6 | VAT 7% | `subtotal * 0.07` | subtotal=1000 | `"70"` | ✅ |
| 7 | Grand total | `subtotal + vat` | subtotal=1000, vat=70 | `"1070"` | ✅ |
| 8 | ROUND (ไม่รองรับ) | `ROUND(val / 7, 2)` | val=3 | not null, not empty | ✅ |
| 9 | IIF conditional | `IIF(qty > 5, 100, 0)` | qty=10 | `"100"` | ✅ |
| 10 | Literal (no vars) | `10 + 20 * 3` | — | `"70"` | ✅ |
| 11 | Non-numeric var skipped | `qty * 100` | qty=5, name="สมชาย" | `"500"` | ✅ |
| 12 | Null var skipped | `qty * price` | qty=null, price=100 | not null | ✅ |
| 13 | Empty vars | `100 + 50` | {} | `"150"` | ✅ |
| 14 | Zero division | `x / y` | x=10, y=0 | not null | ✅ |
| 15 | InvariantCulture decimal | `price * qty` | price=1234.56, qty=2 | `"2469.12"` | ✅ |
| 16 | Operator precedence | `a + b * c` | a=2, b=3, c=4 | `"14"` | ✅ |

---

## 10. E2E Scenarios

### E2E-01: ใบแจ้งหนี้อัตโนมัติ (json + expr + transform)

**Objective:** สร้างใบแจ้งหนี้จาก JSON payload — คำนวณยอดอัตโนมัติ

**FieldMapping Setup:**

| SortOrder | Placeholder | DataSourceType | SourcePath |
|-----------|-------------|----------------|-----------|
| 0 | invoiceNo | json | `invoiceNo` |
| 1 | invoiceDate | json | `date` |
| 2 | qty | json | `items[0].quantity` |
| 3 | unitPrice | json | `items[0].unitPrice` |
| 4 | subtotal | expr | `qty * unitPrice` |
| 5 | vat | expr | `subtotal * 0.07` |
| 6 | grandTotal | expr | `subtotal + vat` |
| 7 | grandTotalText | expr (+ transform) | `grandTotal` · transform=`formatThaiBaht` |

**Input Payload:**
```json
{
  "templateName": "invoice.docx",
  "outputFormat": "pdf",
  "data": {
    "invoiceNo": "INV-2026-001",
    "date": "2026-09-13",
    "items": [{ "quantity": "10", "unitPrice": "850" }]
  }
}
```

**Expected Resolution:**

| Placeholder | ค่า |
|-------------|-----|
| invoiceNo | INV-2026-001 |
| invoiceDate | 2026-09-13 |
| qty | 10 |
| unitPrice | 850 |
| subtotal | 8500 |
| vat | 595 |
| grandTotal | 9095 |
| grandTotalText | เก้าพันเก้าสิบห้าบาทถ้วน |

---

### E2E-02: ดึงข้อมูลลูกค้าจาก DB (json + sql + expr)

**Objective:** ดึงชื่อและที่อยู่ลูกค้าจาก PostgreSQL แล้วรวมกับการคำนวณ

**Prerequisites:**
1. สร้าง DataConnection ชี้ DB ที่มีตาราง `customers`
2. ตาราง: `id, name, tax_id, address`

**FieldMapping Setup:**

| SortOrder | Placeholder | DataSourceType | SourcePath | SQL Query |
|-----------|-------------|----------------|-----------|-----------|
| 0 | customerName | sql | `name` | `SELECT name, tax_id FROM customers WHERE id = 1` |
| 1 | customerTaxId | sql | `tax_id` | `SELECT name, tax_id FROM customers WHERE id = 1` |
| 2 | qty | json | `items[0].quantity` | — |
| 3 | unitPrice | json | `items[0].unitPrice` | — |
| 4 | subtotal | expr | `qty * unitPrice` | — |

**Expected Resolution:**
- SQL executed **1 ครั้ง** (แม้มี 2 mappings — cache hit ครั้งที่ 2)
- `customerName` = ชื่อจาก DB
- `customerTaxId` = เลขผู้เสียภาษีจาก DB
- `subtotal` = qty × unitPrice

---

### E2E-03: ReportBro Pixel-Perfect PDF

**Objective:** ออกแบบ template ใน ReportBro Designer แล้ว generate PDF

1. เปิด Portal → หน้า "Report Designer"
2. ออกแบบ layout ลาก-วาง
3. กด "บันทึกลง Server" → `POST /api/template/reportbro`
4. ตั้งค่า FieldMapping สำหรับ template JSON
5. Generate ผ่าน `POST /api/document/generate`
6. Expected: PDF pixel-perfect ตาม layout ที่ออกแบบ

---

### E2E-04: Tiptap HTML Editor → PDF

**Objective:** แก้ template HTML ใน Tiptap แล้ว preview + generate

1. เปิด Portal → หน้า Template Studio
2. เลือก template .html
3. แก้ไขใน Tiptap Editor (ใส่ `{{placeholder}}`)
4. กด Save → `POST /api/template/tiptap`
5. Preview → `POST /api/document/preview`
6. Expected: PDF ที่ render ด้วย Gotenberg Chromium

---

### E2E-05: Version Rollback

**Objective:** upload template ใหม่แล้ว rollback ไป version เดิม

1. Upload `invoice.docx` v1 → currentVersion = 1
2. Upload `invoice.docx` v2 (ไฟล์ใหม่) → currentVersion = 2
3. Generate ด้วย v2 → ผลลัพธ์จาก v2
4. Rollback: `POST /api/template/invoice.docx/rollback/1`
5. Generate อีกครั้ง → ผลลัพธ์จาก v1

---

## ข้อจำกัดที่ทราบ (Known Limitations)

| หมวด | ข้อจำกัด | Sprint |
|------|---------|--------|
| expr | `ROUND(x, n)` ไม่รองรับ — `DataTable.Compute` รองรับแค่ arithmetic operators | 10 |
| expr | SortOrder ของ expr ต้องสูงกว่า json source ที่อ้างถึง (user responsibility) | 10 |
| sql | รองรับแค่ PostgreSQL (Npgsql) — SQL Server/MySQL ยังไม่ implement | 10 |
| sql | SQL query เป็น static string — ยังไม่ inject ค่าจาก JSON payload | 10 |
| security | Data Protection keys เก็บใน container — recreate container = decrypt ไม่ได้ | 10 |
| storage | MinIO signed URL หมดอายุใน 1 ชั่วโมง | — |
| pdf | Gotenberg ต้อง running — PDF generation ล้มเหลวถ้า container down | — |

---

## Test Files

| ไฟล์ | คำอธิบาย |
|------|---------|
| [`backend.Tests/MathExpressionResolverTests.cs`](../../backend.Tests/MathExpressionResolverTests.cs) | xUnit unit tests (16 test cases) |
| [`docs/tests/sprint10-api-tests.http`](sprint10-api-tests.http) | VS Code REST Client · DataConnection + FieldMapping API |
| [`docs/tests/sprint10-e2e-scenario.md`](sprint10-e2e-scenario.md) | E2E scenario ใบแจ้งหนี้ step-by-step |
| [`docs/tests/sprint10-test-results.md`](sprint10-test-results.md) | ผลรัน unit test จริง |
