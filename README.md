# SMK Document Server v2

Enterprise document generation microservice — รับ JSON payload และสร้างเอกสาร PDF / DOCX / XLSX จาก template สำหรับระบบ Sammakorn

---

## สารบัญ

- [ภาพรวม](#ภาพรวม)
- [Stack](#stack)
- [Architecture](#architecture)
- [Services (Docker)](#services-docker)
- [เริ่มต้นใช้งาน](#เริ่มต้นใช้งาน)
- [Environment Variables](#environment-variables)
- [API Reference](#api-reference)
- [Template System](#template-system)
- [Field Mapping](#field-mapping)
- [Thai Transforms](#thai-transforms)
- [Authentication](#authentication)
- [การ Deploy](#การ-deploy)
- [Database Migrations](#database-migrations)
- [Frontend Portal](#frontend-portal)
- [Testing](#testing)
- [Project Structure](#project-structure)

---

## ภาพรวม

SMK Document Server v2 ทำหน้าที่เป็น **document generation engine** ที่แยกออกจาก business application — ระบบหลักส่ง JSON มา, server แปลงเป็นเอกสารและส่ง URL ดาวน์โหลดกลับ

```
Business App  →  POST /api/documents/generate/{slug}  →  PDF / DOCX / XLSX
              ←  { url: "https://...", expiresAt: "..." }  ←
```

**ความสามารถหลัก:**

- สร้างเอกสาร 3 format: PDF (Chromium), DOCX (OpenXML), XLSX (ClosedXML)
- Template versioning — save ทุกครั้งสร้าง version ใหม่, rollback ได้
- Field Mapping — map JSON path / SQL query → placeholder ในเอกสาร
- Thai data transforms — วันที่ไทย, จำนวนเงินเป็นตัวอักษร, เลขบัตร, เบอร์โทร
- QR code / Barcode inline ใน template ผ่าน `{{qr:key}}` และ `{{barcode:key}}`
- Audit trail — log ทุก generation พร้อม document versioning
- Portal (Next.js) สำหรับ admin จัดการ template, mapping, และดู log
- **Multi-tenancy** — Companies → Projects → API Keys / Templates แยกต่อ project
- **JWT Portal Auth** — User login + RBAC roles (Admin/Editor/Viewer) ต่อ project
- **JSON Schema inference** — infer schema จาก placeholder แล้ว validate payload ก่อน generate
- **HTML Studio** — Monaco Editor + Handlebars helper autocomplete + live preview

---

## Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend Runtime | ASP.NET Core | .NET 10 |
| Language | C# 13 + Nullable refs | — |
| ORM | EF Core + Npgsql | 10.0.4 |
| Database | PostgreSQL | 15-alpine |
| HTML Engine | Handlebars.Net | 2.4.3 |
| DOCX Engine | DocumentFormat.OpenXml | 3.2.0 |
| XLSX Engine | ClosedXML | 0.104.2 |
| PDF Renderer | Gotenberg 8 (Chromium + LibreOffice) | 8 |
| Object Storage | MinIO (S3-compatible) | latest |
| Image/QR | SkiaSharp + QRCoder + ZXing | — |
| Frontend | Next.js 15 + React 19 | — |
| Styling | Tailwind CSS 3 | 3.4.17 |
| Editor | Monaco Editor | 4.7.0 |
| Validation | Zod | 3.24.2 |
| Auth (API) | API Key (SHA-256 hash) | machine-to-machine, scoped to Project |
| Auth (Portal) | JWT Bearer | user login, RBAC roles (Admin/Editor/Viewer) |

---

## Architecture

Clean Architecture แบ่ง 4 layers — dependency ไหลเข้าหา Domain เสมอ:

```
┌─────────────────────────────────────────┐
│  SmkDoc.Api                             │  HTTP Controllers, Middleware, Program.cs
│  (Controllers, Middleware, HealthChecks)│
└────────────────┬────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────┐
│  SmkDoc.Application                     │  Use Cases, Interfaces, DTOs, Helpers
│  (UseCases, Interfaces, Models)         │
└────────────────┬────────────────────────┘
                 │ implements
┌────────────────▼────────────────────────┐
│  SmkDoc.Infrastructure                  │  EF Core, MinIO, Gotenberg, Engines
│  (Persistence, Storage, Engines, Pdf)   │
└─────────────────────────────────────────┘
                 │ domain model
┌────────────────▼────────────────────────┐
│  SmkDoc.Domain                          │  Entities, Enums — ไม่มี dependency ภายนอก
│  (Entities, Enums)                      │
└─────────────────────────────────────────┘
```

**กฎหลัก**: Application ห้าม reference Infrastructure โดยตรง — ทุก dependency ผ่าน Interface

---

## Services (Docker)

| Service | Container | Port | Image |
|---------|-----------|------|-------|
| PostgreSQL | db | 5433 | postgres:15-alpine |
| MinIO | minio | 9000 (API), 9001 (Console) | minio/minio |
| Gotenberg | gotenberg | 3000 | gotenberg/gotenberg:8 |
| Backend API | doc-server-v2 | 8080 | ./backend-v2 (Dockerfile) |
| Frontend Portal | portal-v2 | 3001 | ./frontend-v2 (Dockerfile) |

**Startup order**: `db` + `minio` + `gotenberg` → `doc-server-v2` → `portal-v2`  
แต่ละ service ต้อง healthy ก่อน service ถัดไปจะ start

---

## เริ่มต้นใช้งาน

### ข้อกำหนด

- Docker Desktop (Windows/Mac) หรือ Docker Engine (Linux)
- Git

### Local Development (Docker)

```bash
# 1. Clone repo
git clone <repo-url>
cd smk-doc-server

# 2. ตั้งค่า environment (แก้ไขตามต้องการ)
cp .env.example .env   # หรือใช้ .env ที่มีอยู่

# 3. Build + start ทุก service
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build

# 4. ดู log
docker compose -f docker-compose.v2.yml -p smk-v2 logs -f

# 5. เข้าใช้งาน
# Portal:  http://localhost:3001
# API:     http://localhost:8080
# Swagger: http://localhost:8080/swagger  (Development mode)
# MinIO:   http://localhost:9001
```

### Local Development (Native — ไม่ใช้ Docker)

**ข้อกำหนดเพิ่มเติม**: .NET 10 SDK, Node.js 20+, PostgreSQL 15, MinIO, Gotenberg

```bash
# Backend
cd backend-v2
dotnet run --project src/SmkDoc.Api/SmkDoc.Api.csproj
# → http://localhost:8080

# Frontend
cd frontend-v2
npm install
npm run dev
# → http://localhost:3002
```

---

## Environment Variables

### Root `.env` (docker-compose)

| Variable | Default | คำอธิบาย |
|----------|---------|---------|
| `MASTER_API_KEY` | `secret-smk-key-2026` | API Key เริ่มต้น — **เปลี่ยนใน production** |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Development` เปิด Swagger, `Production` ปิด |
| `POSTGRES_USER` | `postgres` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `password` | PostgreSQL password |
| `POSTGRES_DB` | `smkdoc` | ชื่อ database |
| `MINIO_ROOT_USER` | `admin` | MinIO root user |
| `MINIO_ROOT_PASSWORD` | `password` | MinIO root password |
| `MINIO_PUBLIC_ENDPOINT` | `http://localhost:9000` | URL ที่ browser ใช้ดาวน์โหลด — **ต้องตั้งค่าใน production** |
| `DOC_SERVER_PORT` | `8080` | port ของ backend |
| `PORTAL_PORT` | `3500` | port ของ frontend portal |
| `NEXT_PUBLIC_API_URL` | `http://127.0.0.1:8080` | URL ที่ browser เรียก backend — **ต้องตั้งค่าใน production** |
| `JWT_SECRET` | `dev-smk-jwt-secret-2026-...` | JWT signing secret — **ต้องเปลี่ยนใน production (≥32 chars)** |
| `ADMIN_EMAIL` | `admin@sammakorn.co.th` | Email ของ admin user เริ่มต้น (seed ตอน startup) |
| `ADMIN_PASSWORD` | `Admin@2026!` | Password ของ admin user เริ่มต้น — **ต้องเปลี่ยนใน production** |

### Backend (`appsettings.json`)

ค่า default สำหรับ local dev — ใน production ทุกค่าถูก override โดย env vars:

```json
{
  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5433;..." },
  "Minio": { "Endpoint": "localhost:9000", "AccessKey": "admin", "SecretKey": "password" },
  "Security": { "ApiKey": "dev-smk-key-2026" },
  "GotenbergUrl": "http://localhost:3000",
  "Jwt": { "Secret": "dev-smk-jwt-secret-2026-must-be-at-least-32-chars", "Issuer": "smk-doc-server", "Audience": "smk-portal", "ExpiryHours": 8 },
  "Admin": { "Email": "admin@sammakorn.co.th", "Password": "Admin@2026!" }
}
```

---

## API Reference

### Authentication

Machine-to-machine: ส่ง header (ยกเว้น public routes):

```
X-API-Key: <your-api-key>
```

Portal users: login ผ่าน `POST /api/auth/login` → ได้รับ JWT token

### Document Generation

#### `POST /api/documents/generate/{slug}`

สร้างเอกสารจาก template และ JSON data

**Request:**
```json
{
  "data": {
    "customerName": "บริษัท ABC จำกัด",
    "amount": 250000,
    "items": [
      { "name": "สินค้า A", "qty": 2, "price": 125000 }
    ]
  },
  "output": "pdf",
  "documentRef": "INV-2024-001",
  "changeNote": "Initial generation"
}
```

| Field | Type | Required | คำอธิบาย |
|-------|------|----------|---------|
| `data` | object | ✓ | JSON data สำหรับ placeholder |
| `output` | `pdf\|docx\|xlsx` | ✓ | format ที่ต้องการ |
| `documentRef` | string | — | business key สำหรับ audit trail |
| `changeNote` | string | — | หมายเหตุ version นี้ |

**Response:**
```json
{
  "url": "https://minio.domain.com/outputs/2024/01/15/invoice-abc123.pdf?X-Amz-...",
  "expiresAt": "2024-01-16T10:30:00Z",
  "generationId": "550e8400-e29b-41d4-a716-446655440000",
  "outputFormat": "pdf"
}
```

#### `POST /api/documents/preview/{slug}`

Preview เอกสาร — ไม่บันทึก MinIO, ไม่ write log  
ใช้สำหรับ live preview ใน Portal และ testing

```json
{
  "data": { "customerName": "Test" },
  "html": "<html>...</html>"
}
```

> `html` field ใช้ส่ง HTML โดยตรงจาก Monaco editor — ถ้าไม่ส่งจะใช้ template ใน storage

#### `POST /api/documents/render/stateless`

Render ไฟล์ที่อัปโหลดโดยตรง — ไม่เชื่อม template ในระบบ

```bash
curl -X POST http://localhost:8080/api/documents/render/stateless \
  -H "X-API-Key: secret-smk-key-2026" \
  -F "file=@template.html" \
  -F 'jsonData={"name":"สมชาย"}'
```

### Document Versioning

```
GET  /api/documents/{documentRef}/versions
GET  /api/documents/{documentRef}/versions/{version}/download
GET  /api/documents/download/{logId}
```

### Template Management

```
GET    /api/templates                           # List all templates
POST   /api/templates [FormData]                # Create (upload file หรือ blank HTML)
GET    /api/templates/{id}/html                 # Get HTML source
PUT    /api/templates/{id}/html                 # Save HTML (creates new version)
PUT    /api/templates/{id}                      # Update metadata
DELETE /api/templates/{id}                      # Deactivate
GET    /api/templates/{id}/versions             # Version history
POST   /api/templates/{id}/rollback/{version}   # Rollback to version
GET    /api/templates/{id}/download             # Download template file
POST   /api/templates/{id}/validate             # Validate HTML + dry-run render
GET    /api/templates/{id}/scan-fields          # Scan {{placeholders}} จาก stored file
POST   /api/templates/scan-fields [FormData]    # Scan {{placeholders}} จาก uploaded file
```

### Field Mappings

```
GET  /api/templates/{id}/mappings          # Get mapping configuration
PUT  /api/templates/{id}/mappings          # Save mappings (replace all)
POST /api/templates/{id}/mappings/preview  # Preview result with sample data → PDF
```

### Data Sources

```
GET/POST/PUT/DELETE  /api/data-connections
POST                 /api/data-connections/test   # Test connection (no persist)
GET/POST/PUT/DELETE  /api/datasets
GET/PUT              /api/templates/{id}/datasets # Template-dataset assignments
```

### API Keys

```
GET    /api/api-keys          # List keys (hash ปกปิด)
POST   /api/api-keys          # Create — response แสดง plaintext ครั้งเดียว
DELETE /api/api-keys/{id}     # Revoke (soft delete)
```

### User Management

ต้องใช้ทั้ง `X-API-Key` และ `Authorization: Bearer <jwt>` — Admin role เท่านั้น

```
GET    /api/users                    # รายชื่อสมาชิกใน project
POST   /api/users                    # Invite user ใหม่ (สร้าง account หรือเพิ่มใน project)
PUT    /api/users/{id}/role          # เปลี่ยน role
DELETE /api/users/{id}               # Remove ออกจาก project
PATCH  /api/users/{id}/status        # Suspend / เปิดใช้งาน account
```

### Audit Logs

```
GET /api/logs?page=1&limit=50&app=callerApp   # Paginated generation log
```

### Health Check

```
GET /health
```

Response:
```json
{
  "status": "healthy",
  "checks": [
    { "name": "postgres",   "status": "healthy", "durationMs": 3 },
    { "name": "gotenberg",  "status": "healthy", "durationMs": 12 },
    { "name": "minio",      "status": "healthy", "durationMs": 5 }
  ]
}
```

---

## Template System

### HTML Template (Handlebars)

Template ใช้ Handlebars syntax พร้อม custom helpers:

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>ใบแจ้งหนี้</title>
</head>
<body>
  <h1>{{companyName}}</h1>
  <p>วันที่: {{invoiceDate:thaidate}}</p>
  <p>จำนวนเงิน: {{totalAmount:baht}} บาทถ้วน</p>
  <p>เบอร์โทร: {{phone:thaiphone}}</p>

  <!-- QR Code -->
  <img src="{{qr:documentRef}}">

  <!-- Barcode -->
  <img src="{{barcode:invoiceNo}}">

  <!-- Loop -->
  {{#each items}}
    <tr>
      <td>{{name}}</td>
      <td>{{qty}}</td>
      <td>{{price:currency}}</td>
    </tr>
  {{/each}}

  <!-- Conditional -->
  {{#if isPaid}}
    <span>ชำระแล้ว</span>
  {{/if}}
</body>
</html>

<!-- Header (แสดงทุกหน้า) -->
<template id="header">
  <div>{{companyName}}</div>
</template>

<!-- Footer (แสดงทุกหน้า) -->
<template id="footer">
  <div>หน้า <span class="pageNumber"></span></div>
</template>
```

### DOCX Template

ใช้ `{{placeholder}}` ภายใน Word document — รองรับ table expansion สำหรับ array:

```
ชื่อลูกค้า: {{customerName}}
วันที่: {{invoiceDate:thaidate}}
| {{items[].name}} | {{items[].qty}} | {{items[].price:currency}} |
```

### XLSX Template

ใช้ `{{placeholder}}` ใน cell — รองรับ row expansion สำหรับ array:

```
A1: {{title}}
A3: {{items[].name}}    B3: {{items[].qty}}    C3: {{items[].price}}
```

### Placeholder Types

| Syntax | คำอธิบาย |
|--------|---------|
| `{{key}}` | Text replacement |
| `{{key:transform}}` | Text + Thai transform |
| `{{qr:key}}` | QR Code PNG (inline) |
| `{{barcode:key}}` | Barcode PNG (inline) |
| `{{image:key}}` | Embed image (base64 URL) |
| `{{#each array}}...{{/each}}` | Loop (HTML only) |

---

## Field Mapping

Field Mapping กำหนดว่า placeholder แต่ละตัวดึงข้อมูลมาจากไหน — รองรับ 2 mode:

### JSON Mode (DataSourceType: json)

ดึงข้อมูลจาก request payload ด้วย dot-notation:

| Placeholder | SourcePath | ข้อมูลใน payload |
|------------|-----------|-----------------|
| `{{customerName}}` | `customer.name` | `{ "customer": { "name": "สมชาย" } }` |
| `{{totalAmount}}` | `invoice.total` | `{ "invoice": { "total": 250000 } }` |
| `{{itemName}}` | `items[0].name` | `{ "items": [{ "name": "สินค้า A" }] }` |

### SQL Mode (DataSourceType: sql)

ดึงข้อมูลจาก database ผ่าน Dataset + DatasetAlias:

```
DatasetAlias: "customer"  →  Dataset: SELECT * FROM customers WHERE id = {{customerId}}
ResultPath:   "name"       →  ดึง field "name" จาก query result
```

**ขั้นตอนตั้งค่า SQL mode:**
1. สร้าง DataConnection (Settings → Data Connections)
2. สร้าง Dataset พร้อม SQL query
3. กำหนด TemplateDataset alias ใน template
4. FieldMapping ใช้ `DatasetAlias` + `ResultPath`

### Math Expression

สามารถใส่ math expression บน resolved value:

```
MathExpression: "{totalAmount} * 1.07"   → คำนวณ VAT 7%
MathExpression: "{qty} * {unitPrice}"    → คำนวณ subtotal
```

---

## Thai Transforms

ใส่ transform หลัง `:` ใน placeholder:

| Transform | Input | Output |
|-----------|-------|--------|
| `baht` / `thaibaht` | `250000` | `สองแสนห้าหมื่นบาทถ้วน` |
| `currency` | `250000` | `250,000.00` |
| `currency0` | `250000` | `250,000` |
| `thaidate` / `thai_date_full` | `2024-01-15` | `15 มกราคม 2567` |
| `thaidate_short` | `2024-01-15` | `15 ม.ค. 2567` |
| `thaidatetime` | `2024-01-15T14:30:00` | `15 มกราคม 2567 14:30 น.` |
| `phone` / `thaiphone` | `0812345678` | `081-234-5678` |
| `idcard` / `thaiid` | `1234567890123` | `1-2345-67890-12-3` |

---

## Authentication

ระบบใช้ **two-layer auth**:

### Layer 1 — API Key (machine-to-machine)

- ทุก request จากระบบภายนอกส่ง `X-API-Key: <key>` header
- Server hash ด้วย SHA-256 แล้วเทียบกับ `api_keys` table
- API Key scoped ต่อ **Project** — เปลี่ยน key ได้ต่อ project
- Rate limit: 60 request/นาที ต่อ API Key

```bash
curl -X POST http://localhost:8080/api/api-keys \
  -H "X-API-Key: secret-smk-key-2026" \
  -H "Content-Type: application/json" \
  -d '{"name": "Production System", "callerApp": "erp"}'
```

Response (แสดง plaintext ครั้งเดียว):
```json
{
  "id": "...",
  "plainTextKey": "smk_erp_550e8400-...",
  "name": "Production System"
}
```

**Master Key** สร้างอัตโนมัติจาก `MASTER_API_KEY` env var ตอน startup

### Layer 2 — JWT Bearer (portal users)

Portal ใช้ JWT สำหรับ user login — endpoint นี้เป็น **public** (ไม่ต้องมี API Key):

```
POST /api/auth/login
```

```json
{
  "email": "admin@sammakorn.co.th",
  "password": "Admin@2026!"
}
```

Response:
```json
{
  "token": "eyJhbGci...",
  "expiresAt": "2026-09-22T10:00:00Z",
  "user": { "email": "admin@sammakorn.co.th", "firstName": "Admin" }
}
```

- Password hashed ด้วย BCrypt
- Token เป็น JWT — ตรวจสอบด้วย `[Authorize]` attribute ใน protected endpoints
- RBAC roles: `Admin`, `Editor`, `Viewer` ต่อ Project (`user_project_roles` table)
- Default admin seed ตอน startup: `Admin:Email` / `Admin:Password` ใน `appsettings.json`

### Public routes (ไม่ต้อง auth ใดๆ)

| Route | เหตุผล |
|-------|--------|
| `GET /` | health ping |
| `GET /health` | health check |
| `GET /swagger*` | API docs (Development only) |
| `POST /api/documents/preview/{slug}` | stateless preview |
| `POST /api/auth/login` | เข้าสู่ระบบ Portal |

---

## การ Deploy

### Local Docker

```bash
# Start ทั้งหมด
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build

# Stop (เก็บ volume)
docker compose -f docker-compose.v2.yml -p smk-v2 down

# Stop + ลบ data (รัน migration ใหม่)
docker compose -f docker-compose.v2.yml -p smk-v2 down -v

# Rebuild เฉพาะ service
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build portal-v2
docker compose -f docker-compose.v2.yml -p smk-v2 up -d --build doc-server-v2

# ดู log
docker compose -f docker-compose.v2.yml -p smk-v2 logs -f doc-server-v2
```

### Production บน Coolify

Coolify ใช้ Traefik เป็น reverse proxy — ต้องตั้งค่า env vars เพิ่มใน Coolify dashboard:

```bash
# ต้องตั้งค่า (ไม่มี default ที่ใช้งาน production ได้)
NEXT_PUBLIC_API_URL=https://api.yourdomain.com       # URL backend ที่ browser เรียก
MINIO_PUBLIC_ENDPOINT=https://minio.yourdomain.com   # URL download file ที่ browser ใช้
MASTER_API_KEY=<random-strong-key-32-chars>          # ห้ามใช้ default
POSTGRES_PASSWORD=<strong-password>
MINIO_ROOT_PASSWORD=<strong-password>

# Optional
ASPNETCORE_ENVIRONMENT=Production    # ปิด Swagger (แนะนำ)
```

> **สำคัญ**: ต้อง set `NEXT_PUBLIC_API_URL` เป็น public domain ก่อน build portal — ค่านี้ถูก bake เข้า Next.js bundle ตอน build ไม่สามารถเปลี่ยน runtime ได้

---

## Database Migrations

ใช้ EF Core Code-First migrations — ทำงานอัตโนมัติตอน startup (`MigrateAsync`)

### Migration Files

| Migration | วันที่ | สิ่งที่ทำ |
|-----------|--------|---------|
| `AddDatasourceSupport` | 2026-09-16 | Schema เริ่มต้น: templates, template_versions, field_mappings, generation_logs, api_keys, data_connections |
| `AddDatasetLayer` | 2026-09-17 | เพิ่ม datasets table, ปรับ field_mappings |
| `WaveTwo_FullSchemaRebuild` | 2026-09-17 | Template versioning redesign, documents anchor entity, generation_logs เพิ่มฟิลด์, template_datasets |
| `AddAuthAndMultiTenancy` | 2026-09-18 | Multi-tenancy: companies, projects, users, user_project_roles; api_keys + templates scoped to Project; PL/pgSQL backfill |

### รัน Migration ด้วยตนเอง

```bash
cd backend-v2
dotnet ef database update --project src/SmkDoc.Infrastructure --startup-project src/SmkDoc.Api
```

### สร้าง Migration ใหม่

```bash
cd backend-v2
dotnet ef migrations add <MigrationName> \
  --project src/SmkDoc.Infrastructure \
  --startup-project src/SmkDoc.Api
```

> **สำคัญ**: อย่าแก้ไข entity โดยไม่สร้าง migration — EF จะ detect mismatch และ throw error

---

## Frontend Portal

Next.js 15 App Router — single-page admin portal พร้อม JWT auth + RBAC

### หน้าต่างๆ

| หน้า | Role ขั้นต่ำ | คำอธิบาย |
|------|------------|---------|
| **Login** | Public | หน้าเข้าสู่ระบบ — JWT token เก็บใน `localStorage` |
| **Templates** | Editor | List, create, delete template; copy slug |
| **Studio** | Editor | Monaco Editor แก้ HTML + live PDF preview + version history |
| **Upload** | Editor | Wizard: upload file → scan placeholders → สร้าง mappings อัตโนมัติ |
| **Mapping** | Editor | Drag-and-drop field mapping configuration |
| **Generator** | Viewer | ทดสอบ generate เอกสารพร้อม form input |
| **Audit** | Viewer | ค้นหา document history ด้วย reference |
| **Logs** | Admin | Generation log พร้อม filter |
| **API Keys** | Admin | จัดการ API keys |
| **Users** | Admin | จัดการสมาชิก + role ต่อ project (invite / change role / suspend / remove) |
| **Data Sources** | Admin | จัดการ Data Connections และ Datasets |
| **API Docs** | Viewer | Developer guide พร้อม code samples |
| **Settings** | Admin | Health check + ตั้งค่า API key |

### Authentication & RBAC

Portal ใช้ JWT auth ผ่าน `AuthContext` (React Context SSoT):

- **Login**: `POST /api/auth/login` → JWT เก็บใน `localStorage('smk_jwt')`
- **Auto-logout**: decode JWT `exp` claim → warning modal 60 วินาทีก่อนหมดอายุ → redirect login
- **Unauthorized (401)**: `apiClient` dispatch `auth:unauthorized` event → `AuthContext` logout อัตโนมัติ
- **RBAC**: `lib/rbac.ts` กำหนด `NAV_MIN_ROLE` ต่อ tab — Sidebar filter nav items ตาม role, `RequireRole` component guard view

| Role | สิทธิ์ |
|------|--------|
| **Admin** | เข้าถึงทุกหน้า รวม Logs, Settings, Data Sources, API Keys |
| **Editor** | Templates, Studio, Upload, Mapping + ทุกหน้าของ Viewer |
| **Viewer** | Generator, Audit, API Docs เท่านั้น |

### API Key ใน Portal

Portal เก็บ API key ใน `localStorage` (`smk_api_key`) — ตั้งค่าได้จาก Settings

Default key ถูก bake เข้า bundle จาก `NEXT_PUBLIC_DEFAULT_API_KEY` (= `MASTER_API_KEY` ตอน build)

---

## Testing

### Backend (xUnit)

```bash
# รัน test ทั้งหมด
cd backend-v2
dotnet test

# รันเฉพาะ class
dotnet test --filter "FullyQualifiedName~GenerateDocumentUseCaseTests"

# พร้อม coverage
dotnet test --collect:"XPlat Code Coverage"
```

Test suite ครอบคลุม Domain, Application Use Cases, Infrastructure Engines, Security และ Web APIs — 100% Passing (0 failures)

### Frontend (TypeScript)

```bash
cd frontend-v2
npx tsc --noEmit   # Type check — ต้อง 0 error ก่อน commit

npx vitest         # Unit tests
```

---

## Project Structure

```
smk-doc-server/
├── backend-v2/
│   ├── src/
│   │   ├── SmkDoc.Domain/
│   │   │   ├── Entities/          Template, TemplateVersion, Document, DocumentVersion,
│   │   │   │                      FieldMapping, GenerationLog, ApiKey,
│   │   │   │                      DataConnection, Dataset, TemplateDataset,
│   │   │   │                      User, Company, Project, UserProjectRole
│   │   │   └── Enums/             RenderEngineType, TemplateVersionStatus, TemplateFormat,
│   │   │                          RoleType (Admin/Editor/Viewer)
│   │   ├── SmkDoc.Application/
│   │   │   ├── Common/
│   │   │   │   ├── Interfaces/    IRepository, IUnitOfWork, IStorageService, IPdfRenderer,
│   │   │   │   │                  IExecutionContext, IRenderEngine, ITemplateScannerService,
│   │   │   │   │                  IFieldMappingApplicatorService,
│   │   │   │   │                  IPasswordHasher, IJwtTokenGenerator,
│   │   │   │   │                  IJsonDataParser, ISchemaInferenceService, IJsonSchemaValidationService
│   │   │   │   ├── Models/        DTOs (Request/Response, LoginRequest/Response)
│   │   │   │   └── Helpers/       PlaceholderHelper, ThaiDataTransformer,
│   │   │   │                      FieldMappingApplicatorService, MathExpressionResolverService
│   │   │   ├── Engines/           IRenderEngine (Strategy interface)
│   │   │   └── UseCases/
│   │   │       ├── Documents/     GenerateDocumentUseCase, PreviewDocumentUseCase,
│   │   │       │                  DocumentVersionUseCase, RenderStatelessDocumentUseCase
│   │   │       ├── Templates/     TemplateManagementUseCase, TemplateValidateUseCase,
│   │   │       │                  HtmlStudioUseCase, HtmlPersistenceUseCase
│   │   │       ├── FieldMappings/ FieldMappingUseCase, PreviewMappingUseCase, TemplateDatasetUseCase
│   │   │       ├── Security/      ApiKeyUseCase, LoginUseCase, UserManagementUseCase
│   │   │       ├── Datasets/      DatasetUseCase, DataConnectionUseCase
│   │   │       └── Validation/    ValidatePayloadUseCase
│   │   ├── SmkDoc.Infrastructure/
│   │   │   ├── Auth/              BcryptPasswordHasher, JwtTokenGenerator
│   │   │   ├── Engines/
│   │   │   │   ├── Html/          HtmlTemplateEngine (Handlebars + Gotenberg Chromium),
│   │   │   │   │                  HtmlHelperRegistry, HtmlLayoutProcessor, HtmlPlaceholderTransformer
│   │   │   │   ├── Word/          DocxTemplateEngine (OpenXML + Gotenberg LibreOffice)
│   │   │   │   └── Excel/         ExcelTemplateEngine (ClosedXML + Gotenberg LibreOffice),
│   │   │   │                      ExcelTableExpander, ExcelMediaInjector
│   │   │   ├── Imaging/           QrCodeService, BarcodeService, ImageOptimizerService
│   │   │   ├── Parsing/           JsonDataParser (SSoT for FlattenNamed)
│   │   │   ├── Pdf/               GotenbergPdfRenderer
│   │   │   ├── Persistence/       AppDbContext, EfRepository, UnitOfWork, Migrations/
│   │   │   ├── Schema/            SchemaInferenceService, JsonSchemaValidationService
│   │   │   ├── Security/          DataProtectionService, DocxSecurityScannerService
│   │   │   └── Storage/           MinioStorageService
│   │   └── SmkDoc.Api/
│   │       ├── Controllers/       DocumentController, TemplateController,
│   │       │                      ApiKeyController, AuditLogController,
│   │       │                      AuthController, UsersController,
│   │       │                      DataConnectionsController, DatasetController
│   │       ├── Middleware/        ApiKeyMiddleware, SecurityHeadersMiddleware
│   │       ├── HealthChecks/      GotenbergHealthCheck, MinioHealthCheck
│   │       └── Program.cs         (seed: Company → Project → AdminUser → ApiKey)
│   └── tests/SmkDoc.Tests/       Unit & Integration test suite (xUnit + Moq + FluentAssertions)
├── frontend-v2/
│   └── src/
│       ├── app/page.tsx           Router (switch activeTab)
│       ├── components/
│       │   ├── layout/            AppShell, Sidebar, Topbar
│       │   ├── ui/                Design system primitives
│       │   └── features/          View components (templates, studio, mapping, ...)
│       ├── lib/api/               API clients (client.ts, templates.api.ts, ...)
│       ├── schemas/               Zod schemas (SSoT)
│       ├── types/api.ts           Re-exports (ห้าม import schemas/ โดยตรง)
│       └── hooks/                 useTemplates, useDebounce, ...
├── docker-compose.v2.yml
├── .env                           Dev environment variables
└── README.md
```

---

## MinIO Storage Layout

```
templates/
  {slug}.html              ← active template (latest version)
  archive/{slug}_v{n}.html ← version archive

outputs/
  {yyyy/MM/dd}/{slug}_{generationId}.{pdf|docx|xlsx}
```

Pre-signed URL มีอายุ **24 ชั่วโมง** สำหรับ generate, **1 ชั่วโมง** สำหรับ re-download จาก log

---

## Rate Limiting

Fixed window per API Key — 60 request/minute

Response เมื่อ limit เกิน:
```json
{ "error": "TooManyRequests", "message": "Rate limit exceeded. Maximum 60 requests per minute per API key." }
```

---

## Troubleshooting

### Portal แสดง empty / ไม่มีข้อมูล

API Key ไม่ถูกตั้งค่า → เปิด Settings หรือ Topbar input แล้วใส่ key

### download URL ใช้ไม่ได้ใน production

`MINIO_PUBLIC_ENDPOINT` ยังเป็น `localhost` → ตั้งค่าเป็น public URL ของ MinIO

### Backend ไม่ start (Coolify)

`UseHttpsRedirection` redirect loop กับ Traefik → ระบบแก้ไขแล้วด้วย `ForwardedHeaders`

### DB migration fail

```bash
# ตรวจสอบ migration history
docker exec smk-doc-server-v2 dotnet ef migrations list

# รัน migration ด้วยตนเอง (ถ้า auto-migrate ไม่ทำงาน)
docker exec smk-doc-server-v2 dotnet ef database update
```

### Gotenberg ไม่ตอบ

```bash
docker compose -f docker-compose.v2.yml -p smk-v2 logs gotenberg
curl http://localhost:3000/health
```
