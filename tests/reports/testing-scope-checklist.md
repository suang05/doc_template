# SMK Document Server (V2) — Complete Testing Scope Checklist

**เอกสาร:** แผนและรายการตรวจสอบการทดสอบทุกขอบเขต (All Testing Scopes Checklist)  
**สถาปัตยกรรม:** Clean Architecture (SDD v1.3) · .NET 10 · Next.js 15 · PostgreSQL 15 · MinIO · Gotenberg 8  
**อัปเดตล่าสุด:** 2026-09-17  
**สถานะ:** Ready for Execution  

---

## 📑 สารบัญขอบเขตการทดสอบ (Testing Scopes)

1. [Scope 1: Backend Code-Level Unit Tests (.NET 10 xUnit)](#scope-1-backend-code-level-unit-tests-net-10-xunit)
2. [Scope 2: Frontend Code-Level Unit Tests (Next.js 15 & Vitest)](#scope-2-frontend-code-level-unit-tests-nextjs-15--vitest)
3. [Scope 3: Infrastructure & Container Health Check (Docker V2)](#scope-3-infrastructure--container-health-check-docker-v2)
4. [Scope 4: Live API & Integration Testing (HTTP REST Endpoints)](#scope-4-live-api--integration-testing-http-rest-endpoints)
5. [Scope 5: End-to-End User Journeys & UI Verification (Portal V2)](#scope-5-end-to-end-user-journeys--ui-verification-portal-v2)
6. [เกณฑ์การประเมินและการส่งมอบ (Pass / Fail Criteria)](#เกณฑ์การประเมินและการส่งมอบ-pass--fail-criteria)

---

## Scope 1: Backend Code-Level Unit Tests (.NET 10 xUnit)

**โปรเจกต์:** [`backend-v2/tests/SmkDoc.Tests/SmkDoc.Tests.csproj`](../../backend-v2/tests/SmkDoc.Tests/SmkDoc.Tests.csproj)  
**คำสั่งรัน:** `dotnet test backend-v2/tests/SmkDoc.Tests/SmkDoc.Tests.csproj`

### 1.1 Core Template Engines
- [ ] **DOCX Engine (`DocxTemplateEngineTests.cs`)**
  - [ ] แทนที่ Text Placeholders ทั่วไปใน Word Runs
  - [ ] Dynamic Table Expansion ขยายแถวตารางตาม array ข้อมูล
  - [ ] DrawingML Media Injection กำหนดรูปภาพโดยบังคับ `Id > 0` ป้องกัน Word Desktop Crash
  - [ ] Nested Placeholders และ Object traversal
- [ ] **Excel Engine (`ExcelTemplateEngineTests.cs`)**
  - [ ] แทนที่ Text Placeholders ใน Cell
  - [ ] Dynamic Table Expansion แนวนอนและแนวตั้ง
  - [ ] การคำนวณและคงค่าสูตร Excel (Formulas calculation)
  - [ ] Number Formatting, Date Formatting และ Currency Styling
  - [ ] Media Injection สำหรับรูปภาพและโลโก้ในชีต
- [ ] **HTML Engine (`HtmlTemplateEngineTests.cs`)**
  - [ ] Gotenberg Chromium PDF Rendering pipeline
  - [ ] การตัดคำภาษาไทย (Thai Word-breaking) ด้วยฟอนต์ `Sarabun`

### 1.2 Utilities, Helpers & Security
- [ ] **Math Expression Resolver (`MathExpressionResolverTests.cs`)**
  - [ ] การบวก ลบ คูณ หาร และลำดับความสำคัญเครื่องหมาย (`*` ก่อน `+`)
  - [ ] คำนวณ VAT 7%, Invoice Subtotal, และ GrandTotal
  - [ ] การประเมินเงื่อนไข `IIF(condition, trueVal, falseVal)`
  - [ ] การ Fallback ปลอดภัยเมื่อหารด้วยศูนย์ หรือตัวแปรไม่เป็นตัวเลข
- [ ] **Thai Data Transformer (`ThaiDataTransformerTests.cs`)**
  - [ ] แปลงตัวเลขเป็นบาทไทยตัวหนังสือ (`thai_baht_text`) ถูกต้องตามหลักไวยากรณ์การเงิน
  - [ ] จัดรูปแบบวันที่ พ.ศ. (`thai_date`, `thai_date_short`)
  - [ ] จัดรูปแบบตัวเลขทศนิยม 2 ตำแหน่งพร้อมเครื่องหมายจุลภาค
- [ ] **Word Security Scanner (`DocxSecurityScannerServiceTests.cs`)**
  - [ ] ตรวจจับและปฏิเสธไฟล์ที่มี Macro (.vba)
  - [ ] ตรวจจับ External Links / OLE Objects ที่อันตราย
  - [ ] ตรวจจับ Remote DDE Injection
- [ ] **Data Protection AES (`DataProtectionServiceTests.cs`)**
  - [ ] Round-trip Encrypt & Decrypt ได้ข้อความต้นฉบับตรง 100%
  - [ ] ป้องกัน Crash เมื่อรับข้อความว่างหรือ null
  - [ ] รองรับ Fallback key เมื่อ config ขาดหาย
- [ ] **Barcode & QR Code Services (`BarcodeQrCodeServiceTests.cs`)**
  - [ ] `BarcodeService`: สร้างภาพ Code 128 เป็น PNG Bytes พร้อม Magic Header ถูกต้อง
  - [ ] `QrCodeService`: สร้างภาพ QR Code PNG Bytes ถูกต้อง
- [ ] **Image Optimizer & Placeholder Helpers (`ImageOptimizerServiceTests.cs`, `PlaceholderHelperTests.cs`, `StorageBucketsTests.cs`)**
  - [ ] ปรับขนาดและบีบอัดรูปภาพก่อนแนบลงเอกสาร
  - [ ] Regex Pattern Match Placeholders `{{placeholder}}` และ `<Field>`

### 1.3 Document Generation Use Cases
- [ ] **Generate Document Pipeline (`GenerateDocumentUseCaseTests.cs`)**
  - [ ] ตรวจสอบ Template ID / Slug ถูกต้อง
  - [ ] Merge ข้อมูลเข้ากับ Engine ตาม `RenderEngineType`
  - [ ] บันทึกไฟล์ลง MinIO S3 bucket `outputs`
  - [ ] ออก Presigned S3 Download URL อายุ 24 ชั่วโมง
  - [ ] บันทึก Document Version และ Generation Log ครบถ้วน
- [ ] **Live Preview Pipeline (`PreviewDocumentUseCaseTests.cs`)**
  - [ ] ส่งคืน PDF Stream ไบต์โดยตรง (`application/pdf`)
  - [ ] **Zero Side-Effects Check:** ต้องไม่อัปโหลด MinIO, ไม่เพิ่ม Version, ไม่เขียน Generation Log
- [ ] **Stateless Rendering (`RenderStatelessDocumentUseCaseTests.cs`)**
  - [ ] แปลงไฟล์ Stream ชั่วคราวเป็น PDF โดยไม่แตะ Database หรือ Storage
- [ ] **Document Versioning (`DocumentVersionUseCaseTests.cs`)**
  - [ ] บันทึก Legal Audit Trail (DocumentRef, Version, SHA-256 Hash, Input Payload Snapshot)
  - [ ] ออก Presigned URL สำหรับดาวน์โหลดเอกสารย้อนหลัง

### 1.4 Templates & Datasources Use Cases
- [ ] **Template Management (`TemplateManagementUseCaseTests.cs`, `TemplateValidateUseCaseTests.cs`)**
  - [ ] CRUD Templates และการตรวจสอบ Slug ห้ามซ้ำ
  - [ ] Auto-increment Version เมื่อมีการอัปเดต Template Content
  - [ ] บันทึกและดึง HTML Template สำหรับ Monaco Editor
  - [ ] Soft Delete / Deactivate Template
- [ ] **Field Mappings (`FieldMappingUseCaseTests.cs`, `PreviewMappingUseCaseTests.cs`)**
  - [ ] บันทึกและดึงรายการ Field Mappings
  - [ ] จำลอง Preview ข้อมูลตาม Mappings และ Default Value
- [ ] **Data Connections (`DataConnectionUseCaseTests.cs`)**
  - [ ] ดึงรายการโดยซ่อน Connection String (Encrypted at rest)
  - [ ] สร้างและอัปเดตพร้อม Encrypt รหัสผ่านด้วย AES
  - [ ] ทดสอบ Connectivity ด้วยคำสั่ง `SELECT 1`
- [ ] **Datasets & Template Linkage (`DatasetUseCaseTests.cs`, `TemplateDatasetUseCaseTests.cs`)**
  - [ ] CRUD Dataset และการผูก Data Connection
  - [ ] ผูก Dataset หลายตัวเข้ากับ Template เดียวกัน
  - [ ] ตรวจสอบความซ้ำซ้อนของ Dataset Alias ใน Template เดียวกัน (ห้ามซ้ำ)

---

## Scope 2: Frontend Code-Level Unit Tests (Next.js 15 & Vitest)

**โปรเจกต์:** [`frontend-v2`](../../frontend-v2)  
**คำสั่งรัน:** `npm run test` และ `npm run lint`

### 2.1 Design Tokens & Thai Transforms
- [ ] **Design Tokens (`tokens.test.ts`)**
  - [ ] SSoT Token Integrity: ตรวจสอบค่าสี Accent ตามหมวดหมู่ (Contracts: Indigo, Financial: Emerald, Official: Sky Blue, HR: Amber, Operations: Cyan)
  - [ ] Geometry Rules: ตรวจสอบ Border Radius อยู่ในกรอบ 2px - 4px (ห้ามมี Circular Bubble)
  - [ ] No Dark Navy Check: พื้นหลัง Base Canvas ต้องเป็น Ice-White (`#f8fbfe` / `#f0f7ff`)
- [ ] **Thai Transforms (`thaiTransforms.test.ts`)**
  - [ ] การแปลงค่าเงินบาทไทยตัวหนังสือสำหรับหน้า UI
  - [ ] การแปลงปี พ.ศ. และรูปแบบตัวเลขในตาราง

### 2.2 Zod Runtime Schemas
- [ ] **Core API Schemas (`schemas.test.ts`)**
  - [ ] `GenerateDocumentRequestSchema`: ตรวจสอบ Output format รองรับเฉพาะ `pdf`, `docx`, `xlsx`
  - [ ] `CreateTemplateRequestSchema`: บังคับรูปแบบ Slug (lowercase, hyphen, no spaces)
  - [ ] `SaveFieldMappingItemSchema`: ตรวจสอบ Placeholder, SourcePath, Transform
  - [ ] `CreateApiKeyRequestSchema`: ตรวจสอบความยาวชื่อและ Caller App
- [ ] **Datasources & Datasets Schemas (`datasourcesSchemas.test.ts`)**
  - [ ] `dataConnectionSchema` และ `createDataConnectionSchema`
  - [ ] `DatasetSchema` และ `CreateDatasetSchema` (UUID validation & SQL Query required)
  - [ ] `TemplateDatasetDtoSchema` และ `SaveTemplateDatasetItemSchema` (Alias length & SortOrder)

---

## Scope 3: Infrastructure & Container Health Check (Docker V2)

**คำสั่งรัน:** `docker compose -p smk-v2 -f docker-compose.v2.yml up -d`  
**ตรวจสอบสถานะ:** `docker compose -p smk-v2 ps`

| บริการ | Container Name | Port ที่ต้องเปิด | Endpoint ตรวจสอบ | ผลคาดหวัง |
|---|---|:---:|---|---|
| **PostgreSQL 15** | `smk-postgres-v2` | `5433:5432` | `pg_isready -U postgres -d smkdoc` | `accepting connections` |
| **MinIO S3** | `smk-minio-v2` | `9000`, `9001` | `http://localhost:9000/minio/health/live` | `200 OK` (Buckets: `templates`, `outputs`) |
| **Gotenberg 8** | `smk-gotenberg-v2` | `3000` | `http://localhost:3000/health` | `200 OK` (Chromium + LibreOffice) |
| **Backend API** | `smk-doc-server-v2` | `8080` | `http://localhost:8080/health` | `200 OK` (`status: healthy`) |
| **Frontend Portal** | `smk-doc-portal-v2` | `3001:3000` | `http://localhost:3001` | `200 OK` (Next.js App Loaded) |

---

## Scope 4: Live API & Integration Testing (HTTP REST Endpoints)

**Base URL:** `http://localhost:8080`  
**Authentication Header:** `X-API-Key: secret-smk-key-2026`

### 4.1 Authentication & System Security
- [ ] **SEC-01:** เรียก API โดยไม่มี `X-API-Key` -> ได้รับ `401 Unauthorized`
- [ ] **SEC-02:** เรียก API ด้วย API Key ที่ไม่ถูกต้อง -> ได้รับ `401 Unauthorized`
- [ ] **SEC-03:** เรียก API ด้วย Master API Key ที่ถูกต้อง -> ได้รับ `200 OK`
- [ ] **SEC-04:** Health Check `GET /health` -> เข้าถึงได้โดยไม่ต้องส่ง API Key (`200 OK`)

### 4.2 Template Lifecycle API
- [ ] **TPL-01:** `POST /api/templates` (สร้าง Template พร้อมอัปโหลดไฟล์ HTML/DOCX/XLSX) -> `201 Created`
- [ ] **TPL-02:** `GET /api/templates` -> คืนรายการ Templates ทั้งหมด
- [ ] **TPL-03:** `GET /api/templates/:id/html` -> ดึง HTML สำหรับแสดงผลใน Monaco Editor
- [ ] **TPL-04:** `PUT /api/templates/:id/html` -> บันทึกการแก้ไข HTML (Version เพิ่มขึ้น +1 อัตโนมัติ)
- [ ] **TPL-05:** `POST /api/templates/:id/validate` -> ตรวจสอบ Syntax และ Placeholders
- [ ] **TPL-06:** `DELETE /api/templates/:id` -> Soft Delete / Deactivate Template สำเร็จ

### 4.3 Field Mappings & Datasets API
- [ ] **MAP-01:** `GET /api/templates/:id/mappings` -> คืนรายการ Field Mappings
- [ ] **MAP-02:** `PUT /api/templates/:id/mappings` -> บันทึกการ Map ฟิลด์
- [ ] **DS-01:** `POST /api/dataconnections` -> บันทึก Connection ใหม่ (คืนค่าโดยซ่อน Connection String)
- [ ] **DS-02:** `POST /api/dataconnections/:id/test` -> ทดสอบเชื่อมต่อ Database สำเร็จ
- [ ] **DS-03:** `POST /api/templates/:id/datasets` -> ผูก Datasets เข้ากับ Template

### 4.4 Document Generation & Preview API
- [ ] **DOC-01 (Generate):** `POST /api/documents/generate/:slug`
  - ส่ง Payload JSON + Output Format (`pdf`)
  - คืนค่า Presigned URL อายุ 24 ชม., `generationId`, และ `expiresAt`
  - ตรวจสอบว่าสามารถดาวน์โหลดไฟล์จริงจาก URL ได้
- [ ] **DOC-02 (Live Preview):** `POST /api/documents/preview/:slug`
  - ส่ง Payload JSON + HTML ชั่วคราวจาก Editor
  - ได้รับ PDF Stream ไบต์ (`Content-Type: application/pdf; Content-Disposition: inline`) ทันที
  - ตรวจสอบใน MinIO และ Database ว่าไม่มีการสร้างไฟล์หรือ Log ค้างทิ้งไว้ (Zero Side-Effects)
- [ ] **DOC-03 (Version History):** `GET /api/documents/:ref/versions` -> คืนประวัติทุก Version ของเอกสาร
- [ ] **DOC-04 (Version Download):** `GET /api/documents/:ref/versions/:v/download` -> ออก Presigned URL โหลดไฟล์ Version ย้อนหลัง

---

## Scope 5: End-to-End User Journeys & UI Verification (Portal V2)

**URL:** `http://localhost:3001`  
**Design Standard:** ขาวอมฟ้า (Ice-White), Lucide Icons, Compact Density, Border Radius 2px - 4px

### 5.1 การจัดวางและโครงสร้างหน้าจอ (Layout & Navigation)
- [ ] **Topbar:**
  - Toggle Sidebar ย่อ/ขยายได้สมบูรณ์
  - ช่องกรอก Master API Key แบบ Monospace Font ที่มุมขวาบน
  - Navigation History (Back / Forward / Home)
- [ ] **Sidebar Grouping:**
  - กลุ่ม **เอกสาร:** แม่แบบทั้งหมด (`/templates`), สร้างเอกสาร (`/generator`), ประวัติการสร้าง (`/audit`)
  - กลุ่ม **จัดการ:** Template Editor v2 (`/studio`), อัปโหลด Template (`/upload`), Version History (`/history`)
  - กลุ่ม **ผู้ดูแลระบบ:** Datasources v2 (`/datasources`), API Keys (`/projects`), จัดการผู้ใช้ (`/users`), ตั้งค่า (`/settings`)
- [ ] **Card UI Structure:**
  - ปุ่ม Action หลัก (เช่น "สร้างเอกสาร") ชิดขวาล่างเสมอ
  - เมนู Context Dropdown (`⋮`) อยู่มุมซ้ายล่าง ป้องกัน Dropdown หลุดขอบจอ

### 5.2 ผู้ใช้สร้างและแก้ไข Template ใน Studio (`/studio`)
- [ ] เปิดหน้า Studio โหลดเนื้อหา Monaco Editor รวดเร็ว
- [ ] พิมพ์ `<Field>` Tag หรือเลือกแทรกจากแผงเครื่องมือด้านข้าง
- [ ] กด "Live Preview" แสดงผล PDF ฝั่งขวาทันทีโดยไม่ต้องบันทึก
- [ ] กด "บันทึกแม่แบบ" แจ้งเตือน Toast สีเขียว และ Version เพิ่มขึ้น +1

### 5.3 ผู้ใช้สั่งสร้างเอกสารผ่าน Generator (`/generator`)
- [ ] เลือก Template จาก Dropdown / Search
- [ ] กรอก JSON Payload ใน CodeBlock
- [ ] เลือก Format: PDF / Word / Excel
- [ ] กดปุ่ม "สร้างเอกสาร" (Bottom-Right) -> แสดง Loading -> ได้รับลิงก์ดาวน์โหลดพร้อมปุ่มคัดลอก

### 5.4 ผู้ใช้ตั้งค่า Datasource & Dataset (`/datasources`)
- [ ] สร้างการเชื่อมต่อใหม่ (PostgreSQL / SQL Server)
- [ ] กด "ทดสอบการเชื่อมต่อ" แสดงสถานะเชื่อมต่อสำเร็จ
- [ ] สร้าง Dataset พร้อมกำหนด SQL Query
- [ ] ผูก Dataset เข้ากับ Template ที่เลือก พร้อมตั้งชื่อ Alias

---

## เกณฑ์การประเมินและการส่งมอบ (Pass / Fail Criteria)

| ระดับการทดสอบ | เกณฑ์การผ่าน (Pass Criteria) | สถานะ |
|---|---|:---:|
| **Backend Unit Tests** | ผ่าน 100% (ทุก Test Suite ใน SmkDoc.Tests ต้องเป็นสีเขียว 0 Error) | [ ] |
| **Frontend Tests** | ผ่าน 100% (Vitest All Suites Pass, Zero Lint Warnings) | [ ] |
| **Docker Containers** | 5/5 Containers สถานะ Healthy ไม่มี Restart Loop | [ ] |
| **Security & Auth** | ป้องกัน Request ที่ไม่มี Key ได้ 100%, รหัสผ่านถูกเข้ารหัสใน DB | [ ] |
| **Document Pipeline** | เอกสาร PDF/DOCX/XLSX เรนเดอร์ภาษาไทยถูกต้อง ไม่ตกขอบ สระไม่ลอย | [ ] |
| **Preview Zero-Effects**| Preview Stream PDF ตรง โดยไม่มีขยะไฟล์ใน MinIO หรือ Version เพิ่ม | [ ] |

---

> **หมายเหตุ:** เมื่อดำเนินการรันการทดสอบครบทุก Scope ให้บันทึกผลการทดสอบลงใน [`docs/tests/test-run-results.md`](../../docs/tests/test-run-results.md) เพื่อเป็นหลักฐานประกอบการตรวจรับงาน
