# [FRONT-002]: 02_ui_component_conventions.md — Design Tokens, UI Primitives & Zod SSoT

<context>
Scope: UI Component Conventions, Ice-White Design Tokens, CVA Component Variants, Lucide Icons, Zod Schemas & Vitest Component Testing.
SSoT_For: Frontend Component Design System, Tailwind CSS Tokens, Radix Primitives, Form Validation Schemas, and Vitest Component Testing.
Out_Of_Scope: Next.js App Router data fetching and proxy architecture (see: 01_app_router_and_data_fetching.md), Backend API presentation (see: ../01_clean_arch_patterns/04_api_presentation_layer.md).
Target: `frontend-v2/` (`src/components/ui/`, `src/features/*/schemas/`, `src/__tests__/`).
ArchitecturalSignals: [ARCH_EVOLUTION_FRONTEND_UI]
</context>

---

## 1. Level 1: UI Component & Design System Invariants Matrix

### 1.1 Architectural Wiring & Design Contracts
| Direction | Upstream Origin | Current Role (UI Primitive Engine) | Downstream Destination |
|---|---|---|---|
| **Design Tokens** | CSS Custom Properties (`:root`) | Defines ice-white surfaces and slate typography | Tailwind CSS Theme Configuration |
| **Component Variants**| Caller Props (`variant`, `size`) | Dispatches variant classes via CVA | DOM HTML Attributes (`className`) |
| **Form Inputs** | User Keystrokes | Validates shape at runtime via Zod schema | React Hook Form State |
| **Icons** | Lucide React Icon Package | Consistent 16px/18px stroke rendering | UI Buttons, Headers & Status Badges |
| **Test Verification** | Production Schemas (`@/features/*/schemas/`) | Co-located Vitest execution (`src/__tests__/`) | Quality Gate CI Pipeline |

### 1.2 Affirmative UI Component & Design System Invariants
| UI & Design System Concern | Affirmative Frontend Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Compact Enterprise Geometry** | UI components adhere strictly to compact radii: `0px` (`rounded-none`), `2px` (`rounded-sm`), and `4px` (`rounded-md`). Consumer geometries (`rounded-xl`, `rounded-2xl`) are prohibited. | Border Geometry |
| **Curated Design Tokens** | Surface backdrops, borders, and text colors consume semantic CSS custom properties or standard Tailwind utility classes. Hardcoded arbitrary hex values are prohibited. | Theme Tokens |
| **Type-Safe Component Variants**| UI primitive styling is managed strictly through `class-variance-authority` (CVA) variant definitions. Ad-hoc template string conditional styles are prohibited. | Component Styling |
| **Standardized Iconography** | Icons are imported exclusively from `lucide-react` with fixed `size={16}` or `size={18}` strokes. Inlining raw `<svg>` elements or mixing external icon packages is prohibited. | Icon Assets |
| **SSoT Validation Schemas** | Form and payload validation schemas reside in `src/features/{slice}/schemas/` and are imported into both UI forms and Vitest specs. Redefining duplicate schemas in tests is prohibited. | Schema Integrity |
| **Co-Located Test Directory** | Frontend unit and schema tests reside strictly under `frontend-v2/src/__tests__/` matching source structure. Separate or isolated `src/tests/` folders are prohibited. | Test Organization |
| **Controlled UI Primitives** | Interactive elements wrap accessible Radix/CVA primitives (`<Button>`, `<Input>`, `<Card>`). Bare unstyled HTML form elements are prohibited. | DOM Primitives |

---

## 2. Level 2: Core Design System SSoT ASTs & Cognitive UI Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Compact Enterprise Geometry (<= 4px):** Enterprise document workflows require dense, information-rich viewports; consumer `rounded-2xl` cards waste substantial screen real estate and reduce professional visual cohesion.
* **Why Ice-White Background (`#f8fbfe`):** A subtle cool undertone distinguishes active white card surfaces (`#ffffff`) from screen canvas without harsh contrast or eye strain.
* **Why Class Variance Authority (CVA):** Eliminates messy ternary string concatenations, providing type-safe compile-time variant combinations with automatic default fallbacks.
* **Why Single Source of Truth Zod Schemas:** Form validation schemas must match backend request rules; importing the same schema into tests guarantees zero test-drift and eliminates tautological tests.

### 2.2 Proactive UI Guards
```yaml
CompactGeometryGuard:
  Invariant: "UI primitives must use rounded-none, rounded-sm, or rounded-md."
  ActionOnViolation: "Replace rounded-lg, rounded-xl, or rounded-2xl with rounded-md."

LucideIconStandardGuard:
  Invariant: "Icons must be imported from lucide-react with fixed size."
  ActionOnViolation: "Replace inline <svg> with standardized Lucide React icon component."

ZodSchemaSourceGuard:
  Invariant: "Tests must import production Zod schemas from @/features/*/schemas/."
  ActionOnViolation: "Remove local duplicate z.object() and import SSoT schema."

SemanticColorTokenGuard:
  Invariant: "Tailwind classes must use semantic tokens rather than arbitrary hex."
  ActionOnViolation: "Replace bg-[#...] with bg-slate-* or text-sky-* tokens."
```

### 2.3 Ice-White Design Token Palette
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

### 2.4 Compact Enterprise Geometry Rules
| Geometry Token | Tailwind Class | Application Scope | Prohibited Alternatives |
|---|---|---|---|
| **`0px`** | `rounded-none` | Split-screen panels, monaco editor containers | - |
| **`2px`** | `rounded-sm` | Badges, tags, table action buttons | Large rounded tags |
| **`4px`** | `rounded-md` | Cards, modals, text inputs, primary buttons | `rounded-xl`, `rounded-2xl` |
| **Pills** | `rounded-full` | Strictly restricted to avatar icons & circular badges | Full-pill buttons or input fields |

---

## 3. Level 3: Canonical Component & Schema Archetypes (Surgical AST Recipes)

### 3.1 CVA Component Variant Archetype (`Button.tsx`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-UI-001]:
* Defines UI primitive variants using `class-variance-authority` (CVA).
* Uses Compact Enterprise Geometry (`rounded-md`); merges caller `className` via `cn()`.
* Forwards `ref` via `React.forwardRef` and declares explicit `displayName`.

```typescript
import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/utils/cn';

// Step 1: Define CVA Variant Schema <Why: type-safe styling without string concatenation>
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

// Step 2: Implement Component with ForwardRef <Why: enable seamless form hook and focus integration>
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

### 3.2 CVA Input Primitive Archetype (`Input.tsx`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-UI-002]:
* Applies compact enterprise 4px border radius (`rounded-md`), subtle borders (`border-slate-300`), and accessible focus rings.
* Integrates with `cn()` utility and forwards `ref`.

```typescript
import * as React from 'react';
import { cn } from '@/utils/cn';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    // Step 1: Render Styled Input Primitive <Why: unified focus states and compact enterprise padding>
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

### 3.3 Enterprise Card Container Archetype (`Card.tsx`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-UI-003]:
* Encapsulates container geometry with `rounded-md`, `border-slate-200`, and clean surface card backgrounds.

```typescript
import * as React from 'react';
import { cn } from '@/utils/cn';

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

### 3.4 SSoT Zod Validation Schema Archetype (`template.schema.ts`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-UI-004]:
* Defines form and payload validation schemas in `src/features/{feature}/schemas/` as Single Source of Truth.
* Exports both runtime Zod schema (`*Schema`) and derived TypeScript type (`z.infer<typeof *Schema>`).

```typescript
import { z } from 'zod';

// Step 1: Reusable Field Rules <Why: consistent slug regex and length constraints across app>
export const slugSchema = z
  .string()
  .min(2, 'Slug must be at least 2 characters')
  .max(100, 'Slug must not exceed 100 characters')
  .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Slug must be lowercase alphanumeric and hyphens');

// Step 2: Composite Request Schema <Why: runtime validation matching backend OpenAPI contract>
export const Create{Aggregate}RequestSchema = z.object({
  name: z.string().min(1, '{Aggregate} name is required').max(100),
  slug: slugSchema,
  category: z.string().max(50).optional().nullable(),
  htmlContent: z.string().optional().nullable(),
});

// Step 3: Infer Static TypeScript Type <Why: compile-time type safety derived from runtime schema>
export type Create{Aggregate}Request = z.infer<typeof Create{Aggregate}RequestSchema>;
```

### 3.5 Co-Located Vitest Spec Archetype (`src/__tests__/schemas.test.ts`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-UI-005]:
* Co-locates tests strictly under `frontend-v2/src/__tests__/`.
* Imports real production schemas from `@/features/*/schemas/` to eliminate test drift.

```typescript
import { describe, it, expect } from 'vitest';
import { Create{Aggregate}RequestSchema } from '@/features/{feature}/schemas/{aggregate}.schema';

describe('Create{Aggregate}RequestSchema validation', () => {
  it('should accept valid payload', () => {
    // Arrange: Valid payload matching schema
    const validData = {
      name: 'Sample Name',
      slug: 'sample-slug',
      category: 'General',
    };

    // Act: Validate via SSoT schema
    const result = Create{Aggregate}RequestSchema.safeParse(validData);

    // Assert: Must pass validation
    expect(result.success).toBe(true);
  });

  it('should reject invalid uppercase characters in slug', () => {
    // Arrange: Invalid slug with uppercase characters
    const invalidData = {
      name: 'Sample Name',
      slug: 'INVALID_SLUG',
    };

    // Act & Assert
    const result = Create{Aggregate}RequestSchema.safeParse(invalidData);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].path).toContain('slug');
    }
  });

  it('should fail when name is empty', () => {
    // Arrange: Empty required name field
    const invalidData = {
      name: '',
      slug: 'valid-slug',
    };

    // Act & Assert
    const result = Create{Aggregate}RequestSchema.safeParse(invalidData);
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
+ import { Create{Aggregate}RequestSchema } from '@/features/{feature}/schemas/{aggregate}.schema';
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
+ <Input placeholder="Enter name..." value={name} onChange={handleChange} />
```
*Rationale: Raw HTML inputs lack standardized enterprise focus rings, accessible disabled states, and compact geometry tokens.*
