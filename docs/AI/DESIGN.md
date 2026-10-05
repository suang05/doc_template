# DESIGN.md — Frontend Design Tokens & UI Architecture

> **Purpose:** Authoritative design tokens, Ice-White theme rules, geometry standards, and HyperUI component layout patterns.  
> **Related Docs:** [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Frontend TypeScript standards), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (Frontend anti-patterns).

### ⚡ Quick-Lookup: Frontend Design Tokens & Geometry

| Token / Concept | Standard Value | Rule |
|---|---|---|
| **Base Canvas** | Ice-White (`#f8fbfe` / `#f0f7ff`) | Minimalist modern theme; **NEVER** use dark navy |
| **Border Radius** | `2px` to `4px` (`rounded-sm`) | Compact enterprise geometry; no bubble/pill shapes |
| **Icons** | Lucide Icons (`lucide-react`) | Strictly Lucide only; no emoji or mixed icon sets |
| **Validation** | Zod Schemas (`src/schemas/`) | Runtime type safety; **zero `any`** types |
| **HTTP Client** | `apiClient<T>`, `apiClientBlob`, `apiClientStream` | SSoT fetch wrappers พร้อม RFC 7807 Problem Details & AbortSignal |

---

## 🎨 Frontend Architecture & Design Rules (`frontend-v2/`)

All developments in `frontend-v2/` MUST strictly adhere to the following standards:

### 1. Single Source of Truth (SSoT) for Design Tokens
- All visual values (colors, category accents, radii, typography, spacing) MUST be defined in `src/tokens/index.ts` and mirrored in `DESIGN.md`.
- **STRICT PROHIBITION:** Never hardcode hex colors (e.g. `#10b981`) or arbitrary radii in components. Always use semantic token utilities or token references.
- **NO SAMMAKORN Navy:** Never use dark navy. The base canvas is Modern Minimalist **ขาวอมฟ้า (Ice-White `#f8fbfe` / `#f0f7ff`)**.
- **Category Colorful Accents:** Document types must use designated functional colors:
  - 💜 **Contracts / Legal:** Indigo (`#6366f1`)
  - 💚 **Financial / Invoices:** Emerald (`#10b981`)
  - 💙 **Official Letters:** Sky Blue (`#0284c7`)
  - 🧡 **HR / Personnel:** Amber (`#f59e0b`)
  - 🩵 **Operations / General:** Cyan / Slate (`#0891b2`)

### 2. Geometry, Icons & Micro-Copy Rules
- **Border Radius:** Strictly **2px to 4px** (`--radius-sm: 2px; --radius-md: 4px;` / `rounded-[2px]`, `rounded-[4px]`, `rounded-sm`). No bubble or circular rounded shapes.
- **Iconography:** Use **Lucide icons ONLY** (`lucide-react`). No mixing with other icon sets.
- **Layout & Typography:** High-density, compact dashboard layout (`text-xs`, `text-sm`, `h-8` action buttons). Do not use oversized banners.
- **Concise Micro-Copy:** Rely on symbolic communication (colored status dots, icons, badges). Keep Thai text crisp, short, and natural. Do NOT use redundant English brackets (e.g., use `"เอกสาร"` instead of `"เอกสาร (Documents)"`, `"ผู้ดูแลระบบ"` instead of `"ผู้ดูแล [ADMIN]"`). Avoid robotic or "AI-generated" phrasing.

### 3. Zod-First Validation (Runtime Type-Safety)
- Every API request, response, and form input must be validated via Zod schemas in `src/schemas/`.
- Never use raw `any` types for document payloads or API responses.

### 4. SOLID / KISS / DRY in Frontend
- **SRP:** UI components only render presentation; business logic lives in `src/hooks/`; API calls live in `src/lib/api/`.
- **OCP:** UI blocks (e.g. `Badge`, `CardBlock`) accept category variant props mapped to tokens.
- **DIP:** Hooks depend on API abstractions and Zod schemas, not raw fetch calls inside UI.
- **DRY:** Single source of truth for API routes, tokens, and schemas.
- **KISS:** Keep React state simple, predictable, and clean.

### 5. Navigation & Layout Architecture (SPA Tab Switcher)
The frontend implements a Single Page Application (SPA) architecture for layout navigation. Instead of using native Next.js App Router navigation (`/app/[route]`), the main page (`app/page.tsx`) uses an `<AppShell>` that manages an `activeTab` state and renders views using a `switch` statement.
- **Topbar (`Topbar.tsx`):**
  - Left: Toggle Sidebar (`PanelLeftClose`), Navigation history back/forward (`ChevronLeft`, `ChevronRight`), Home (`Home`).
  - Right: Quick Master API Key input box with mono font, Quick Search (`Search` / `Ctrl+K`), Notifications (`Bell`), Help (`HelpCircle`).
- **Sidebar (`Sidebar.tsx`):**
  - **เอกสาร:** `templates` (แม่แบบทั้งหมด), `generator` (สร้างเอกสาร), `audit` (ประวัติการสร้าง), `logs` (ประวัติการใช้งาน).
  - **จัดการ:** `studio` (Template Editor v2), `upload` (อัปโหลด Template), `mapping` (กำหนดฟิลด์), `version-history` (Version History), `analytics` (Analytics).
  - **ผู้ดูแลระบบ:** `datasources` (Datasources v2), `projects` (API Keys), `users` (จัดการผู้ใช้), `settings` (ตั้งค่าระบบ), `apidocs` (API Docs).
  - **Footer:** User Profile + Popover (เปลี่ยน API Key, ออกจากระบบ).

### 5.1 Card UI Structure
- Card main actions (e.g., Primary Button "สร้างเอกสาร") MUST be aligned to the **bottom-right**.
- Secondary/Context menus (e.g., Dropdown `⋮`) MUST be aligned to the **bottom-left** to prevent dropdown clipping.
- Do NOT use massive Modals for complex forms (e.g., Upload Template); always use split-screen layouts.

### 6. Consolidated Reusable UI Blocks (17 Atomic Components)
Eliminate legacy duplication (`Badge` + `StatusBadge` + `Pill` -> `Badge.tsx`; `Tabs` + `FilterTabs` -> `Tabs.tsx`):
1. **`Button.tsx`**: Semantic actions (`primary` [Sky Blue], `secondary`, `outline`, `ghost`, `danger`, `success`), loading spinner, 2-4px radius. Zero `navy` variant.
2. **`Badge.tsx`**: Consolidated for format tags (`pdf`, `docx`, `xlsx`, `html`), status dots (`success`, `failed`, `pending`), and category accents (`contract`, `financial`, `official`, `hr`, `operations`).
3. **`CardBlock.tsx`**: HyperUI-style card block on Ice-White canvas with 1px border (`border-border`).
4. **`StatBlock.tsx`**: HyperUI metric tile with value, label, trend badge, and colored icon box.
5. **`Input.tsx`**: Form input with Zod validation error integration, icon slot, clear button.
6. **`Select.tsx`**: Dropdown select with Zod validation.
7. **`Modal.tsx`**: Accessible dialog overlay with backdrop and 2-4px radius.
8. **`Table.tsx`**: Data grid with column alignment, sorting indicators, and striped/hover rows.
9. **`Pagination.tsx`**: Page navigator with item counter and page size selector.
10. **`Tabs.tsx`**: Consolidated tabs with optional count badges.
11. **`CodeBlock.tsx`**: Syntax-highlighted code viewer with 1-click copy.
12. **`EmptyState.tsx`**: Symbolic empty indicator with icon, title, and action button.
13. **`Toast.tsx`**: Floating notification alerts.
14. **`Toolbar.tsx`**: Action bar container combining search, category filters, and action buttons.
15. **`Dropdown.tsx`**: Accessible dropdown menu component with customizable triggers.
16. **`PdfPreviewPanel.tsx`**: Integrated PDF previewer for studio and generator.
17. **`Pill.tsx`**: Specialized tag-like pill component.

---

