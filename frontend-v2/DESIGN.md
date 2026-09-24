# DESIGN.md — Single Source of Truth (SSoT) Design Tokens

Design specifications and token dictionary for **SAMMAKORN Document Platform (`frontend-v2`)**.
All visual and geometric parameters MUST strictly adhere to this document. **Zero arbitrary hardcoded colors or radii are permitted in code.**

---

## 🏛️ 1. Geometry & Spatial Standards

- **Border Radius:** Strictly **2px to 4px** (`--radius-sm: 2px`, `--radius-md: 4px`).
  - *No bubble, pill, or circular rounded shapes.*
  - Buttons, inputs, modals, cards, badges, and tabs must all use geometric 2–4px radius.
- **Density & Scale:** High-Density Enterprise Dashboard.
  - Standard action button height: `h-8` (32px).
  - Standard input field height: `h-8` (32px).
  - Compact text hierarchy: `text-[11px]` (micro-labels), `text-xs` (12px), `text-sm` (14px).
  - Headings: `text-sm` (subsections), `text-base` (section titles), `text-lg` (page titles).

---

## 🎨 2. Palette & Canvas (ขาวอมฟ้า / Ice-White Canvas)

### Base Backgrounds & Surfaces
| Token | CSS Variable | Hex Code | Purpose |
|---|---|---|---|
| `canvas` | `--bg-canvas` | `#f8fbfe` | Main application background (Light Ice-Blue tint) |
| `surface` | `--bg-surface` | `#ffffff` | Elevated cards, sidebars, modal surfaces |
| `surface-subtle` | `--bg-surface-subtle` | `#f1f5f9` | Table headers, secondary hover states, input fill |
| `border` | `--border-subtle` | `#e2e8f0` | 1px crisp card and separator borders |
| `border-focus` | `--border-focus` | `#bae6fd` | Focused input borders, active item highlights |

### Text & Typography
| Token | CSS Variable | Hex Code | Purpose |
|---|---|---|---|
| `text-primary` | `--text-primary` | `#0f172a` | High-contrast main headings and table data |
| `text-secondary` | `--text-secondary` | `#475569` | Body text, form labels, descriptions |
| `text-muted` | `--text-muted` | `#94a3b8` | Placeholders, inactive icons, timestamps |

---

## 🌈 3. Document Category Colorful Accents (NO Navy)

Each document category uses a designated vibrant functional color pair:

| Category | Token Identifier | Primary Accent | Light Tint | Border | Badge Text | Target Documents |
|---|---|---|---|---|---|---|
| **Contract / Legal** | `cat-contract` | `#6366f1` (Indigo) | `#eef2ff` | `#c7d2fe` | `#4338ca` | สัญญาจะซื้อจะขาย, สัญญาจ้าง, บันทึกข้อตกลง |
| **Financial** | `cat-financial` | `#10b981` (Emerald) | `#ecfdf5` | `#a7f3d0` | `#065f46` | ใบแจ้งหนี้, ใบเสร็จรับเงิน, หนังสือรับรองภาษี |
| **Official Letter** | `cat-official` | `#0284c7` (Sky Blue) | `#f0f9ff` | `#bae6fd` | `#0369a1` | หนังสือแจ้งโอน, จดหมายบอกกล่าว, ประกาศ |
| **HR / Personnel** | `cat-hr` | `#f59e0b` (Amber) | `#fffbeb` | `#fde68a` | `#92400e` | สัญญาจ้างงาน, ใบรับรองเงินเดือน, ใบเตือน |
| **Operations** | `cat-operations` | `#0891b2` (Cyan) | `#ecfeff` | `#a5f3fc` | `#155e75` | ใบส่งมอบห้องชุด, แบบฟอร์มตรวจรับบ้าน |

---

## ⚡ 4. Semantic Actions & Statuses

| State | Primary Color | Soft Background | Border | Purpose |
|---|---|---|---|---|
| **Primary Action** | `#0284c7` (Sky-600) | `#f0f9ff` | `#38bdf8` | Main buttons, active sidebar tabs |
| **Success** | `#10b981` (Emerald-500) | `#ecfdf5` | `#6ee7b7` | Generation SUCCESS, valid signatures |
| **Danger / Error** | `#ef4444` (Rose-500) | `#fef2f2` | `#fca5a5` | Revoke keys, generation FAILED, delete |
| **Warning** | `#f59e0b` (Amber-500) | `#fffbeb` | `#fcd34d` | Missing required fields, draft status |
| **Pending / Info** | `#3b82f6` (Blue-500) | `#eff6ff` | `#93c5fd` | In-progress generation, tooltips |

---

## 🔤 5. Typography

- **Font Family:** `'Sarabun', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif`
- **Thai Word-Breaking:** Enabled via Sarabun font and native CSS line-break rules.
- **Tabular Numbers:** Numbers in tables, metrics, and dates MUST use `font-mono tabular-nums`.

---

## 🎯 6. Iconography Rules

- **Strict Standard:** **Lucide icons ONLY** (`lucide-react`).
- **Standard Sizes:**
  - Compact button / row: `w-3.5 h-3.5` (14px) or `w-4 h-4` (16px).
  - KPI Stat block: `w-5 h-5` (20px) inside a `w-8 h-8` soft-colored box.
  - Large modal / empty state: `w-8 h-8` (32px).
