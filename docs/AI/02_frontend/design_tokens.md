# design_tokens.md — Frontend Design Tokens & Atomic UI System

<cognitive_priming>
> [PURPOSE_SSOT]: Authoritative Single Source of Truth (SSoT) for visual design tokens, Ice-White color palette, compact enterprise geometry (0px-4px radius), atomic component primitives, and micro-copy standards for `frontend-v2/`.
> [OPERATIONAL_MODES]:
> - `[MODE: COMPONENT_STYLING]`: Style components using `@/tokens` and Tailwind classes without ad-hoc hex values.
> - `[MODE: DESIGN_AUDIT]`: Reject circular pills (`rounded-full`) or oversized rounded containers (`rounded-xl`+).
> [BEACONS: Ice-White #f8fbfe, Sky Blue #0284c7, Lucide Icons, Zod SSoT, 2px-4px Radius, Atomic UI]
</cognitive_priming>

<prior_inversion_map>
| UI Design Context | Defective Prior (Banned) | Canonical Standard (Mandatory Substitution) | Invariant Logic |
|---|---|---|---|
| **Canvas Background** | Pure gray (`bg-gray-100`) or dark navy | Ice-White (`colors.canvas` / `#f8fbfe`) | Minimalist modern enterprise theme |
| **Corner Radius** | Bubble/pill radius (`rounded-xl`, `rounded-full`) | Sharp compact geometry (`rounded-sm` / 2px, `rounded-md` / 4px) | Enterprise data density |
| **Brand Color** | Generic royal blue or indigo | Sky Blue (`colors.primary` / `#0284c7`, hover `#0369a1`) | High-contrast single interactive brand |
| **Icon Library** | FontAwesome, inline SVGs, or emojis | Strictly Lucide React (`lucide-react`) | Consistent stroke weight and visual tone |
| **Card CTA Placement** | Top-right or centered action buttons | Primary action bottom-right; context menu bottom-left | Thumb-flow & viewport boundary safety |
| **Thai Micro-Copy** | English brackets (e.g. `"แม่แบบ (Templates)"`) | Pure crisp Thai (e.g. `"แม่แบบ"`) | Clean enterprise localization |
| **Component Tags** | Raw inline HTML (`<button>`, `<input>`) | Atomic UI primitives (`<Button>`, `<Input>`) | Design system consistency & accessibility |
</prior_inversion_map>

<severity_hierarchy>
- `[TIER-1-FATAL]`: Zero raw unstyled `<button>` or `<input>` elements (must use atomic primitives), zero bubble radii (`rounded-xl`, `rounded-2xl`, `rounded-full`), zero arbitrary hex color classes in components.
- `[TIER-2-INTEGRITY]`: Strict Lucide icon usage, bottom-right primary CTA alignment, bottom-left context menu anchor, natural Thai micro-copy without English brackets.
- `[TIER-3-PERF]`: High-density font sizing (`text-xs` for table data and inputs, `h-8` action buttons), split-screen layout over modal sprawl.
</severity_hierarchy>

<design_tokens_scope>

## 1. Quick-Lookup: Design Tokens & Geometry Standards

| Token / Concept | Standard Value / SSoT | Architectural Invariant |
|---|---|---|
| **Base Canvas** | Ice-White (`#f8fbfe` / `#f0f7ff`) | Minimalist modern theme; **NEVER** use dark navy or pure cold gray |
| **Primary Brand** | Sky Blue (`#0284c7`, Light: `#f0f9ff`, Dark: `#0369a1`) | Single interactive brand color; high contrast on white/ice surfaces |
| **Border Radius** | Sharp `0px`, `2px` (`sm`), `4px` (`md`) | Compact enterprise geometry; bubble/pill rounded corners strictly prohibited |
| **Iconography** | Lucide Icons (`lucide-react`) | Strictly Lucide only; no emoji or mixed third-party icon packages |
| **Validation SSoT** | Zod Schemas (`src/schemas/`) | Runtime type safety; infer TS types via `z.infer<T>`; zero `any` types |
| **HTTP Client** | `apiClient<T>`, `apiClientBlob`, `apiClientStream` | SSoT fetch wrappers with RFC 9457 `ProblemDetails` parsing & `AbortSignal` |

---

## 2. Geometry & Radii Tokens

All tokens are declared in `frontend-v2/src/tokens/index.ts`:

| Token Name | Value | Tailwind Class | Semantic Application |
|---|---|---|---|
| `tokens.radius.none` | `0px` | `rounded-none` | Full-width containers, table header cells |
| `tokens.radius.sm` | `2px` | `rounded-[2px]` / `rounded-sm` | Badges, tags, form inputs, action buttons, table rows |
| `tokens.radius.md` | `4px` | `rounded-[4px]` / `rounded-md` | Cards, modals, studio panes, popovers |

> [INVARIANT]: Circular pills (`rounded-full`) and oversized rounded containers (`rounded-xl`, `rounded-2xl`, `rounded-3xl`) are **strictly prohibited** across all views.

---

## 3. Base & Neutral Palette

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

---

## 4. Document Category Accents

| Category ID | Label (Thai) | Primary | Light Background | Border | Text |
|---|---|---|---|---|---|
| `contract` | สัญญา/นิติกรรม | `#6366f1` (Indigo) | `#eef2ff` | `#c7d2fe` | `#4338ca` |
| `financial` | การเงิน/ใบเสร็จ | `#10b981` (Emerald) | `#ecfdf5` | `#a7f3d0` | `#065f46` |
| `official` | หนังสือสำคัญ | `#0284c7` (Sky Blue) | `#f0f9ff` | `#bae6fd` | `#0369a1` |
| `hr` | บุคคล/ภายใน | `#f59e0b` (Amber) | `#fffbeb` | `#fde68a` | `#92400e` |
| `operations` | ปฏิบัติการทั่วไป | `#0891b2` (Cyan/Slate) | `#ecfeff` | `#a5f3fc` | `#155e75` |

---

## 5. Output Format & System Status Tokens

### 5.1 Format Tokens

| Format ID | Extension | Primary Accent | Light Background | Text Accent |
|---|---|---|---|---|
| `pdf` | `.pdf` | `#e11d48` (Rose) | `#fff1f2` | `#9f1239` |
| `docx` | `.docx` | `#2563eb` (Blue) | `#eff6ff` | `#1e40af` |
| `xlsx` | `.xlsx` | `#059669` (Emerald) | `#ecfdf5` | `#065f46` |
| `html` | `.html` | `#d97706` (Amber) | `#fffbeb` | `#92400e` |

### 5.2 System Status Tokens

| Status ID | Label (Thai) | Accent Color | Dot / Badge Styling |
|---|---|---|---|
| `success` | สำเร็จ | `#10b981` | Emerald badge with solid dot |
| `failed` | ล้มเหลว | `#ef4444` | Red badge with alert dot |
| `pending` | กำลังดำเนินการ | `#f59e0b` | Amber badge with pulsing dot |
| `idle` | พร้อมใช้งาน | `#64748b` | Neutral slate badge |

---

## 6. Card UI Geometry & Micro-Copy Rules

1. **Card Action Placement:**
   - **Primary Action Button (CTA):** Aligned to the **bottom-right** of cards and panes.
   - **Context Menu Trigger:** Three-dot menu (`⋮` / `MoreVertical`) and secondary actions aligned to the **bottom-left** of card footers. Prevents dropdown clipping on viewport edges.
2. **Split-Screen over Modals:**
   - Complex authoring workflows (Template Studio, Field Mapping, Batch Generator) use split-screen (editor left, preview right). Modals reserved strictly for concise, single-step confirmations.
3. **Natural Thai Micro-Copy:**
   - Prohibit redundant English brackets: use `"แม่แบบ"` (not `"แม่แบบ (Templates)"`), `"ประวัติการสร้าง"` (not `"ประวัติการสร้าง (Audit Logs)"`), `"ผู้ดูแลระบบ"` (not `"ผู้ดูแลระบบ [ADMIN]"`).
4. **High-Density Compact Layout:**
   - Standard font sizes: `text-xs` (12px) for table data and inputs; `text-sm` (14px) for card body text; `text-base`/`text-lg` for section titles.
   - Action buttons standard height: `h-8` or `h-8.5`.

---

## 7. The 19 Atomic Design Primitives (`@/components/ui/`)

All reusable UI components reside in `frontend-v2/src/components/ui/` and export via `index.ts`:

1. `<Button>`: Variants (`primary`, `secondary`, `outline`, `ghost`, `danger`, `success`), sizes (`sm`, `md`, `lg`), loading spinner, `rounded-sm`/`rounded-md`, Lucide icon slots.
2. `<Badge>`: Categories (`contract`, `financial`, `official`, `hr`, `operations`), formats (`pdf`, `docx`, `xlsx`, `html`), status (`success`, `failed`, `pending`, `idle`), `rounded-sm`.
3. `<CardBlock>`: Enterprise card container (`bg-surface border border-border rounded-sm p-4`).
4. `<StatBlock>`: Metric tile with quantitative value (`text-2xl font-bold`), trend badge, and category icon.
5. `<Input>`: Left icon slot, clear button (`onClear`), Zod error display (`error={errors.name?.message}`), `rounded-sm`.
6. `<Select>`: Strongly typed options, chevron icon, Zod error display, compact padding.
7. `<Modal>`: Accessible dialog overlay, backdrop blur, `Escape` key and outside click dismissal, `rounded-md`.
8. `<Table>`: High-density data grid, sticky header, hover row styles, sortable headers.
9. `<Pagination>`: Previous/Next, page index numbers, total item counter, page size selector.
10. `<Tabs>`: Active tab indicator (`border-b-2 border-primary text-primary`), optional item count badges.
11. `<CodeBlock>`: Syntax-highlighted code/JSON viewer, monospace font, 1-click clipboard copy with temporary `Check` confirmation.
12. `<EmptyState>`: Centered Lucide icon, title, helper text, optional primary CTA button.
13. `<Toast>`: Floating notifications, fixed bottom-right, auto-dismiss (3000ms), status variants.
14. `<Toolbar>`: Search input slot, category dropdown filters, format filter pills, right-aligned CTA.
15. `<Dropdown>`: Contextual menu with bottom-left or bottom-right anchoring, action group dividers, danger styling.
16. `<PdfPreviewPanel>`: Embedded iframe with Blob URL, full-screen toggle, download action, print action, APM telemetry badges.
17. `<Pill>`: Lightweight interactive filter/tag pill with category color accents.
18. `<PageHeader>`: Page title (`text-xl font-bold`), subtitle, breadcrumbs, right-aligned action buttons.
19. `<RequireRole>`: Declarative RBAC gate wrapper checking session against required `UserRole`.

</design_tokens_scope>

<verification_gateway>
- [ ] Base background strictly uses Ice-White (`#f8fbfe`)
- [ ] Border radii constrained to 0px, 2px (`rounded-sm`), 4px (`rounded-md`)
- [ ] No `rounded-full` pills or `rounded-xl` containers
- [ ] Iconography exclusively imports from `lucide-react`
- [ ] Primary buttons use Sky Blue (`#0284c7`), hover (`#0369a1`)
- [ ] Card primary CTA aligned bottom-right; context menu aligned bottom-left
</verification_gateway>
