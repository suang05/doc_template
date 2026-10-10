# DESIGN.md — Frontend Design Tokens & UI Design System

> **Purpose:** Authoritative Single Source of Truth (SSoT) for UI design tokens, Ice-White theme rules, sharp enterprise geometry, atomic component primitives, and micro-copy standards for `frontend-v2/`.  
> **Related Docs:** [FRONTEND_SPEC.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/FRONTEND_SPEC.md) (Screen Inventory, ASCII Wireframes & Stitching Recipes), [CODING_CONVENTIONS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/CODING_CONVENTIONS.md), [ANTI-PATTERNS.md](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/ANTI-PATTERNS.md).

<ai_directive>
CRITICAL ATTENTION ROUTING: 
This document defines the strict visual design tokens and atomic component rules for the Next.js (`frontend-v2/`) portal.
- When styling React components, Tailwind utility classes, or atomic primitives, you MUST strictly apply the design tokens in this document.
- For Screen Inventory, page layouts, ASCII wireframes, and page stitching recipes, ALWAYS refer to [FRONTEND_SPEC.md](FRONTEND_SPEC.md).
- **BI-DIRECTIONAL SYNC TRIGGER:** If you modify tokens or atomic components in this document, you MUST proactively ask the user whether to synchronize the screen specifications in `FRONTEND_SPEC.md`.
</ai_directive>

<frontend_scope>

### ⚡ Quick-Lookup: Design Tokens & Geometry Standards

| Token / Concept | Standard Value / SSoT | Architectural Invariant |
|---|---|---|
| **Base Canvas** | Ice-White (`#f8fbfe` / `#f0f7ff`) | Minimalist modern theme; **NEVER** use dark navy or pure cold gray |
| **Primary Brand** | Sky Blue (`#0284c7`, Light: `#f0f9ff`, Dark: `#0369a1`) | Single interactive brand color; high contrast on white/ice surfaces |
| **Border Radius** | Sharp `0px`, `2px` (`sm`), `4px` (`md`) | Compact enterprise geometry; bubble/pill rounded corners strictly prohibited |
| **Iconography** | Lucide Icons (`lucide-react`) | Strictly Lucide only; no emoji or mixed third-party icon packages |
| **Validation SSoT** | Zod Schemas (`src/schemas/`) | Runtime type safety; infer TS types via `z.infer<T>`; **zero `any`** types |
| **HTTP Client** | `apiClient<T>`, `apiClientBlob`, `apiClientStream` | SSoT fetch wrappers with RFC 9457 `ProblemDetails` parsing & `AbortSignal` |
| **Screen Assembly** | Component Stitching Recipes | Defined in [`FRONTEND_SPEC.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/FRONTEND_SPEC.md) |

---

## 🎨 Single Source of Truth (SSoT) Design Tokens

All visual tokens are centralized in [`frontend-v2/src/tokens/index.ts`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/frontend-v2/src/tokens/index.ts). Arbitrary hex colors or ad-hoc radius classes in component files are strictly prohibited.

### 1. Geometry & Radii Tokens

The document platform enforces a crisp, high-density enterprise aesthetic:

| Token Name | Value | Tailwind Class | Semantic Application |
|---|---|---|---|
| `tokens.radius.none` | `0px` | `rounded-none` | Full-width containers, table header cells |
| `tokens.radius.sm` | `2px` | `rounded-[2px]` / `rounded-sm` | Badges, tags, form inputs, action buttons, table rows |
| `tokens.radius.md` | `4px` | `rounded-[4px]` / `rounded-md` | Cards, modals, studio panes, popovers |

> [!IMPORTANT]
> Circular pills (`rounded-full`) and oversized rounded containers (`rounded-xl`, `rounded-2xl`, `rounded-3xl`) are **strictly prohibited** across all views.

### 2. Base & Neutral Palette

| Token Name | Hex Value | Semantic Role |
|---|---|---|
| `colors.canvas` | `#f8fbfe` | Base background for the entire application (Ice-White / ขาวอมฟ้า) |
| `colors.surface` | `#ffffff` | Elevated surface for cards, modals, table bodies, topbar, sidebar |
| `colors.surfaceSubtle` | `#f1f5f9` | Secondary backgrounds, input fields, subtle hover states |
| `colors.border` | `#e2e8f0` | Standard structural borders (1px solid) |
| `colors.borderFocus` | `#bae6fd` | Interactive focus rings and highlighted borders |
| `colors.primary` | `#0284c7` | Sky Blue — primary interactive brand color |
| `colors.primaryLight` | `#f0f9ff` | Subtle primary tinted background for active tabs, selected rows |
| `colors.primaryDark` | `#0369a1` | Hover and active state for primary buttons |
| `colors.textPrimary` | `#0f172a` | High-contrast body text and headers |
| `colors.textSecondary` | `#475569` | Secondary descriptions, column headers, metadata labels |
| `colors.textMuted` | `#94a3b8` | Placeholders, inactive icons, breadcrumb separators |

### 3. Document Category Accents

Document types are mapped to functional, recognizable color accents across badges, borders, and cards:

| Category ID | Label (Thai) | Primary | Light Background | Border | Text |
|---|---|---|---|---|---|
| `contract` | สัญญา/นิติกรรม | `#6366f1` (Indigo) | `#eef2ff` | `#c7d2fe` | `#4338ca` |
| `financial` | การเงิน/ใบเสร็จ | `#10b981` (Emerald) | `#ecfdf5` | `#a7f3d0` | `#065f46` |
| `official` | หนังสือสำคัญ | `#0284c7` (Sky Blue) | `#f0f9ff` | `#bae6fd` | `#0369a1` |
| `hr` | บุคคล/ภายใน | `#f59e0b` (Amber) | `#fffbeb` | `#fde68a` | `#92400e` |
| `operations` | ปฏิบัติการทั่วไป | `#0891b2` (Cyan/Slate) | `#ecfeff` | `#a5f3fc` | `#155e75` |

### 4. Output Format Tokens

| Format ID | Extension | Primary Accent | Light Background | Text Accent |
|---|---|---|---|---|
| `pdf` | `.pdf` | `#e11d48` (Rose) | `#fff1f2` | `#9f1239` |
| `docx` | `.docx` | `#2563eb` (Blue) | `#eff6ff` | `#1e40af` |
| `xlsx` | `.xlsx` | `#059669` (Emerald) | `#ecfdf5` | `#065f46` |
| `html` | `.html` | `#d97706` (Amber) | `#fffbeb` | `#92400e` |

### 5. System Status Tokens

| Status ID | Label (Thai) | Accent Color | Dot / Badge Styling |
|---|---|---|---|
| `success` | สำเร็จ | `#10b981` | Emerald badge with solid dot |
| `failed` | ล้มเหลว | `#ef4444` | Red badge with alert dot |
| `pending` | กำลังดำเนินการ | `#f59e0b` | Amber badge with pulsing dot |
| `idle` | พร้อมใช้งาน | `#64748b` | Neutral slate badge |

---

## 📐 Card UI Geometry & Micro-Copy Rules

1. **Card Action Placement:**
   - **Primary Action Button (CTA):** Primary button (e.g. "สร้างเอกสาร", "บันทึก") MUST be aligned to the **bottom-right** of cards and panes.
   - **Context Menu Trigger:** Three-dot menu (`⋮` / `MoreVertical`) and secondary actions MUST be aligned to the **bottom-left** of card footers. This prevents dropdown menus from being clipped by viewport edges.
2. **Split-Screen over Modals:**
   - For complex authoring workflows (Template Studio, Field Mapping, Batch Generator), always use a split-screen layout (editor on left, preview on right). Modals are reserved strictly for concise, single-step confirmations.
3. **Natural Thai Micro-Copy:**
   - Use crisp, natural, professional Thai.
   - **STRICT PROHIBITION:** Never use redundant English brackets alongside Thai words (e.g., use `"แม่แบบ"` instead of `"แม่แบบ (Templates)"`, use `"ประวัติการสร้าง"` instead of `"ประวัติการสร้าง (Audit Logs)"`, use `"ผู้ดูแลระบบ"` instead of `"ผู้ดูแลระบบ [ADMIN]"`).
4. **High-Density Compact Layout:**
   - Standard font sizes: `text-xs` (12px) for table data and form inputs; `text-sm` (14px) for card body text; `text-base` / `text-lg` for section titles.
   - Action buttons standard height: `h-8` or `h-8.5`.
   - Avoid massive illustration banners or marketing cards inside the operational document gateway.

---

## 🧩 Consolidated Reusable UI Blocks (Atomic Design Components)

All reusable components are located in [`frontend-v2/src/components/ui/`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/frontend-v2/src/components/ui/) and exported via `index.ts`:

### 1. `Button.tsx`
- **Variants:** `primary` (Sky Blue `#0284c7`), `secondary` (Surface Subtle), `outline` (Border), `ghost` (Hover only), `danger` (Red `#ef4444`), `success` (Emerald `#10b981`).
- **Sizes:** `sm` (h-7, text-xs), `md` (h-8.5, text-xs), `lg` (h-10, text-sm).
- **Invariants:** Built-in loading spinner (`loading={true}` disables button), 2px-4px radius, accepts Lucide icon slots (`iconLeft`, `iconRight`). **Zero dark navy variant**.

### 2. `Badge.tsx`
- **Consolidated Types:**
  - Category Badges: `category="contract" | "financial" | "official" | "hr" | "operations"`
  - Format Badges: `format="pdf" | "docx" | "xlsx" | "html"`
  - Status Badges: `status="success" | "failed" | "pending" | "idle"` with colored indicator dot.
- **Geometry:** `rounded-sm` (2px), compact padding (`px-2 py-0.5 text-[11px]`).

### 3. `CardBlock.tsx`
- **Purpose:** Standard HyperUI-style card container for templates, logs, and dashboard widgets.
- **Styling:** `bg-surface border border-border rounded-sm p-4 hover:border-slate-300 transition-colors`.

### 4. `StatBlock.tsx`
- **Purpose:** Dashboard metric tile displaying numerical KPIs.
- **Elements:** Quantitative value (`text-2xl font-bold`), descriptive label, trend percentage badge, and colored category icon container.

### 5. `Input.tsx`
- **Purpose:** Text input with integrated validation and feedback.
- **Features:** Left icon slot, clear button (`onClear`), Zod error message display (`error={errors.fieldName?.message}`), `rounded-sm` border with focus ring.

### 6. `Select.tsx`
- **Purpose:** Form select dropdown with strongly typed options.
- **Features:** Chevron icon, disabled state, Zod error integration, compact padding.

### 7. `Modal.tsx`
- **Purpose:** Accessible dialog overlay for confirmations and settings.
- **Features:** Backdrop blur, `Escape` key listener, outside click dismissal, sharp 4px radius (`rounded-md`), header title + close `X` button.

### 8. `Table.tsx`
- **Purpose:** High-density data grid for audit logs, template lists, and user tables.
- **Features:** Sticky header, striped/hover row options, sortable column headers with direction indicators, numeric alignment support.

### 9. `Pagination.tsx`
- **Purpose:** Server-driven pagination bar.
- **Features:** Previous/Next buttons, page index numbers, total item counter (`1-20 จาก 240 รายการ`), and page size selector (`10`, `25`, `50`, `100`).

### 10. `Tabs.tsx`
- **Purpose:** Segmented tab control for multi-view panes.
- **Features:** Active tab indicator (`border-b-2 border-primary text-primary`), optional item count badge per tab, keyboard arrow navigation.

### 11. `CodeBlock.tsx`
- **Purpose:** Syntax-highlighted code and JSON payload viewer.
- **Features:** Monospace typography, subtle surface background, 1-click clipboard copy button with temporary `Check` confirmation.

### 12. `EmptyState.tsx`
- **Purpose:** Clean, informative empty state for zero-result tables or lists.
- **Features:** Centered Lucide icon in subtle container, title, helper text, and optional primary call-to-action button.

### 13. `Toast.tsx`
- **Purpose:** Floating notifications for asynchronous actions (save, generate, error).
- **Features:** Fixed bottom-right positioning, auto-dismiss timer (3000ms), variants for success, error, info, warning.

### 14. `Toolbar.tsx`
- **Purpose:** Filter and action bar situated above tables and grids.
- **Features:** Search input slot, category dropdown filters, format filter pills, and right-aligned primary CTA button.

### 15. `Dropdown.tsx`
- **Purpose:** Contextual dropdown menu.
- **Features:** Accessible trigger, bottom-left or bottom-right anchoring, dividers between action groups, danger action styling for deletion.

### 16. `PdfPreviewPanel.tsx`
- **Purpose:** Integrated PDF previewer for Studio and Generator.
- **Features:** Embedded iframe with Blob URL, full-screen toggle, download action, print action, and APM latency/size telemetry badges.

### 17. `Pill.tsx`
- **Purpose:** Lightweight interactive filter or tag pill.
- **Features:** Selected/unselected toggling, compact typography, category color accents.

### 18. `PageHeader.tsx`
- **Purpose:** Standard top section for each dashboard view.
- **Features:** Page title (`text-xl font-bold`), subtitle/description, optional breadcrumbs, and right-aligned primary action buttons.

### 19. `RequireRole.tsx`
- **Purpose:** Declarative RBAC gate wrapper component.
- **Features:** Checks current session role against required `UserRole`. If unauthorized, renders clean enterprise "Access Denied" empty state.

---

</frontend_scope>
