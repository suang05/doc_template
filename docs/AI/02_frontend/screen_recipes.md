# screen_recipes.md — Frontend Screen Specifications & Stitching Recipes

<cognitive_priming>
> [PURPOSE_SSOT]: Authoritative Single Source of Truth (SSoT) for frontend UX specifications, Screen Inventory, ASCII layout wireframes, AppShell architecture, server-side API proxying, and the 4 Master Component Stitching Recipes for `frontend-v2/`.
> [OPERATIONAL_MODES]:
> - `[MODE: VIEW_ASSEMBLY]`: Assemble views strictly using one of the 4 Master Stitching Recipes and atomic primitives from `@/components/ui/`.
> - `[MODE: PROTOCOL_SANITIZATION]`: Sanitize external AI-generated UI markup (v0, Stitch, Figma Make) to match SSoT tokens and geometry.
> [BEACONS: Next.js 15, App Router, RSC, Monaco Live Preview, AbortController, Stitching Recipes, RBAC, Zod]
</cognitive_priming>

<prior_inversion_map>
| Frontend Context | Defective Prior (Banned) | Canonical Standard (Mandatory Substitution) | Invariant Logic |
|---|---|---|---|
| **Page Architecture** | Marking entire page with `"use client"` | RSC by default >> Leaf Client Components | Server streaming & minimal client JS payload |
| **Component Assembly** | Raw inline HTML (`<button>`, `<input>`) | Stitching atomic primitives from `@/components/ui/` | Design system consistency & accessibility |
| **Token Styling** | Ad-hoc hex or standard Tailwind colors | SSoT semantic tokens (`bg-canvas`, `bg-surface`, `bg-primary`) | Theme cohesion & central maintenance |
| **Live Preview Sync** | Unthrottled render calls without abort | Debounced (800ms) with `AbortController.abort()` | Race-condition & memory leak prevention |
| **API Token Storage** | Storing Bearer JWT in LocalStorage/cookies | Next.js API Proxy (`/api/proxy/[...path]`) + httpOnly cookie | Zero JWT exposure in browser JavaScript |
| **Type Definitions** | Manual duplicate TypeScript interfaces | Derived from Zod schemas via `z.infer<typeof Schema>` | Runtime & compile-time Single Source of Truth |
| **Error Handling** | Unhandled fetch catch or generic alerts | RFC 9457 `ProblemDetails` via `ApiError` adapter | Type-safe error diagnostics across fields |
</prior_inversion_map>

<severity_hierarchy>
- `[TIER-1-FATAL]`: Zero raw unstyled `<button>` / `<input>` tags, zero Bearer JWT stored in client JavaScript (proxy only), zero `any` types in TypeScript definitions.
- `[TIER-2-INTEGRITY]`: Restrict `"use client"` strictly to interactive leaves, enforce 800ms debounce with `AbortController` on live preview, derive TS types from Zod schemas.
- `[TIER-3-PERF]`: Clean URL object revocation (`URL.revokeObjectURL`) on preview stream re-renders, centralized API client wrappers (`apiClient<T>`).
</severity_hierarchy>

<screen_recipes_scope>

## 1. System Architecture & Layout Topology

```
Browser Navigation >> Next.js App Router (URL)
  >> AuthContext (User Session & Role)
  >> AppShell Hierarchy:
     ├── Topbar (h-12 / 48px): Sidebar toggle, history navigation, X-API-Key input, Ctrl+K search, alerts
     ├── Sidebar (56px collapsed / 224px expanded):
     │   ├── เอกสาร: Templates, Generator, Audit
     │   ├── จัดการ: Studio, Upload, Mapping, Version History, Analytics
     │   ├── ผู้ดูแลระบบ: Datasources, Projects, Users, Settings
     │   └── User Profile Popover
     └── Main Canvas (#f8fbfe): Centered max-w-7xl responsive scrollable container
```

### 1.1 Secure Server-Side API Proxy Architecture

The browser client **never** stores or handles the backend Bearer JWT. The client communicates strictly with the Next.js server route handler, which translates secure httpOnly session cookies into upstream bearer tokens:

```
Browser Client (apiClient<T>)
  >> POST /api/proxy/v1/templates (Cookie included, zero Bearer JWT in client JS)
  >> Next.js Route Handler (/api/proxy/[...path]):
     ├── await auth() reads httpOnly cookie
     ├── Extracts session.backendToken
     ├── Injects Authorization: Bearer {token}
     ├── Preserves X-API-Key header
     └── POST http://localhost:8080/api/v1/templates
  >> Backend Gateway (SmkDoc.Api :8080)
  >> Streams response / RFC 9457 ProblemDetails back to Client
```

### 1.2 Monaco Studio v2 Interactive Live Preview Pipeline

Delivers real-time feedback with zero UI freezing, handling large HTML templates and Handlebars data binding through an asynchronous debounced pipeline:

```
1. Editor types HTML / Handlebars in Monaco Editor
2. variableExtractor.ts auto-extracts {{variables}} into sample JSON payload
3. useLivePreview Hook triggers:
   ├── Starts 800ms debounce timer
   ├── Aborts any active pending request via in-flight abortController.abort()
4. Idle 800ms elapses >> POST /api/proxy/v1/documents/preview/{slug}
5. Backend renders HTML via Gotenberg Chromium >> Binary PDF Stream
6. Next.js Proxy streams Blob (application/pdf) to Client
7. Hook Lifecycle:
   ├── URL.revokeObjectURL(previousUrl) to prevent browser memory leaks
   ├── newUrl = URL.createObjectURL(blob)
   └── Measures renderLatencyMs and contentSizeBytes
8. PdfPreviewPanel renders iframe with live PDF and APM telemetry badge
```

---

## 2. Role-Based Access Control (RBAC) Matrix

Frontend permissions mirror the backend security domain and are enforced via `frontend-v2/src/lib/rbac.ts`:

| Section | Route ID | Route Path | Min Role | Viewer | Editor | Admin |
|---|---|---|---|:---:|:---:|:---:|
| **เอกสาร** | `templates` | `/templates` | Editor | No | Yes | Yes |
| **เอกสาร** | `generator` | `/generator` | Viewer | Yes | Yes | Yes |
| **เอกสาร** | `audit` | `/audit` | Viewer | Yes | Yes | Yes |
| **เอกสาร** | `logs` | `/logs` | Admin | No | No | Yes |
| **จัดการ** | `studio` | `/studio/[templateId]` | Editor | No | Yes | Yes |
| **จัดการ** | `upload` | `/upload` | Editor | No | Yes | Yes |
| **จัดการ** | `mapping` | `/mapping` | Editor | No | Yes | Yes |
| **จัดการ** | `version-history`| `/version-history` | Editor | No | Yes | Yes |
| **จัดการ** | `analytics` | `/analytics` | Admin | No | No | Yes |
| **ผู้ดูแลระบบ** | `datasources` | `/datasources` | Admin | No | No | Yes |
| **ผู้ดูแลระบบ** | `projects` | `/projects` | Admin | No | No | Yes |
| **ผู้ดูแลระบบ** | `users` | `/users` | Admin | No | No | Yes |
| **ผู้ดูแลระบบ** | `settings` | `/settings` | Admin | No | No | Yes |
| **คู่มือ** | `apidocs` | `/apidocs` | Viewer | Yes | Yes | Yes |

---

## 3. The 4 Master Stitching Recipes (Lego Blueprints)

All views MUST assemble atomic components using one of these four master recipes:

### Recipe 1: `DataGridRecipe` (Listing & Management Pages)
- **Use Case:** Tables and grids with filtering, search, pagination, and row actions.
- **Assembly Structure:**
  ```tsx
  <div className="space-y-4">
    <PageHeader title="..." description="..." action={<Button variant="primary">...</Button>} />
    <Toolbar
      search={<Input placeholder="ค้นหา..." value={search} onChange={...} />}
      filters={<><Select options={categories} /><Select options={formats} /></>}
    />
    <CardBlock className="p-0 overflow-hidden">
      <Table columns={columns} data={rows} onRowClick={...} />
    </CardBlock>
    <Pagination page={page} totalPages={totalPages} totalItems={total} onPageChange={...} />
  </div>
  ```
- **Applied In:** `/templates`, `/audit`, `/logs`, `/users`, `/datasources`, `/projects`.

### Recipe 2: `SplitStudioRecipe` (Authoring & Live Preview Pages)
- **Use Case:** Code/template editing requiring immediate visual feedback.
- **Assembly Structure:**
  ```tsx
  <div className="flex flex-col h-[calc(100vh-6rem)] border border-border rounded-sm bg-surface overflow-hidden">
    <StudioHeaderBar title="..." isDirty={isDirty} onSave={...} onBack={...} />
    <div className="flex-1 flex overflow-hidden">
      {/* Left Pane (Editor) */}
      <div className="w-1/2 border-r border-border flex flex-col">
        <Tabs tabs={['เนื้อหา HTML', 'Header & Footer', 'Sample Data']} active={tab} onChange={...} />
        <div className="flex-1 overflow-hidden"><Editor language="html" value={code} onChange={...} /></div>
      </div>
      {/* Right Pane (Live PDF Preview) */}
      <div className="w-1/2 flex flex-col bg-slate-100">
        <PdfPreviewPanel pdfUrl={pdfUrl} loading={previewLoading} latencyMs={renderLatencyMs} />
      </div>
    </div>
  </div>
  ```
- **Applied In:** `/studio/[templateId]`, `/mapping`.

### Recipe 3: `WizardRecipe` (Interactive Document Generation)
- **Use Case:** Guided step-by-step inputs producing a document stream.
- **Assembly Structure:**
  ```tsx
  <div className="space-y-4">
    <PageHeader title="สร้างเอกสาร" description="กรอกข้อมูลเพื่อสร้างไฟล์ PDF/Word/Excel" />
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
      <div className="lg:col-span-5 space-y-4">
        <CardBlock title="1. เลือกแม่แบบ"><TemplateSelector ... /></CardBlock>
        <CardBlock title="2. ข้อมูล JSON Payload"><JsonPayloadForm ... /></CardBlock>
        <Button variant="primary" className="w-full" onClick={handleGenerate}>สร้างเอกสารทันที</Button>
      </div>
      <div className="lg:col-span-7">
        <PdfPreviewPanel pdfUrl={generatedPdfUrl} loading={generating} />
      </div>
    </div>
  </div>
  ```
- **Applied In:** `/generator`, `/upload`.

### Recipe 4: `DashboardRecipe` (Analytics & Telemetry)
- **Use Case:** High-level metrics, system health, and operational overviews.
- **Assembly Structure:**
  ```tsx
  <div className="space-y-6">
    <PageHeader title="ภาพรวมระบบ" description="สถิติการใช้งานและการประมวลผล" />
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <StatBlock label="สร้างสำเร็จ" value="12,480" trend="+12%" icon={CheckCircle} />
      <StatBlock label="ความเร็วเฉลี่ย" value="142 ms" trend="-8%" icon={Zap} />
      <StatBlock label="ล้มเหลว" value="3" trend="-50%" icon={AlertTriangle} />
      <StatBlock label="พื้นที่จัดเก็บ" value="1.4 GB" icon={HardDrive} />
    </div>
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <CardBlock title="ประวัติการสร้างล่าสุด"><RecentAuditList /></CardBlock>
      <CardBlock title="สถานะ Microservices"><ServiceHealthGrid /></CardBlock>
    </div>
  </div>
  ```
- **Applied In:** `/analytics`, `/settings`.

---

## 4. Complete Screen Inventory (14 Active Routes)

| Screen Name | Route Path | Min Role | Stitch Recipe | Key Hooks / Context | Primary User Action |
|---|---|:---:|---|---|---|
| **แม่แบบทั้งหมด** | `/templates` | Editor | `DataGridRecipe` | `useTemplates()`, `useStoredApiKey()` | ค้นหา, กรองหมวดหมู่, เปิด Studio |
| **สร้างเอกสาร** | `/generator` | Viewer | `WizardRecipe` | `useDocuments()`, `useStoredApiKey()` | เลือกเทมเพลต, กรอก JSON, สั่งสร้าง |
| **สตูดิโอเทมเพลต** | `/studio/[templateId]`| Editor | `SplitStudioRecipe` | `useTemplateStudio()`, `useLivePreview()`| แก้โค้ด HTML/Handlebars, พรีวิวสด |
| **ประวัติการสร้าง** | `/audit` | Viewer | `DataGridRecipe` | `useAuditLogs()` | ตรวจสอบผลการสร้าง, ดาวน์โหลดซ้ำ |
| **ประวัติการใช้งาน** | `/logs` | Admin | `DataGridRecipe` | `useGenerationLogs()` | ตรวจสอบ System Logs, Latency, Errors |
| **อัปโหลดแม่แบบ** | `/upload` | Editor | `WizardRecipe` | `useTemplateUpload()` | อัปโหลด HTML/Word/Excel, ตั้งชื่อ |
| **กำหนดฟิลด์** | `/mapping` | Editor | `SplitStudioRecipe` | `useFieldMappings()` | แมป JSON Payload กับ Dataset |
| **ประวัติเวอร์ชัน** | `/version-history` | Editor | `DataGridRecipe` | `useVersionHistory()` | เปรียบเทียบ Diff, สลับเวอร์ชันหลัก |
| **Analytics** | `/analytics` | Admin | `DashboardRecipe` | `useAnalytics()` | ดูกราฟ Throughput, Error Rate |
| **Datasources** | `/datasources` | Admin | `DataGridRecipe` | `useDatasources()` | จัดการ Connection SQL/API |
| **API Keys** | `/projects` | Admin | `DataGridRecipe` | `useProjects()`, `useApiKeys()` | ออกคีย์ ReadOnly / ReadWrite |
| **จัดการผู้ใช้** | `/users` | Admin | `DataGridRecipe` | `useUsers()` | กำหนดสิทธิ์ Viewer, Editor, Admin |
| **ตั้งค่าระบบ** | `/settings` | Admin | `DashboardRecipe` | `useSettings()`, `useStoredApiKey()`| จัดการ Master Key, MinIO, Gotenberg |
| **API Docs** | `/apidocs` | Viewer | Custom Doc | `useAuthContext()` | อ่าน OpenAPI Contract & ตัวอย่างโค้ด |

---

## 5. ASCII Wireframe Blueprints

### Wireframe 1: Template Catalog (`/templates`)

```text
[Topbar]  (h-12) [Toggle] [Back] [Forward]  แม่แบบทั้งหมด          [API Key...] [Ctrl+K] [Alert]
-----------------------------------------------------------------------------------------------
[Sidebar] | Main Content Canvas (Ice-White #f8fbfe)
(w-56)    | -----------------------------------------------------------------------------------
          | | PageHeader: แม่แบบทั้งหมด                              [+ อัปโหลดแม่แบบ]         |
เอกสาร    | -----------------------------------------------------------------------------------
- แม่แบบ  | | Toolbar: [Search input...]  [หมวดหมู่ Select]  [รูปแบบ Select]                   |
- สร้าง   | -----------------------------------------------------------------------------------
- ประวัติ | | Grid / Table (3 Columns):                                                       |
          | | [Card: สัญญาจ้างพนักงาน v2.1]   [Card: ใบเสร็จรับเงิน]    [Card: ใบรับรองเงินเดือน]  |
จัดการ    | | [Badge: สัญญา] [Word]           [Badge: การเงิน] [PDF]    [Badge: บุคคล]             |
- สตูดิโอ | | แก้ไข: 2 ชม. ที่แล้ว            แก้ไข: 1 วันก่อน          แก้ไข: 3 วันก่อน           |
- ฟิลด์   | | [Menu]  [สร้างเอกสาร]           [Menu]  [สร้างเอกสาร]     [Menu]  [สร้างเอกสาร]      |
          | -----------------------------------------------------------------------------------
Admin     | | Pagination: แสดง 1-12 จาก 48 รายการ                     [<] 1 [2] 3 [>]          |
- ผู้ใช้  | -----------------------------------------------------------------------------------
- ตั้งค่า |
```

### Wireframe 2: Template Studio v2 (`/studio/[templateId]`)

```text
[Topbar]  (h-12) [Toggle]  แม่แบบทั้งหมด / สตูดิโอแม่แบบ
-----------------------------------------------------------------------------------------------
[Sidebar] | Studio Container (h-[calc(100vh-6rem)])
(w-14)    | -----------------------------------------------------------------------------------
[icon]    | | [Back] สัญญาจ้างพนักงาน (สัญญา-001) *     [142ms / 48KB]  [Save Button]           |
[icon]    | -----------------------------------+-----------------------------------------------
[icon]    | | Tabs: [HTML เนื้อหา]             | [PdfPreviewPanel]                             |
[icon]    | |       [Header/Footer]            | [Zoom] [Print] [Download]                     |
          | |       [Sample JSON Payload]      |                                               |
          | |----------------------------------| [Embedded PDF Document Frame]                 |
          | | [Monaco Editor Pane]             | วันที่ {{thaiDate createdDate}}               |
          | | 1 | <!DOCTYPE html>              |                                               |
          | | 2 | <html>                       | จำนวนเงิน {{bahtText amount}}                 |
          | | 3 |   <body>                     |                                               |
          | | 4 |     <h1>{{title}}</h1>       |                                               |
          | | 5 |   </body>                    |                                               |
          | | 6 | </html>                      |                                               |
```

### Wireframe 3: Document Generator (`/generator`)

```text
[Topbar]  (h-12) [Toggle]  สร้างเอกสาร
-----------------------------------------------------------------------------------------------
[Sidebar] | Main Content Canvas (#f8fbfe)
          | -----------------------------------------------------------------------------------
          | | PageHeader: สร้างเอกสารด่วน (Interactive Generator)                             |
          | -----------------------------------+-----------------------------------------------
          | | Step 1: เลือกแม่แบบ              | พรีวิวเอกสารที่สร้าง                          |
          | | [Select: สัญญาจะซื้อจะขาย]        |                                               |
          | |                                  | [Live PDF Preview Frame]                      |
          | | Step 2: กรอกข้อมูล (JSON Payload)|                                               |
          | | [JSON Payload Editor]            |                                               |
          | | { "customer": "สมชาย", ... }     |                                               |
          | |                                  |                                               |
          | | [Action: ดาวน์โหลด DOCX]         |                                               |
          | | [Primary Action: สร้าง PDF ทันที] | [Status: พร้อมดาวน์โหลด (120 KB)]             |
```

---

## 6. Design Tool Stitch Sanitizer Protocol

When receiving mockups or exported JSX/HTML from AI design tools (**Google Stitch**, **v0.dev**, **Figma Make**, **Claude Design**), apply this normalization protocol before committing code:

1. **Color Token Normalization:**
   - `bg-slate-50`, `bg-gray-100` >> `bg-canvas` (`#f8fbfe`)
   - `bg-white` >> `bg-surface` (`#ffffff`)
   - `border-gray-200`, `border-slate-200` >> `border-border` (`#e2e8f0`)
   - `bg-blue-600`, `bg-indigo-600` >> `bg-primary` (`#0284c7`)
   - `text-blue-900`, `text-indigo-900` >> `text-primaryDark` (`#0369a1`)
   - Replace hardcoded hex colors with category token classes.
2. **Geometry Clamping:**
   - Clamp all bubble radii (`rounded-xl`, `rounded-2xl`, `rounded-full`) to `rounded-sm` (2px) or `rounded-md` (4px).
3. **Component Lifting:**
   - Replace raw `<button>` with `<Button variant="..." size="...">`.
   - Replace raw `<input>` with `<Input error={...}>`.
   - Replace raw status chips with `<Badge status="..." />` or `<Badge category="..." />`.
   - Replace raw modal overlays with `<Modal isOpen={...} onClose={...}>`.
4. **Icon Standardization:**
   - Strip all SVG inline tags, FontAwesome, or Heroicons; replace with equivalent Lucide Icons from `lucide-react`.

---

## 7. Frontend Engineering Standards (TS 5.7, Next.js 15 & Zod)

### 7.1 Architectural Component Hierarchy (`RULE-FE-ARCH`)
* `[RULE-FE-ARCH-001] [MUST]`: Default to React Server Components (RSC). Use Server Components for layout scaffolding, data fetching, and static UI trees.
* `[RULE-FE-ARCH-002] [MUST]`: Restrict `"use client"` directive strictly to leaf components requiring interactive state (`useState`, `useReducer`), browser event handlers (`onClick`, `onChange`), or lifecycle hooks (`useEffect`).
* `[RULE-FE-ARCH-003] [MUST NOT]`: NEVER mark entire page layouts or parent container modules with `"use client"`. Push interactivity down to the leaves of the render tree.

### 7.2 TypeScript 5.7 Strictness & Zero-`any` Invariant (`RULE-FE-TS`)
* `[RULE-FE-TS-001] [MUST]`: Strict typing is mandatory. The `any` type is STRICTLY FORBIDDEN. Use `unknown` with type guards or discriminated unions for dynamic data.
* `[RULE-FE-TS-002] [MUST]`: Derive TypeScript types directly from Zod validation schemas using `z.infer<typeof Schema>` (SSoT). Avoid declaring manual parallel interface definitions.
* `[RULE-FE-TS-003] [MUST]`: Use immutable Readonly types for component props and state slices where mutation is not required (`Readonly<{ ... }>`).

### 7.3 Zod Runtime Validation & Form Contracts (`RULE-FE-ZOD`)
* `[RULE-FE-ZOD-001] [MUST]`: All form payloads and incoming API JSON responses MUST be validated at runtime via Zod schemas located in `@/schemas/`.
* `[RULE-FE-ZOD-002] [MUST]`: Enforce fail-fast schema parsing. Parse API responses using `Schema.parse(response)` or `Schema.safeParse(response)` before passing data to presentation components.
* `[RULE-FE-ZOD-003] [MUST]`: Error messages returned to the user MUST map RFC 9457 `ProblemDetails.errors` or Zod issues to specific field states via `<Input error={...} />`.

### 7.4 HTTP Client & Streaming Hygiene (`RULE-FE-HTTP`)
* `[RULE-FE-HTTP-001] [MUST]`: All network requests MUST use centralized fetch wrappers (`apiClient<T>`, `apiClientBlob`, `apiClientStream`). Raw `fetch()` invocations inside components are PROHIBITED.
* `[RULE-FE-HTTP-002] [MUST]`: Always pass `AbortSignal` (`ct.signal`) to cancel inflight requests upon component unmount or search query debounce.
* `[RULE-FE-HTTP-003] [MUST]`: Document previews (PDF, DOCX) MUST stream via `apiClientBlob` or `ReadableStream` directly into `URL.createObjectURL(blob)` and revoke URL objects upon teardown to prevent client memory leaks.

### 7.5 Frontend Unit Testing Standards (`RULE-FE-TEST`)
* `[RULE-FE-TEST-001] [MUST]`: Place all Vitest unit test files exclusively inside `frontend-v2/src/__tests__/` (`*.test.ts`). Creating parallel `src/tests/` directories is strictly prohibited.
* `[RULE-FE-TEST-002] [MUST]`: Import Zod schemas exclusively from the canonical `@/schemas/*.schema` modules. Never validate against legacy duplicate definitions in `@/types/api`.
* `[RULE-FE-TEST-003] [MUST]`: Enforce 1:1 validation parity with Backend Domain Value Objects (e.g., `TemplateSlug` regex `^[a-z0-9]+(-[a-z0-9]+)*$` prohibits underscores `_` and uppercase characters).
* `[RULE-FE-TEST-004] [MUST NOT]`: Never write tautological tests that assert on string literals, static constant arrays, or helper functions defined locally inside the test file. Every test MUST import and invoke a real production function, hook, or schema.
* `[RULE-FE-TEST-005] [MUST]`: Assert async rejections in a single statement using `await expect(promise).rejects.toMatchObject({ status, errorCode })` instead of redundant `rejects.toThrow()` followed by `try/catch` blocks.

---

## 8. Frontend Lifecycle Implementation Archetypes (Next.js 15 / React 19)

### Archetype 10: Server-First RSC Data Fetching & Leaf Client Component

```tsx
// =========================================================================
// CANONICAL IMPLEMENTATION: Archetype 10 (React Server Component)
// File: src/app/templates/[templateId]/page.tsx
// =========================================================================
import { notFound } from "next/navigation";
import { fetchTemplateById } from "@/lib/api/templates";
import { TemplateEditorClient } from "@/components/features/templates/TemplateEditorClient";

interface TemplatePageProps {
  params: Promise<{ templateId: string }>;
}

export default async function TemplatePage({ params }: TemplatePageProps) {
  const { templateId } = await params;
  const template = await fetchTemplateById(templateId);

  if (!template) {
    notFound();
  }

  return (
    <main className="container mx-auto px-4 py-6">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight text-neutral-900">{template.name}</h1>
      </div>
      <TemplateEditorClient initialTemplate={template} />
    </main>
  );
}
```

### Archetype 11: Race-Condition-Free Live Preview (`AbortController` + Telemetry)

```tsx
// =========================================================================
// CANONICAL IMPLEMENTATION: Archetype 11 (Live Preview Hook)
// File: src/hooks/useLivePreview.ts
// =========================================================================
"use client";

import { useRef, useState, useCallback } from "react";
import { renderPreviewApi } from "@/lib/api/documents";

export function useLivePreview() {
  const abortControllerRef = useRef<AbortController | null>(null);
  const [previewBlobUrl, setPreviewBlobUrl] = useState<string | null>(null);
  const [latencyMs, setLatencyMs] = useState<number | null>(null);
  const [isRendering, setIsRendering] = useState(false);

  const triggerPreview = useCallback(async (templateId: string, payload: Record<string, unknown>) => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const abortController = new AbortController();
    abortControllerRef.current = abortController;

    setIsRendering(true);
    const startTime = performance.now();

    try {
      const blob = await renderPreviewApi(templateId, payload, abortController.signal);
      
      const endTime = performance.now();
      setLatencyMs(Math.round(endTime - startTime));
      
      if (previewBlobUrl) {
        URL.revokeObjectURL(previewBlobUrl);
      }
      
      setPreviewBlobUrl(URL.createObjectURL(blob));
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      console.error("Live preview generation failed:", error);
    } finally {
      setIsRendering(false);
    }
  }, [previewBlobUrl]);

  return { previewBlobUrl, latencyMs, isRendering, triggerPreview };
}
```

### Archetype 12: Type-Safe RFC 9457 Client Diagnostic Adapter

```typescript
// =========================================================================
// CANONICAL IMPLEMENTATION: Archetype 12 (RFC 9457 Adapter)
// File: src/lib/api/client.ts
// =========================================================================
export interface ProblemDetailsError {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  invalidParams?: Array<{ name: string; reason: string }>;
  errors?: Array<{ path: string; message: string; rule?: string }>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problemDetails: ProblemDetailsError
  ) {
    super(problemDetails.detail ?? problemDetails.title ?? "An API error occurred");
    this.name = "ApiError";
  }
}

export async function parseResponse<T>(response: Response): Promise<T> {
  if (response.ok) {
    if (response.status === 204) return {} as T;
    return (await response.json()) as T;
  }

  const contentType = response.headers.get("content-type");
  if (contentType?.includes("application/problem+json") || contentType?.includes("application/json")) {
    const errorJson = (await response.json()) as ProblemDetailsError;
    throw new ApiError(response.status, errorJson);
  }

  throw new ApiError(response.status, {
    status: response.status,
    title: response.statusText,
    detail: await response.text()
  });
}
```

</screen_recipes_scope>

<verification_gateway>
- [ ] Views strictly use one of the 4 Master Stitching Recipes (`DataGridRecipe`, `SplitStudioRecipe`, `WizardRecipe`, `DashboardRecipe`)
- [ ] No raw `<button>` or `<input>` elements emitted in page components
- [ ] `"use client"` pushed down strictly to leaf components requiring interactive events or state
- [ ] Preview hooks implement `AbortController` cancellation and URL revocation
- [ ] Client routes enforce RBAC permissions according to the 14-route matrix
- [ ] API calls utilize `apiClient<T>` with RFC 9457 error adapter
</verification_gateway>
