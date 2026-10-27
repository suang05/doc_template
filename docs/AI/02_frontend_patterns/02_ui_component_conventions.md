# [FRONT-002]: 02_ui_component_conventions.md — Design Tokens, UI Primitives & Zod SSoT

<context>
Scope: UI Component Conventions, Ice-White Design Tokens, CVA Component Variants, Lucide Icons, Zod Schemas & Vitest
Target: `frontend-v2/` (`src/components/ui/`, `src/schemas/`, `src/__tests__/`)
Authority: Absolute SSoT Invariant (Deterministic compact enterprise UI, zero raw HTML inputs, zero decorative emojis)
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_UI_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: UI Component & Design System Invariants Matrix

### Architectural Wiring & Design Contracts
| Direction | Upstream Origin | Current Role (UI Primitive Engine) | Downstream Destination |
|---|---|---|---|
| **Design Tokens** | CSS Custom Properties (`:root`) | Defines ice-white surfaces and slate typography | Tailwind CSS Theme Configuration |
| **Component Variants**| Caller Props (`variant`, `size`) | Dispatches variant classes via CVA | DOM HTML Attributes (`className`) |
| **Form Inputs** | User Keystrokes | Validates shape at runtime via Zod schema | React Hook Form State |
| **Icons** | Lucide React Icon Package | Consistent 16px/18px stroke rendering | UI Buttons, Headers & Status Badges |
| **Test Verification** | Production Schemas (`@/schemas/`) | Co-located Vitest execution (`src/__tests__/`) | Quality Gate CI Pipeline |

### UI Component Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Border Radii** | Arbitrary or large rounding: `rounded-xl`, `rounded-2xl` | Compact Enterprise Geometry: `0px` (`rounded-none`), `2px` (`rounded-sm`), `4px` (`rounded-md`) |
| **Color Tokens** | Hardcoded arbitrary hex: `bg-[#0284c7]` | Curated Ice-White design tokens or Tailwind semantic utility classes |
| **Component Variants** | Ad-hoc template string conditional styles | `class-variance-authority` (CVA) variant definitions |
| **Icon Set** | Raw inline `<svg>` elements or inconsistent icon packages | Standard Lucide React icons (`lucide-react`) with fixed `size={16}` or `size={18}` |
| **Validation Schemas** | Defining duplicated test-local Zod schemas in tests | Single Source of Truth Zod schemas imported from `@/schemas/` |
| **Test Co-location** | Creating isolated `src/tests/` directory | Strictly co-located in `frontend-v2/src/__tests__/` matching source structure |
| **Raw HTML Inputs** | Direct `<input>` or `<button>` without styling wrapper | Controlled CVA UI primitives (`<Input>`, `<Button>`, `<Card>`) |

---

## 2. Level 2: Core Design System SSoT ASTs & Cognitive UI Engine

### Engineering Rationale & Architectural Decisions
* **Why Compact Enterprise Geometry (<= 4px):** Enterprise document workflows require dense, information-rich viewports; consumer `rounded-2xl` cards waste substantial screen real estate and reduce professional visual cohesion.
* **Why Ice-White Background (`#f8fbfe`):** A subtle cool undertone distinguishes active white card surfaces (`#ffffff`) from screen canvas without harsh contrast or eye strain.
* **Why Class Variance Authority (CVA):** Eliminates messy ternary string concatenations, providing type-safe compile-time variant combinations with automatic default fallbacks.
* **Why Single Source of Truth Zod Schemas:** Form validation schemas must match backend request rules; importing the same schema into tests guarantees zero test-drift and eliminates tautological tests.

### Cognitive UI Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Dense Information Architecture, Unified Enterprise Aesthetics & Strict Contract Fidelity
  TradeOffPriority: VisualCohesion > TypographicClarity > ComponentReusability > CustomStylingFreedom

ProactiveUiGuards:
  - Scope: frontend-v2.src.components
    Trigger: Declaration of rounded-lg, rounded-xl, or rounded-2xl on UI primitives
    CanonicalAction: Enforce Compact Enterprise Geometry (rounded-none, rounded-sm, or rounded-md) >> Ref: [RULE-UI-001]

  - Scope: frontend-v2.src.components
    Trigger: Use of raw inline <svg> instead of Lucide React icon
    CanonicalAction: Import standard icon from lucide-react with size={16} or size={18} >> Ref: [RULE-UI-002]

  - Scope: frontend-v2.src.__tests__
    Trigger: Inline duplicate z.object() declared inside test spec
    CanonicalAction: Import SSoT schema directly from @/schemas/ >> Ref: [RULE-UI-003]

  - Scope: frontend-v2.src
    Trigger: Hardcoded hex color codes in className (e.g. bg-[#...])
    CanonicalAction: Substitute with Tailwind semantic token (bg-slate-*, text-sky-*, etc.)

CoreExtensionSlots:
  PrimitiveButtonSlot: Button (CVA-managed action primitive)
  PrimitiveInputSlot: Input (CVA-managed text field with focus rings)
  PrimitiveCardSlot: Card / CardHeader / CardContent (Container primitive)
  ValidationSchemaSlot: *Schema (Zod runtime and inferred TypeScript type)
  VitestSpecSlot: __tests__/* (Co-located Vitest behavioral assertions)
```

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

### CVA Input Primitive Archetype (`Input.tsx`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-002]:
* [MUST] Apply compact enterprise 4px border radius (`rounded-md`), subtle borders (`border-slate-300`), and accessible sky focus rings.
* [MUST] Integrate with `cn()` utility and forward `ref`.
```typescript
import * as React from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          'flex h-9 w-full rounded-md border border-slate-300 bg-white px-3 py-1 text-sm shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-sky-500 disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Input.displayName = 'Input';
```

### Enterprise Card Container Archetype (`Card.tsx`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-003]:
* [MUST] Encapsulate container geometry with `rounded-md`, `border-slate-200`, and clean surface card backgrounds.
```typescript
import * as React from 'react';
import { cn } from '@/lib/utils';

export const Card = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('rounded-md border border-slate-200 bg-white shadow-sm', className)} {...props} />
  )
);
Card.displayName = 'Card';

export const CardHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('flex flex-col space-y-1.5 p-6 pb-4', className)} {...props} />
  )
);
CardHeader.displayName = 'CardHeader';

export const CardTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h3 ref={ref} className={cn('font-semibold leading-none tracking-tight text-slate-900', className)} {...props} />
  )
);
CardTitle.displayName = 'CardTitle';

export const CardDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, ...props }, ref) => (
    <p ref={ref} className={cn('text-xs text-slate-500', className)} {...props} />
  )
);
CardDescription.displayName = 'CardDescription';

export const CardContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('p-6 pt-0', className)} {...props} />
  )
);
CardContent.displayName = 'CardContent';

export const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('flex items-center p-6 pt-0', className)} {...props} />
  )
);
CardFooter.displayName = 'CardFooter';
```

### SSoT Zod Validation Schema Archetype (`template.schema.ts`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-004]:
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

### Co-Located Vitest Spec Archetype (`src/__tests__/schemas.test.ts`)
>> MANDATORY CANONICAL TEMPLATE [RULE-UI-005]:
* [MUST] Co-locate tests strictly under `frontend-v2/src/__tests__/`.
* [MUST] Import real production schemas from `@/schemas/` to eliminate test drift.
```typescript
import { describe, it, expect } from 'vitest';
import { templateSchema } from '@/schemas/template.schema';

describe('templateSchema validation', () => {
  it('should accept valid template payload', () => {
    const validData = {
      name: 'Invoice Template',
      slug: 'invoice-template',
      category: 'Billing',
    };

    const result = templateSchema.safeParse(validData);
    expect(result.success).toBe(true);
  });

  it('should reject invalid uppercase characters in slug', () => {
    const invalidData = {
      name: 'Invoice Template',
      slug: 'INVOICE_TEMPLATE',
    };

    const result = templateSchema.safeParse(invalidData);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].path).toContain('slug');
    }
  });

  it('should fail when template name is empty', () => {
    const invalidData = {
      name: '',
      slug: 'valid-slug',
    };

    const result = templateSchema.safeParse(invalidData);
    expect(result.success).toBe(false);
  });
});
```

---

## 4. Level 4: High-Salience Fatal Traps & Unified Micro-Diffs

### [TRAP-UI-001]: Bloated Rounded Corners
```diff
- <button className="rounded-2xl p-4">Submit</button>
+ <button className="rounded-md px-4 py-2 text-sm">Submit</button>
```
*Rationale: Consumer rounded geometries break compact enterprise visual hierarchy and waste density in tabular screens.*

### [TRAP-UI-002]: Raw SVG Icon Duplication
```diff
- <svg className="w-4 h-4"><path d="..." /></svg>
+ <FileText className="w-4 h-4 text-slate-500" />
```
*Rationale: Inlining raw SVG elements bloats client bundles, violates DRY, and creates visual style mismatches.*

### [TRAP-UI-003]: Test-Local Duplicate Zod Schemas
```diff
- const localSchema = z.object({ name: z.string() }); // REJECTED: Tautological test!
+ import { templateSchema } from '@/schemas/template.schema';
```
*Rationale: Testing against isolated local schemas tests the mock schema rather than production validation rules.*

### [TRAP-UI-004]: Arbitrary Hardcoded Hex Colors
```diff
- <div className="bg-[#0284c7] text-[#ffffff]">Action</div>
+ <div className="bg-sky-600 text-white">Action</div>
```
*Rationale: Hardcoded hex codes bypass theme tokens, creating maintainability barriers during dark-mode or re-theming updates.*

### [TRAP-UI-005]: Unwrapped Raw HTML Inputs
```diff
- <input type="text" className="p-2 border" onChange={handleChange} />
+ <Input placeholder="Enter template name..." value={name} onChange={handleChange} />
```
*Rationale: Raw HTML inputs lack standardized enterprise focus rings, accessible disabled states, and compact geometry tokens.*
