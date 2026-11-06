# SMK Document Server (V2) — Test Run Results

**วันที่รัน:** 2026-09-17  
**Environment:** Local Docker V2 (`smk-v2` stack — 5 containers healthy)  
**Backend Runtime:** .NET 10 Clean Architecture (`SmkDocServerV2`)  
**Frontend Runtime:** Next.js 15 App Router · React 19 · Vitest  

---

## 📊 สรุปผลการทดสอบรวม (Executive Summary)

| ขอบเขตการทดสอบ (Scope) | จำนวนทั้งหมด | PASS | FAIL | สถานะ |
|---|:---:|:---:|:---:|:---:|
| **Scope 1: Backend Unit Tests (xUnit)** | 145 | 145 | 0 | ✅ **100% Pass** |
| **Scope 2: Frontend Unit Tests (Vitest)** | 18 | 18 | 0 | ✅ **100% Pass** |
| **Scope 3: Docker Containers Health** | 5 | 5 | 0 | ✅ **100% Healthy** |
| **Scope 4: Live HTTP API Endpoints** | 12 | 12 | 0 | ✅ **100% Pass** |
| **Scope 5: Document Pipeline & S3 Delivery**| 3 | 3 | 0 | ✅ **100% Pass** |
| **รวมทั้งสิ้น** | **183** | **183** | **0** | 🏆 **All Passed** |

---

## Scope 1: Backend Unit Tests (.NET 10 xUnit) ✅ 145/145

**Framework:** xUnit.net · .NET 10.0 · เวลาทดสอบ 17 วินาที  
**ผลลัพธ์:** `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145`

### รายการ Test Suites ที่ผ่าน:
1. **Core Engines:**
   - `DocxTemplateEngineTests`: Word Table Expander, Text Replacer, DrawingML Media Injector (`Id > 0`)
   - `ExcelTemplateEngineTests`: Cell Replacer, Formulas Preserve, Dynamic Table Expand, Media Injector
   - `HtmlTemplateEngineTests`: Chromium Gotenberg PDF rendering, Thai font Sarabun
2. **Business Logic & Math:**
   - `MathExpressionResolverTests` (16 tests): VAT 7%, Subtotal, GrandTotal, Operator precedence, Zero division fallback
   - `ThaiDataTransformerTests`: BahtText conversion, Thai Buddhist Era date formatting, number formats
   - `PlaceholderHelperTests`: Regex matching `{{placeholder}}` และ `<Field>` tags
3. **Security & Utilities:**
   - `DocxSecurityScannerServiceTests`: ตรวจจับ Macro, External link injection, DDE attack
   - `DataProtectionServiceTests`: AES-256 Roundtrip encrypt/decrypt, empty inputs, default key fallback
   - `BarcodeQrCodeServiceTests`: Barcode Code 128 และ QR Code PNG generation พร้อม Magic Header
   - `ImageOptimizerServiceTests`: Image resizing, compression, format preservation
   - `StorageBucketsTests`: S3 Bucket name constants
4. **Application Use Cases:**
   - `GenerateDocumentUseCaseTests`: Multi-dataset merge, Engine routing, MinIO upload, Presigned URL, Version audit
   - `PreviewDocumentUseCaseTests`: Stateless streaming, Zero MinIO upload, Zero database mutation
   - `RenderStatelessDocumentUseCaseTests`: Memory stream rendering
   - `DocumentVersionUseCaseTests`: SHA-256 hash checking, download presigned URL
   - `TemplateManagementUseCaseTests`: CRUD, Unique slug enforcement, Auto-increment version
   - `TemplateValidateUseCaseTests`: Template syntax & placeholder validation
   - `FieldMappingUseCaseTests` & `PreviewMappingUseCaseTests`: Mapping resolution & preview
   - `ApiKeyUseCaseTests`: Key validation, active/revocation check
   - `DataConnectionUseCaseTests`: CRUD, AES connection string encryption, connectivity testing
   - `DatasetUseCaseTests` & `TemplateDatasetUseCaseTests`: Dataset CRUD, Template-dataset link, Duplicate alias rejection

---

## Scope 2: Frontend Unit Tests (Vitest) ✅ 18/18

**Framework:** Vitest v5.0.1 · Next.js 15 · เวลาทดสอบ 3.65 วินาที  
**ผลลัพธ์:** `Test Files 4 passed (4), Tests 18 passed (18)`

1. **`schemas.test.ts` (Zod Schemas Integrity):**
   - `GenerateDocumentRequestSchema`: Accept valid payload, reject invalid format (`bmp`)
   - `CreateTemplateRequestSchema`: Enforce slug format (lowercase hyphenated)
   - `SaveFieldMappingItemSchema`: Validate required fields & Thai transform
   - `CreateApiKeyRequestSchema`: Reject empty name/callerApp
2. **`datasourcesSchemas.test.ts` (Datasources & Datasets Schemas):**
   - `dataConnectionSchema` & `createDataConnectionSchema`: Validate connection inputs
   - `testDataConnectionSchema`: Validate connectivity test payload
   - `DatasetSchema` & `CreateDatasetSchema`: Enforce valid UUID & SQL Query
   - `SaveTemplateDatasetItemSchema` & `TemplateDatasetDtoSchema`: Validate Alias & SortOrder
3. **`tokens.test.ts` (Design Tokens SSoT):**
   - Validate category accents (Contracts Indigo, Financial Emerald, Official Sky Blue, HR Amber, Operations Cyan)
   - Enforce border radii constraint 2px - 4px
   - Base canvas Ice-White verification
4. **`thaiTransforms.test.ts` (Thai Utilities):**
   - Thai Baht text formatting, Thai Date conversions

---

## Scope 3: Docker Containers Health Check ✅ 5/5 Healthy

| Container Name | Image | Status | Ports | Healthcheck |
|---|---|:---:|:---:|:---:|
| `smk-postgres-v2` | `postgres:15-alpine` | Up (healthy) | `5433:5432` | `pg_isready` passed |
| `smk-minio-v2` | `minio/minio` | Up (healthy) | `9000, 9001` | `mc ready local` passed |
| `smk-gotenberg-v2` | `gotenberg/gotenberg:8` | Up (healthy) | `3000` | HTTP healthcheck passed |
| `smk-doc-server-v2` | `smk-v2-doc-server-v2` | Up (healthy) | `8080` | `/health` endpoint passed |
| `smk-doc-portal-v2` | `smk-v2-portal-v2` | Up (healthy) | `3500:3000` | Next.js server passed |

---

## Scope 4: Live HTTP API Tests (Local Docker) ✅ 12/12

| Test Code | Endpoint / Scenario | HTTP Status | ผลลัพธ์ | หมายเหตุ |
|---|---|:---:|:---:|---|
| **SEC-01** | `GET /health` | **200 OK** | ✅ PASS | รายงาน status: healthy (postgres + gotenberg ok) |
| **SEC-02** | `GET /api/templates` (ไม่มี Key) | **401 Unauthorized**| ✅ PASS | ถูกบล็อกโดย `ApiKeyMiddleware` |
| **SEC-03** | `GET /api/templates` (Key ผิด) | **401 Unauthorized**| ✅ PASS | ถูกบล็อกโดย `ApiKeyMiddleware` |
| **SEC-04** | `GET /api/templates` (Master Key) | **200 OK** | ✅ PASS | เข้าถึงข้อมูลได้ถูกต้อง |
| **SEC-05** | `GET /api/api-keys` | **200 OK** | ✅ PASS | คืนรายการคีย์ และบันทึก `lastUsedAt` แบบเรียลไทม์ |
| **TPL-01** | `POST /api/templates` | **200 OK** | ✅ PASS | สร้าง Template `test-receipt-v2` สำเร็จ |
| **TPL-02** | `PUT /api/templates/:id/html` | **200 OK** | ✅ PASS | บันทึก HTML ลง MinIO, เพิ่ม version: 2 |
| **TPL-03** | `GET /api/templates/:id/html` | **200 OK** | ✅ PASS | ดึงเนื้อหา HTML กลับมาได้สมบูรณ์ |
| **DS-01**  | `GET /api/data-connections` | **200 OK** | ✅ PASS | คืน Array รายการ Data Connections |
| **DS-02**  | `GET /api/datasets` | **200 OK** | ✅ PASS | คืน Array รายการ Datasets |
| **LOG-01** | `GET /api/logs` (ก่อน Generate) | **200 OK** | ✅ PASS | รายการ logs: [] (total: 0) |
| **LOG-02** | `GET /api/logs` (หลัง Generate) | **200 OK** | ✅ PASS | บันทึก Log สำเร็จ: status SUCCESS, duration 975ms |

---

## Scope 5: Document Pipeline, Preview & S3 Delivery ✅ 3/3

### 1. Live Preview (`POST /api/documents/preview/test-receipt-v2`)
* **Request:** Payload JSON `{"customerName": "นายสมชาย ใจดี", "amount": "5,000"}`
* **Response Header:** `Content-Type: application/pdf; Content-Disposition: inline`
* **Response Body:** Binary PDF Stream (`%PDF-1.4 ... %%EOF`)
* **Thai Font Verification:** ฟอนต์ `Sarabun-Regular` ถูกฝังลงใน PDF ชัดเจน ไม่ตกขอบ
* **Zero Side-Effects Check:** ✅ PASS — MinIO outputs ไม่มีขยะไฟล์ค้าง และ Generation Logs คงเดิม 0 รายการ

### 2. Document Generation (`POST /api/documents/generate/test-receipt-v2`)
* **Request:** Payload JSON พร้อม `documentRef: "INV-2026-9999"`
* **Response:**
  ```json
  {
    "url": "http://localhost:9000/outputs/outputs/2026/09/17/test-receipt-v2_4e09c83857f941c58baacdd3ada48a9b.pdf?X-Amz-Algorithm=...",
    "expiresAt": "2026-09-18T13:26:50.8176827+00:00",
    "generationId": "4e09c838-57f9-41c5-8baa-cdd3ada48a9b",
    "outputFormat": "pdf"
  }
  ```
* **Performance:** ใช้เวลาเพียง **975 ms** (< 1 วินาที)
* **S3 Download Verification:** ดาวน์โหลดไฟล์ขนาด **11,529 bytes** จาก MinIO สำเร็จ

### 3. Legal Audit Trail & Versioning (`GET /api/documents/INV-2026-9999/versions`)
* **Response:** คืนประวัติเอกสาร Version 1 ผูกกับ GenerationLog ID และ Template Version ID ถูกต้อง 100%
