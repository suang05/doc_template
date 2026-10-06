# ONBOARDING.md — smk-doc-server v2
> คู่มือเริ่มต้นสำหรับ Developer & AI Agents • Updated September 2026

---

## Stack Summary

| Layer | Technology | Version | Notes |
|---|---|---|---|
| Backend API | ASP.NET Core | .NET 10 | Clean Architecture (4 projects) |
| Frontend Portal | Next.js | 15 + React 19 | App Router, TypeScript |
| ORM | Entity Framework Core + Npgsql | 10 | Code-first, Migrations |
| Database | PostgreSQL | 15-alpine | `smkdoc` database |
| PDF Conversion | Gotenberg | 8 | Chromium & LibreOffice |
| Object Storage | MinIO | self-host | S3-compatible (`templates/`, `outputs/`) |
| Styling | Tailwind CSS | 3 | CSS Custom Properties (Tokens SSoT) |

---

## Quick Start (V2 Local Development)

```bash
# 1. Start V2 services (ต้องใช้ project name smk-v2 เสมอ)
docker compose -p smk-v2 -f docker-compose.v2.yml up -d

# 2. ตรวจสอบ containers
docker ps | grep smk-v2

# 3. Backend tests
cd backend-v2
dotnet test

# 4. Frontend
cd frontend-v2
npm install && npm run dev
```

**หมายเหตุ:** AI Agents ห้ามรัน docker commands เอง — แสดง command แล้วให้ User รัน

---

## Key Files & Directories

| File/Dir | หน้าที่ |
|---|---|
| `AGENTS.md` | **อ่านก่อนเสมอ** — Rules สำหรับ AI Agents ทุกตัว |
| `docs/AI/ARCHITECTURE.md` | System architecture, layer rules, request pipeline |
| `docs/AI/PROJECT_STRUCTURE.md` | Folder map ของทุก project |
| `docs/AI/PATTERNS.md` | Coding patterns พร้อม code examples |
| `docs/AI/ANTI-PATTERNS.md` | สิ่งที่ห้ามทำ (พร้อม code examples) |
| `docs/AI/DECISIONS.md` | ADRs — เหตุผลที่ตัดสินใจแต่ละอย่าง |
| `docs/AI/API_CONTRACT.md` | Endpoint specs, request/response schemas |
| `docs/AI/DB_SCHEMA.md` | PostgreSQL tables, columns, relations |
| `docs/AI/TEMPLATE_ENGINE.md` | Handlebars helpers, font sync rules |
| `docs/AI/DESIGN.md` | UI Tokens, component specs (frontend) |
| `backend-v2/src/SmkDoc.Api/Program.cs` | DI registration (single source of truth for wiring) |
| `backend-v2/src/SmkDoc.Application/Common/Helpers/ThaiDataTransformer.cs` | SSoT for Thai formatting |
| `backend-v2/src/SmkDoc.Application/Common/Helpers/PlaceholderHelper.cs` | SSoT for Placeholder Regex |
| `docker-compose.v2.yml` | V2 container definitions |
| `frontend-v2/src/tokens/index.ts` | Design Tokens SSoT |

---

## 5 Rules ที่ต้องจำ

1. **ห้ามแตะ V1** — ไฟล์ใน `frontend/` และ `backend/` เป็น Legacy ห้ามแก้ไข
2. **Plan before Code** — ทุก feature ต้องนำเสนอ Plan เป็น Artifact และรอ Confirm ก่อน
3. **Thin Controllers** — Controller ≤ 5 บรรทัด ห้าม inject `IRepository<T>` โดยตรง
4. **Test must pass** — `dotnet test` และ `npm test` ต้องผ่าน 100% (0 errors, 0 failures) ทุกครั้งที่แก้โค้ด
5. **RFC 7807 errors** — Error responses ทั้งหมดต้องเป็น Problem Details format

---

## Development Workflow สำหรับ AI Agents

```
1. อ่าน AGENTS.md + docs/AI/ ที่เกี่ยวข้อง
2. วิเคราะห์ codebase จริง (grep/view file)
3. สร้าง Plan → Artifact → รอ User confirm
4. Implement → dotnet build → dotnet test
5. ถามว่าต้องการอัปเดต docs/AI/ ไหม
```

---

## Environment Variables ที่สำคัญ

| Variable | หน้าที่ | Example |
|---|---|---|
| `MASTER_API_KEY` | Bypass DB API key validation (bootstrapping) | `smk-dev-key-2026` |
| `DATABASE_URL` | PostgreSQL connection string | `Host=localhost;Port=5433;...` |
| `MINIO_ENDPOINT` | MinIO host:port | `minio:9000` |
| `MINIO_PUBLIC_ENDPOINT` | Public URL สำหรับ Presigned URLs | `http://localhost:9000` |
| `GOTENBERG_URL` | Gotenberg base URL | `http://gotenberg:3000` |
| `Jwt:Secret` | JWT signing secret (≥32 chars) | — |
