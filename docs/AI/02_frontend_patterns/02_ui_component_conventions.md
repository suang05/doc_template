# [FRONT-002]: 02_ui_component_conventions.md — Design Tokens, UI Primitives & Zod SSoT

<context>
Scope: UI Component Conventions, Ice-White Design Tokens, CVA Component Variants, Lucide Icons, Zod Schemas & Vitest
Target: `frontend-v2/` (`src/components/ui/`, `src/schemas/`, `src/__tests__/`)
Authority: Absolute SSoT Invariant (Deterministic compact enterprise UI, zero raw HTML inputs, zero decorative emojis)
Boundary: Pure Architectural AST only. Prohibit task roadmaps, execution checklists, and conversational prose.
TriggerSignals: [SIGNAL_UI_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: UI Component & Design System Invariants Matrix

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Border Radii** | Arbitrary or large rounding: `rounded-xl`, `rounded-2xl` | Compact Enterprise Geometry: `0px` (`rounded-none`), `2px` (`rounded-sm`), `4px` (`rounded-md`) |
| **Color Tokens** | Hardcoded arbitrary hex: `bg-[#0284c7]` | Curated Ice-White design tokens or Tailwind semantic utility classes |
| **Component Variants** | Ad-hoc template string conditional styles | `class-variance-authority` (CVA) variant definitions |
| **Icon Set** | Raw inline `<svg>` elements or inconsistent icon packages | Standard Lucide React icons (`lucide-react`) with fixed `size={16}` or `size={18}` |
| **Validation Schemas** | Defining duplicated test-local Zod schemas in tests | Single Source of Truth Zod schemas imported from `@/schemas/` |
| **Test Co-location** | Creating isolated `src/tests/` directory | Strictly co-located in `frontend-v2/src/__tests__/` matching source structure |

---

## 2. Level 2: Core Design System SSoT ASTs & Tokens

### Ice-White Design Token Palette
```css
:root {
  /* Surface & Background */
  --canvas-bg: #f8fbfe;         /* Primary screen backdrop */
  --surface-card: #ffffff;      /* Cards, tables, modals */
  --border-subtle: #e2e8f0;     /* Default card & divider borders (Slate-200) */
  --border-strong: #cbd5e1;     /* Input borders, active states (Slate-300) */

  /* Typography */
  --text-primary: #0f172a;      /* Headings, primary labels (Slate-900) */
  --text-secondary: #475569;    /* Descriptions, table body (Slate-600) */
  --text-muted: #94a3b8;        /* Placeholders, disabled states (Slate-400) */

  /* Brand Accents */
  --primary-brand: #0284c7;     /* Sky-600: Primary actions, links */
  --primary-hover: #0369a1;     /* Sky-700: Hover states */
  --primary-soft: #f0f9ff;      /* Sky-50: Active nav, badge background */

  /* Semantic Feedback */
  --status-success: #16a34a;    /* Emerald-600 */
  --status-warning: #d97706;    /* Amber-600 */
  --status-error: #dc2626;      /* Red-600 */
}
```

### Compact Enterprise Geometry Rules
| Geometry Token | Tailwind Class | Application Scope | Prohibited Alternatives (! BANNED) |
|---|---|---|---|
| **`0px`** | `rounded-none` | Split-screen panels, monaco editor containers | - |
| **`2px`** | `rounded-sm` | Badges, tags, table action buttons | Large rounded tags |
| **`4px`** | `rounded-md` | Cards, modals, text inputs, primary buttons | ! `rounded-xl`, `rounded-2xl` |
| **Pills** | `rounded-full` | Strictly restricted to avatar icons & circular badges | ! Full-pill buttons or input fields |

---

## 3. Level 3: Canonical Component & Schema Archetypes

### CVA Component Variant Archetype (`Button.tsx`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-001]:
* [MUST] Define UI primitive variants using `class-variance-authority` (CVA).
* [MUST] Use Compact Enterprise Geometry (`rounded-none`, `rounded-sm`, `rounded-md`); prohibit `rounded-xl` and `rounded-2xl`.
* [MUST] Integrate `cn()` helper (`clsx` + `tailwind-merge`) to merge caller-provided `className`.
* [MUST] Forward refs via `React.forwardRef` and declare explicit `displayName`.
```typescript
import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

export const buttonVariants = cva(
  'inline-flex items-center justify-center font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 disabled:pointer-events-none disabled:opacity-50 rounded-md',
  {
    variants: {
      variant: {
        primary: 'bg-sky-600 text-white hover:bg-sky-700 shadow-sm',
        secondary: 'bg-slate-100 text-slate-800 hover:bg-slate-200 border border-slate-200',
        destructive: 'bg-red-600 text-white hover:bg-red-700 shadow-sm',
        outline: 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50',
        ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
      },
      size: {
        sm: 'h-8 px-2.5 text-xs',
        md: 'h-9 px-4 text-sm',
        lg: 'h-10 px-6 text-sm',
        icon: 'h-9 w-9 p-0',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, ...props }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(buttonVariants({ variant, size, className }))}
        {...props}
      />
    );
  }
);
Button.displayName = 'Button';
```

### SSoT Zod Validation Schema Archetype (`template.schema.ts`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-002]:
* [MUST] Define form and payload validation schemas in `src/schemas/` as Single Source of Truth.
* [MUST] Export both runtime Zod schema (`*Schema`) and derived TypeScript type (`z.infer<typeof *Schema>`).
* [MUST NOT] Redefine or duplicate Zod schemas locally inside unit test files.
```typescript
import { z } from 'zod';

export const templateSchema = z.object({
  name: z.string().min(1, 'Template name is required').max(100),
  slug: z.string().min(1, 'Slug is required').max(100).regex(/^[a-z0-9-]+$/, 'Lowercase letters, numbers, and hyphens only'),
  category: z.string().max(50).optional(),
});

export type TemplateFormData = z.infer<typeof templateSchema>;
```

---

## 4. Level 4: Anti-Patterns & Micro-Diffs

### [TRAP-UI-001]: Geometry: Bloated Rounded Corners
```typescript
// ! BANNED: Consumer rounded geometries break compact enterprise aesthetic
<button className="rounded-2xl p-4">Submit</button>

// >> CANONICAL: 4px rounded compact geometry
<button className="rounded-md px-4 py-2 text-sm">Submit</button>
```

### [TRAP-UI-002]: Icon Duplication: Raw SVG vs Lucide React
```typescript
// ! BANNED: Inline raw SVG elements bloat bundle and cause visual divergence
<svg className="w-4 h-4"><path d="..." /></svg>

// >> CANONICAL: Consistent Lucide icon imports
import { FileText } from 'lucide-react';
<FileText className="w-4 h-4 text-slate-500" />
```

### [TRAP-UI-003]: Test-Local Zod Schema Duplication
```typescript
// ! BANNED: Tautological test declaring its own local duplicate schema
// src/__tests__/template.test.ts
const localSchema = z.object({ name: z.string() }); // REJECTED!

// >> CANONICAL: Real production module import
import { templateSchema } from '@/schemas/template.schema';
```
