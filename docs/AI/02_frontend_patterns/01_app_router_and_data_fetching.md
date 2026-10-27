# [FRONT-001]: 01_app_router_and_data_fetching.md — Next.js 15 App Router & API Proxy SSoT

<context>
Scope: Next.js 15 App Router Architecture, Server/Client Component Boundaries, Route Handler Proxy & Live Preview Pipeline
Target: `frontend-v2/` (`src/app/`, `src/hooks/`, `src/lib/api/`)
Authority: Absolute SSoT Invariant (Zero client-side JWT token access, 100% debounced streaming preview)
Format: 100% English AST, Positive-First Archetypes (80/20 Rule), Canonical Keyboard Operators
TriggerSignals: [SIGNAL_UI_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. Level 1: App Router & Data Fetching Invariants Matrix

### Architectural Wiring & Data Contracts
| Direction | Upstream Origin | Current Role (Next.js Application Gateway) | Downstream Destination |
|---|---|---|---|
| **Client Request** | Browser Client Component | Dispatches fetch to local route handler | Secure Server Proxy (`/api/proxy/[...path]`) |
| **Auth Injection** | NextAuth `httpOnly` Session | Extracts Bearer JWT & Project Context | Upstream Request Headers (`Authorization`, `x-project-id`) |
| **Header Stripping**| Browser Request Headers | Strips hop-by-hop headers (`host`, `connection`, etc.) | Backend API Port (`http://backend:8080/api/v1/`) |
| **Streaming Egress**| Backend Document Stream | Duplex-pipes body stream directly to caller | Browser Client (`Response(backendRes.body)`) |
| **Live Preview** | Template Editor Changes | Debounces (800ms) with `AbortController` cancellation | Blob URL Egress (`URL.revokeObjectURL(old)`) |

### App Router Invariants Matrix
| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Client Component Scope** | Placing `'use client'` at root page or layout | React Server Components (RSC) by default; restrict `'use client'` to leaf interactive components |
| **API Token Storage** | Storing JWT tokens in `localStorage` or browser cookies | Next.js Server Proxy (`/api/proxy/[...path]`) with `httpOnly` session cookies |
| **Direct Backend Calls** | Client browser fetching backend API directly (`http://localhost:8080`) | All client fetch calls routed through `/api/proxy/[...path]` |
| **Live Preview Sync** | Firing preview requests on every keystroke | Debounced (800ms) with `AbortController` cancellation |
| **Blob URL Leaks** | Overwriting preview object URLs without revoking | Immediate cleanup: `URL.revokeObjectURL(previousUrl)` |
| **Route Params (Next.js 15)**| Synchronous params: `{ params: { id } }` | Async Promise params: `{ params }: { params: Promise<{ id: string }> }` + `await params` |
| **Stateful Server Leaves**| Using React hooks (`useState`, `useEffect`) in RSC pages | Split layout into RSC Page wrapper + Client Leaf Widget |

---

## 2. Level 2: Core Frontend Topology & Cognitive Frontend Engine

### Engineering Rationale & Architectural Decisions
* **Why Next.js Server Proxy over Direct Browser Calls:** Keeps backend JWT tokens and internal cluster endpoints completely hidden inside server-side `httpOnly` cookies, eliminating XSS token theft vectors.
* **Why Next.js 15 Async `params`:** Next.js 15 enforces asynchronous route parameters to enable partial prerendering and streaming server components without synchronous blocking.
* **Why 800ms Debounce with AbortController:** Document rendering via Gotenberg is CPU-intensive; 800ms debounce prevents cluster thrashing, while `AbortController` immediately halts stale rendering jobs on the wire.
* **Why Strict `URL.revokeObjectURL` Cleanup:** Browser blob URLs remain retained in browser heap memory until explicitly revoked; omitting cleanup causes substantial memory leaks during prolonged template editing sessions.

### Cognitive Frontend Optimization & Guard Engine
```yaml
OptimizationObjectives:
  PrimaryObjective: Zero-Trust Token Isolation, Sub-Second Live Preview & Lean Client Bundles
  TradeOffPriority: SecurityPurity (Server-Proxy) > MemoryFidelity (Blob Revoke) > RenderingResponsiveness > BoilerplateSimplicity

ProactiveFrontendGuards:
  - Scope: frontend-v2.src.app
    Trigger: Declaration of 'use client' at page.tsx or layout.tsx root
    CanonicalAction: Convert page to async RSC and extract interactive logic to leaf component under _components/ >> Ref: [RULE-FRONT-001]

  - Scope: frontend-v2.src.app
    Trigger: Accessing params synchronously without await in Next.js 15
    CanonicalAction: Type params as Promise<{ [key: string]: string }> and await params before reading properties >> Ref: [RULE-FRONT-002]

  - Scope: frontend-v2.src.hooks
    Trigger: Creating object URL with URL.createObjectURL without revoking previous URL
    CanonicalAction: Enforce URL.revokeObjectURL(previousUrlRef.current) before updating state and on unmount >> Ref: [RULE-FRONT-003]

  - Scope: frontend-v2.src.lib.api
    Trigger: Fetching internal backend port directly from browser runtime
    CanonicalAction: Rewrite URL to route through /api/proxy/[...path] to ensure secure token injection

CoreExtensionSlots:
  ServerProxySlot: /api/proxy/[...path]/route.ts (Duplex streaming reverse proxy)
  LivePreviewHookSlot: useLivePreview (Debounced rendering with abort lifecycle)
  SplitEditorRecipeSlot: TemplateEditorSplitLayout (Monaco editor + PDF iframe layout)
  DataTableRecipeSlot: DataTablePage (Enterprise grid with search & row actions)
  WizardRecipeSlot: WizardLayout (Multi-step form container with stepper)
  DashboardRecipeSlot: MetricsDashboardPage (KPI cards + Chart + Audit log feed)
```

### Hop-by-Hop Stripped Headers
The Next.js reverse proxy MUST strip these headers before forwarding upstream:
```typescript
const HOP_BY_HOP_HEADERS = ['host', 'connection', 'content-length', 'transfer-encoding'];
```

### Component Boundary Classification Matrix
| Component Archetype | Execution Realm | Permitted Capabilities | Prohibited Patterns |
|---|---|---|---|
| `Page / Layout` | Server Component (RSC) | Async data fetching, session resolution | No React hooks (`useState`, `useEffect`), no event listeners |
| `Interactive Widget` | Client Component (`'use client'`) | State, effects, live preview, event listeners | No direct database queries, no secret environment variables |
| `Proxy Route Handler` | Node.js Server Runtime | Token injection, header transformation, streaming | No client rendering, no localStorage access |

---

## 3. Level 3: Canonical Frontend Archetypes

### Secure Server Proxy Route (`/api/proxy/[...path]/route.ts`)
>> MANDATORY CANONICAL TEMPLATE [RULE-FRONT-001]:
* [MUST] Route all client browser API traffic through the Next.js reverse proxy (`/api/proxy/[...path]`).
* [MUST] Strip hop-by-hop headers (`host`, `connection`, `content-length`, `transfer-encoding`) before upstream dispatch.
* [MUST] Inject backend Bearer JWT token strictly on server side from `httpOnly` NextAuth session cookies (prohibit client-side access).
* [MUST] Stream backend response body directly to caller (`new Response(backendRes.body)` with duplex streaming).
```typescript
import { NextRequest } from 'next/server';
import { auth } from '@/auth';

const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

const HOP_BY_HOP_HEADERS = ['host', 'connection', 'content-length', 'transfer-encoding'];

async function proxy(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const session = await auth();

  const targetUrl = `${BACKEND_INTERNAL_URL}/api/${path.join('/')}${req.nextUrl.search}`;

  const headers = new Headers(req.headers);
  for (const name of HOP_BY_HOP_HEADERS) headers.delete(name);

  // Inject backend Bearer token securely from httpOnly session
  if (session?.backendToken) {
    headers.set('Authorization', `Bearer ${session.backendToken}`);
  } else {
    headers.delete('authorization');
  }

  // Inject active tenant ProjectId
  if (!headers.has('x-project-id') && session?.user?.projectId) {
    headers.set('x-project-id', session.user.projectId);
  }

  const hasBody = req.method !== 'GET' && req.method !== 'HEAD';

  const backendRes = await fetch(targetUrl, {
    method: req.method,
    headers,
    body: hasBody ? req.body : undefined,
    duplex: hasBody ? 'half' : undefined,
  } as RequestInit);

  const resHeaders = new Headers(backendRes.headers);
  resHeaders.delete('content-encoding');
  resHeaders.delete('content-length');

  return new Response(backendRes.body, {
    status: backendRes.status,
    headers: resHeaders,
  });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
```

### Next.js 15 Async Route Page Archetype (`src/app/(portal)/templates/[id]/page.tsx`)
>> MANDATORY CANONICAL TEMPLATE [RULE-FRONT-002]:
* [MUST] Treat `params` as `Promise<{ [key: string]: string }>` and `await params` before accessing properties.
* [MUST] Execute initial server-side data fetching inside the RSC page before rendering client leaves.
```tsx
import { auth } from '@/auth';
import { notFound } from 'next/navigation';
import { TemplateEditorClientWidget } from './_components/TemplateEditorClientWidget';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function TemplateDetailsPage({ params }: PageProps) {
  const { id } = await params;
  const session = await auth();

  if (!session?.user) {
    notFound();
  }

  return (
    <div className="flex h-full flex-col">
      <TemplateEditorClientWidget templateId={id} projectId={session.user.projectId} />
    </div>
  );
}
```

### Real-Time Live Preview Pipeline Hook (`useLivePreview.ts`)
>> MANDATORY CANONICAL TEMPLATE [RULE-FRONT-003]:
* [MUST] Debounce live preview calls by at least 800ms to avoid Gotenberg engine thrashing.
* [MUST] Cancel inflight HTTP requests via `AbortController` before issuing new preview calls.
* [MUST] Clean up browser memory by calling `URL.revokeObjectURL(previousUrl)` whenever a new preview Blob URL is created or component unmounts.
```typescript
'use client';

import { useState, useRef, useCallback, useEffect } from 'react';
import { documentsApi } from '@/lib/api/documents.api';

export function useLivePreview(slug?: string, html?: string, sampleDataJson?: string) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const previousUrlRef = useRef<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

  const triggerPreview = useCallback(async () => {
    if (!slug && !html) return;

    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setLoading(true);
    setError(null);

    try {
      const blob = await documentsApi.renderPreview({
        slug,
        html,
        sampleDataJson,
        signal: controller.signal,
      });

      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }

      const newUrl = URL.createObjectURL(blob);
      previousUrlRef.current = newUrl;
      setPdfUrl(newUrl);
    } catch (err: unknown) {
      if ((err as Error)?.name !== 'AbortError') {
        setError((err as Error)?.message || 'Preview generation failed');
      }
    } finally {
      setLoading(false);
    }
  }, [slug, html, sampleDataJson]);

  useEffect(() => {
    if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
    debounceTimerRef.current = setTimeout(triggerPreview, 800);

    return () => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      if (previousUrlRef.current) URL.revokeObjectURL(previousUrlRef.current);
    };
  }, [triggerPreview]);

  return { pdfUrl, loading, error, refresh: triggerPreview };
}
```

---

## 3.1 UI Architectural Recipes (Canonical Screen Archetypes)
>> MANDATORY CANONICAL TEMPLATES [RULE-FRONT-004]:
* [MUST] Adhere to the 4 canonical enterprise screen recipes below without introducing divergent layout conventions.

### Recipe A: Dynamic Split-Pane Template Editor
* Target: Template Editor, Live Preview Studio (`src/app/(portal)/templates/[id]/editor/page.tsx`).
* Invariants: Left pane code editor, right pane streaming PDF preview iframe, header with manual render action.
```tsx
'use client';

import { useState } from 'react';
import { useLivePreview } from '@/hooks/useLivePreview';
import { Button } from '@/components/ui/button';

export function TemplateEditorSplitLayout({ initialHtml, slug }: { initialHtml: string; slug: string }) {
  const [html, setHtml] = useState(initialHtml);
  const { pdfUrl, loading, error, refresh } = useLivePreview(slug, html);

  return (
    <div className="flex h-screen flex-col bg-slate-900 text-slate-100">
      <header className="flex h-12 items-center justify-between border-b border-slate-800 px-4">
        <span className="text-xs font-mono text-slate-400">Editing: {slug}</span>
        <Button size="sm" onClick={refresh} disabled={loading}>
          {loading ? 'Rendering...' : 'Render Preview'}
        </Button>
      </header>
      <main className="grid flex-1 grid-cols-2 overflow-hidden">
        <section className="flex flex-col border-r border-slate-800">
          <textarea
            aria-label="Template HTML Source"
            className="h-full w-full resize-none bg-slate-950 p-4 font-mono text-xs text-slate-200 focus:outline-none"
            value={html}
            onChange={(e) => setHtml(e.target.value)}
          />
        </section>
        <section className="relative flex flex-col bg-slate-800/40">
          {error && <div className="p-4 text-xs text-rose-400 bg-rose-950/40 border-b border-rose-800">{error}</div>}
          {pdfUrl ? (
            <iframe title="Document Preview" src={pdfUrl} className="h-full w-full border-none" />
          ) : (
            <div className="flex h-full items-center justify-center text-xs text-slate-400">
              No preview rendered. Click Render or edit source.
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
```

### Recipe B: Enterprise Data Table Management
* Target: Template Registry, API Key Management, User List (`src/app/(portal)/templates/page.tsx`).
* Invariants: Search + Filter Header, Table body with row action buttons, compact enterprise badges.
```tsx
'use client';

import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Table, TableHead, TableRow, TableHeader, TableBody, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Plus, Search } from 'lucide-react';

interface DataTableProps {
  items: Array<{ id: string; name: string; slug: string; engine: string; isActive: boolean }>;
  onSearch: (query: string) => void;
  onCreateNew: () => void;
}

export function DataTablePage({ items, onSearch, onCreateNew }: DataTableProps) {
  return (
    <div className="flex flex-col gap-6 p-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">Document Templates</h1>
          <p className="text-xs text-slate-500">Manage, version, and configure enterprise output formats.</p>
        </div>
        <Button onClick={onCreateNew} size="sm"><Plus className="mr-1.5 h-4 w-4" /> New Template</Button>
      </div>

      <div className="flex items-center gap-3">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
          <Input placeholder="Search by name or slug..." className="pl-9 text-xs" onChange={(e) => onSearch(e.target.value)} />
        </div>
      </div>

      <div className="rounded-lg border border-slate-200 bg-white">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Template Name</TableHead>
              <TableHead>Slug</TableHead>
              <TableHead>Engine</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((t) => (
              <TableRow key={t.id}>
                <TableCell className="font-medium text-slate-900">{t.name}</TableCell>
                <TableCell className="font-mono text-xs text-slate-500">{t.slug}</TableCell>
                <TableCell><Badge variant="outline">{t.engine}</Badge></TableCell>
                <TableCell><Badge variant={t.isActive ? "default" : "secondary"}>{t.isActive ? "Active" : "Archived"}</Badge></TableCell>
                <TableCell className="text-right"><Button variant="ghost" size="sm">Edit</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
```

### Recipe C: Multi-Step Form Wizard Layout
* Target: Template Creation Wizard, Data Connection Setup (`src/app/(portal)/templates/new/page.tsx`).
* Invariants: Horizontal step tracker, single card container for current step, persistent Back/Next footer with form validation guard.
```tsx
'use client';

import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

interface WizardProps {
  currentStep: number;
  totalSteps: number;
  title: string;
  description: string;
  children: React.ReactNode;
  onBack: () => void;
  onNext: () => void;
  isNextDisabled: boolean;
}

export function WizardLayout({ currentStep, totalSteps, title, description, children, onBack, onNext, isNextDisabled }: WizardProps) {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6 py-10">
      <div className="flex items-center justify-between px-2">
        {Array.from({ length: totalSteps }).map((_, idx) => (
          <div key={idx} className="flex items-center gap-2">
            <div className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold ${
              idx + 1 === currentStep ? 'bg-primary text-primary-foreground' : idx + 1 < currentStep ? 'bg-slate-200 text-slate-700' : 'bg-slate-100 text-slate-400'
            }`}>
              {idx + 1}
            </div>
            {idx < totalSteps - 1 && <div className="h-0.5 w-16 bg-slate-200" />}
          </div>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{title}</CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
        <CardFooter className="flex justify-between border-t border-slate-100 pt-4">
          <Button variant="outline" size="sm" onClick={onBack} disabled={currentStep === 1}>Back</Button>
          <Button size="sm" onClick={onNext} disabled={isNextDisabled}>{currentStep === totalSteps ? 'Complete & Save' : 'Next Step'}</Button>
        </CardFooter>
      </Card>
    </div>
  );
}
```

### Recipe D: Executive Metrics Dashboard Layout
* Target: Analytics, Throughput, System Health (`src/app/(portal)/dashboard/page.tsx`).
* Invariants: 4-Column KPI grid, responsive chart container, recent activity event feed.
```tsx
'use client';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';

interface DashboardProps {
  stats: Array<{ label: string; value: string; changeDescription: string }>;
  recentLogs: Array<{ id: string; templateSlug: string; durationMs: number; format: string; success: boolean }>;
}

export function MetricsDashboardPage({ stats, recentLogs }: DashboardProps) {
  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-xl font-bold tracking-tight text-slate-900">System Overview</h1>
        <p className="text-xs text-slate-500">Document generation throughput, error rates, and active tenancy metrics.</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <Card key={stat.label}>
            <CardHeader className="pb-2">
              <span className="text-xs font-medium text-slate-500">{stat.label}</span>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-slate-900">{stat.value}</div>
              <p className="text-xs text-slate-500 mt-1">{stat.changeDescription}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle className="text-sm">Rendering Throughput (Last 24h)</CardTitle></CardHeader>
          <CardContent><div className="h-64 flex items-center justify-center text-xs text-slate-400">Chart Visualization Container</div></CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm">Recent Generation Audits</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-3">
            {recentLogs.map((log) => (
              <div key={log.id} className="flex items-center justify-between border-b border-slate-100 pb-2 text-xs">
                <div>
                  <div className="font-medium text-slate-800">{log.templateSlug}</div>
                  <div className="text-slate-400">{log.durationMs}ms - {log.format}</div>
                </div>
                <span className={log.success ? "text-emerald-600" : "text-rose-600"}>{log.success ? "200 OK" : "Failed"}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
```

---

## 4. Level 4: High-Salience Fatal Traps & Unified Micro-Diffs

### [TRAP-FRONT-001]: Client Component Sprawl on Root Page
```diff
- 'use client'; export default function TemplatesPage() { return <div>...</div>; }
+ import { TemplateListWidget } from './_components/TemplateListWidget'; export default async function TemplatesPage() { return <TemplateListWidget />; }
```
*Rationale: Adding `'use client'` to the page root disables React Server Component benefits, increases bundle size, and prevents direct server session resolution.*

### [TRAP-FRONT-002]: Direct Backend Access vs Secure Server Proxy
```diff
- const res = await fetch('http://localhost:8080/api/v1/templates');
+ const res = await fetch('/api/proxy/v1/templates');
```
*Rationale: Direct browser calls to internal backend endpoints trigger CORS failures and expose authorization tokens on the client.*

### [TRAP-FRONT-003]: Memory Leak: Unrevoked Object URLs
```diff
- const newUrl = URL.createObjectURL(blob); setPdfUrl(newUrl);
+ if (previousUrlRef.current) URL.revokeObjectURL(previousUrlRef.current); const newUrl = URL.createObjectURL(blob); previousUrlRef.current = newUrl; setPdfUrl(newUrl);
```
*Rationale: Overwriting Blob URLs without revoking the previous URL retains binary PDF blobs in browser RAM indefinitely.*

### [TRAP-FRONT-004]: Synchronous Next.js 15 Route Params
```diff
- export default async function Page({ params }: { params: { id: string } }) { const id = params.id; }
+ export default async function Page({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; }
```
*Rationale: Next.js 15 requires awaiting route params; synchronous property access throws a runtime warning/error during hydration.*

### [TRAP-FRONT-005]: Uncancelled In-Flight Preview Requests
```diff
- const blob = await documentsApi.renderPreview({ slug, html });
+ if (abortControllerRef.current) abortControllerRef.current.abort(); const controller = new AbortController(); abortControllerRef.current = controller; const blob = await documentsApi.renderPreview({ slug, html, signal: controller.signal });
```
*Rationale: Firing new preview requests without aborting in-flight requests causes race conditions where an older, slower render overwrites a newer one.*
