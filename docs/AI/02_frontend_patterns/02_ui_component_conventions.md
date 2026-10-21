# [FRONT-002]: 02_ui_component_conventions.md — Design Tokens, UI Primitives & Zod SSoT

<context>
Scope: UI Component Conventions, Ice-White Design Tokens, CVA Component Variants, Lucide Icons, Zod Schemas & Vitest
Target: `frontend-v2/` (`src/components/ui/`, `src/schemas/`, `src/__tests__/`)
Authority: Absolute SSoT Invariant (Deterministic compact enterprise UI, zero raw HTML inputs, zero decorative emojis)
</context>

---

## 1. Design Philosophy & Ice-White Token Palette

SmkDoc Portal adopts the **Ice-White Compact Enterprise Design System**: ultra-crisp white surfaces with subtle cool-slate borders and sky-blue brand accents.

```css
:root {
  /* Surface & Background */
  --canvas-bg: #f8fbfe;         /* Primary screen backdrop */
  --surface-card: #ffffff;      /* Cards, tables, modals */
  --border-subtle: #e2e8f0;     /* Default card & divider borders */
  --border-strong: #cbd5e1;     /* Input borders, active states */

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

---

## 2. Compact Enterprise Geometry Rules

| Geometry Token | Tailwind Class | Application Scope | Prohibited Alternatives (! BANNED) |
|---|---|---|---|
| **`0px`** | `rounded-none` | Split-screen panels, monaco editor containers | - |
| **`2px`** | `rounded-sm` | Badges, tags, table action buttons | Large rounded tags |
| **`4px`** | `rounded-md` | Cards, modals, text inputs, primary buttons | ! `rounded-xl`, `rounded-2xl` |
| **Pills** | `rounded-full` | Strictly restricted to avatar icons & circular badges | ! Full-pill buttons or input fields |

---

## 3. CVA Component Variant Archetype (`Button.tsx`)

Every atomic UI component in `src/components/ui/` uses `class-variance-authority` (CVA) for variant management:

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

---

## 4. Iconography Standard

Strictly import icons from `lucide-react`. Zero third-party icon packages, font icons, or emojis permitted:

```typescript
// >> CANONICAL: Crisp, tree-shakeable Lucide icons
import { FileText, Download, CheckCircle, AlertTriangle, RefreshCw } from 'lucide-react';

<Button variant="outline" size="sm">
  <Download className="mr-2 h-4 w-4" /> Download PDF
</Button>
```

---

## 5. Zod Validation SSoT (`src/schemas/`)

Form validation schemas reside strictly in `src/schemas/`. TypeScript types are inferred directly from schemas:

```typescript
import { z } from 'zod';

export const createTemplateSchema = z.object({
  name: z.string().min(1, 'Template name is required').max(100),
  slug: z.string().min(1, 'Slug is required').max(100).regex(/^[a-z0-9-]+$/, 'Alphanumeric lowercase and hyphens only'),
  category: z.string().max(50).optional(),
});

export type CreateTemplateFormValues = z.infer<typeof createTemplateSchema>;
```

---

## 6. Frontend Unit Testing Standard (`src/__tests__/`)

All tests must reside in `src/__tests__/` (directory `src/tests/` is strictly prohibited). Tests must import real Zod schemas:

```typescript
import { describe, it, expect } from 'vitest';
import { createTemplateSchema } from '@/schemas/template.schema';

describe('createTemplateSchema', () => {
  it('should accept valid template payload', () => {
    const valid = { name: 'Tax Invoice', slug: 'tax-invoice-v1', category: 'Finance' };
    const result = createTemplateSchema.safeParse(valid);
    expect(result.success).toBe(true);
  });

  it('should reject invalid slug with uppercase letters', () => {
    const invalid = { name: 'Tax Invoice', slug: 'TAX_INVOICE' };
    const result = createTemplateSchema.safeParse(invalid);
    expect(result.success).toBe(false);
  });
});
```
