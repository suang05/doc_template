# [FRONT-001]: 01_app_router_and_data_fetching.md — Next.js 15 App Router & API Proxy SSoT

<context>
Category: 02_frontend_patterns
Domain: Next.js 15 App Router Architecture, Server Components & API Gateway Proxy
SoleResponsibilities:
  - Next.js 15 App Router topology (`src/app/(app)/`) and Server/Client Component boundaries
  - Async route parameters handling (`const { id } = await params`)
  - Server-side data fetching with ambient tenant headers and TanStack Query hydration
  - Next.js API Gateway Proxy (`/api/proxy/[...path]`) for secure credential isolation
Target: `frontend-v2/` (`src/app/`, `src/features/`, `src/hooks/`, `src/lib/api/`)
Signal: [ARCH_EVOLUTION_FRONTEND_DATA]
</context>

---

## 1. Level 1: App Router & Data Fetching Invariants Matrix

### 1.1 Architectural Wiring & Data Contracts
| Direction | Upstream Origin | Current Role (Next.js Application Gateway) | Downstream Destination |
|---|---|---|---|
| **Client Request** | Browser Client Component | Dispatches fetch to local route handler | Secure Server Proxy (`/api/proxy/[...path]`) |
| **Auth Injection** | NextAuth `httpOnly` Session | Extracts Bearer JWT & Project Context | Upstream Request Headers (`Authorization`, `Project-Id`) |
| **Header Stripping**| Browser Request Headers | Strips hop-by-hop headers (`host`, `connection`, etc.) | Backend API Port (`http://backend:8080/api/v1/`) |
| **Streaming Egress**| Backend Document Stream | Duplex-pipes body stream directly to caller | Browser Client (`Response(backendRes.body)`) |
| **Live Preview** | Template Editor Changes | Debounces (800ms) with `AbortController` cancellation | Blob URL Egress (`URL.revokeObjectURL(old)`) |

### 1.2 Affirmative App Router & Data Fetching Invariants
| App Router & Data Concern | Affirmative Frontend Invariant (Positive SSoT Law) | Boundary Vector |
|---|---|---|
| **Server Component Default** | React Server Components (RSC) are the default for all pages and layouts. `'use client'` is strictly confined to leaf interactive components. Root page `'use client'` is prohibited. | Component Boundary |
| **Proxy Token Boundary & Silent Refresh** | Backend JWT tokens and tenant project IDs (`Project-Id`) are injected exclusively server-side via Next.js Server Proxy (`/api/proxy/[...path]`) using `httpOnly` cookies. The proxy intercepts HTTP 401 and transparently performs silent token refresh via `POST /api/v1/auth/refresh` before retrying the upstream call. Storing tokens in `localStorage` or unhandled session drops are prohibited. | Session Security |
| **Perimeter Proxy Routing** | All browser client API traffic routes strictly through `/api/proxy/[...path]`. Direct browser calls to internal backend ports are prohibited. | Network Ingress |
| **Debounced Live Preview** | Template live preview triggers are debounced by at least 800ms and linked to an `AbortController`. Firing unthrottled preview requests on keystrokes is prohibited. | Rendering Throttling |
| **Blob URL Memory Lifecycle**| Every newly generated Blob URL must revoke the preceding URL via `URL.revokeObjectURL(oldUrl)` and clean up on component unmount. Unrevoked URL allocation is prohibited. | Browser Memory & GC |
| **Next.js 15 Async Params** | Route parameters are typed and handled strictly as asynchronous promises: `{ params }: { params: Promise<{ id: string }> }` followed by `await params`. Synchronous param access is prohibited. | Framework Routing |
| **Server State Governance** | Client server state and data mutations are governed via `@tanstack/react-query` (`useQuery`, `useMutation`) with automated cache invalidation. Ad-hoc `useEffect` fetching is prohibited. | State Synchronization |
| **Optimistic Concurrency Parity** | State-mutating forms and hooks (`PUT`, `PATCH`, `DELETE`) must forward `If-Match: "v{version}"` using the resource's integer `version` (or strong `ETag` header). Client error boundaries must intercept HTTP `412 Precondition Failed` (`PRECONDITION_FAILED`) and `428 Precondition Required` (`PRECONDITION_REQUIRED`), alert the operator with conflict details, and prompt to reload rather than overwriting. Blind mutations without `If-Match` are prohibited. | Concurrency Parity |
| **Feature Sliced Isolation** | Code is organized in self-contained feature slices under `src/features/{slice}/` exporting their public interface through `index.ts`. Flat global folder sprawl is prohibited. | Code Topology |

---

## 2. Level 2: Core Frontend Topology & Cognitive Frontend Engine

### 2.1 Engineering Rationale & Architectural Decisions
* **Why Next.js Server Proxy over Direct Browser Calls:** Keeps backend JWT tokens and internal cluster endpoints completely hidden inside server-side `httpOnly` cookies, eliminating XSS token theft vectors.
* **Why Next.js 15 Async `params`:** Next.js 15 enforces asynchronous route parameters to enable partial prerendering and streaming server components without synchronous blocking.
* **Why 800ms Debounce with AbortController:** Document rendering via Gotenberg is CPU-intensive; 800ms debounce prevents cluster thrashing, while `AbortController` immediately halts stale rendering jobs on the wire.
* **Why Strict `URL.revokeObjectURL` Cleanup:** Browser blob URLs remain retained in browser heap memory until explicitly revoked; omitting cleanup causes substantial memory leaks during prolonged template editing sessions.
* **Why TanStack Query over Ad-hoc useEffect:** Eliminates duplicate network requests, provides stale-while-revalidate background caching, and automates cache invalidation across disparate feature widgets without props drilling.
* **Why Client-Side If-Match Parity over Blind Last-Write-Wins:** In an authoring console where template designers edit complex schemas or HTML concurrently in multiple browser tabs, blind mutations cause silent lost updates. Forwarding `If-Match: "v{version}"` ensures that if another tab saved in the interim, the browser receives `412 Precondition Failed` (`PRECONDITION_FAILED`), preserving uncommitted editor changes and alerting the designer.
* **Why Feature-Sliced Taxonomy over Flat Layered Folders:** Grouping related components, hooks, APIs, and schemas by domain capability ensures high cohesion and low coupling. Developers modify feature mechanics in a single directory without navigating across 6 disparate root folders, preventing circular dependencies and git merge conflicts.
* **Why Server-Side Silent Token Refresh in API Proxy:** JWT access tokens have a short lifespan (15 minutes). If the proxy did not intercept 401 responses, a template designer actively typing or testing in Monaco Studio would experience abrupt network failures or session terminations upon access token expiration. The server-side proxy intercepts 401, issues a refresh request using the secure `httpOnly` refresh token, updates the session, and seamlessly replays the request without client disruption.

### 2.2 Proactive Frontend Guards
```yaml
ClientComponentRootGuard:
  Invariant: "Page and layout files must remain React Server Components."
  ActionOnViolation: "Remove 'use client' from page root and extract interactive leaves."

Next15AsyncParamsGuard:
  Invariant: "Route params must be declared as Promise<T> and awaited."
  ActionOnViolation: "Convert { params }: { params: Promise<{ id: string }> } and await params."

BlobUrlMemoryLeakGuard:
  Invariant: "Object URLs must be revoked before creating a replacement and on unmount."
  ActionOnViolation: "Add URL.revokeObjectURL(previousUrlRef.current) before update and in cleanup."

DirectBackendAccessGuard:
  Invariant: "Browser runtime must not call backend API port directly."
  ActionOnViolation: "Rewrite client fetch paths to route through /api/proxy/[...path]."
```

### 2.3 Frontend Bounded Context Taxonomy (`src/features/`)
```
frontend-v2/src/features/
  templates/ # Authoring: List, Create, Upload Wizard, Choice Modal
    components/
    hooks/
    api/
    schemas/
    index.ts # Public API export barrel
  studio/ # Interactive Authoring: Monaco, HTML preview, Variable extraction
    components/
    hooks/
    domain/
    index.ts # Public API export barrel
  generator/ # Rendering: Dynamic forms, Payload editor, Gotenberg dispatch
    components/
    hooks/
    api/
    schemas/
    index.ts # Public API export barrel
  mapping/ # Field Mapping: Visual Mapper, SQL/Fx transform, drag-and-drop
    components/
    hooks/
    domain/
    schemas/
    index.ts # Public API export barrel
  datasources/ # Integration: Datasets, Connections, Test query
    components/
    hooks/
    api/
    schemas/
    index.ts # Public API export barrel
  auth/ # IdentityAccess: Login, NextAuth proxy, session state
  users/ # IdentityAccess: User management, role assignment
  apikeys/ # IdentityAccess: API key tokens, project scoping
```

### 2.4 Hop-by-Hop Stripped Headers
The Next.js reverse proxy MUST strip these headers before forwarding upstream:
```typescript
const HOP_BY_HOP_HEADERS = ['host', 'connection', 'content-length', 'transfer-encoding'];
```

### 2.5 Component Boundary Classification Matrix
| Component Archetype | Execution Realm | Permitted Capabilities | Prohibited Patterns |
|---|---|---|---|
| `Page / Layout` | Server Component (RSC) | Async data fetching, session resolution | No React hooks (`useState`, `useEffect`), no event listeners |
| `Interactive Widget` | Client Component (`'use client'`) | State, effects, live preview, event listeners | No direct database queries, no secret environment variables |
| `Proxy Route Handler` | Node.js Server Runtime | Token injection, header transformation, streaming | No client rendering, no localStorage access |

---

## 3. Level 3: Canonical Frontend Archetypes (Surgical AST Recipes)

### 3.1 Secure Server Proxy Route Archetype (`/api/proxy/[...path]/route.ts`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-FRONT-001]:
* Routes all client browser API traffic through the Next.js reverse proxy.
* Strips hop-by-hop headers before upstream dispatch.
* Injects backend Bearer JWT token strictly on server side from `httpOnly` NextAuth session cookies.
* Streams backend response body directly to caller (`new Response(backendRes.body)` with duplex streaming).

```typescript
import { NextRequest } from 'next/server';
import { auth } from '@/auth';

const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

const HOP_BY_HOP_HEADERS = ['host', 'connection', 'content-length', 'transfer-encoding'];

async function proxy(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  // Step 1: Resolve Async Route Params <Why: Next.js 15 dynamic route contract>
  const { path } = await params;
  const session = await auth();

  const targetUrl = `${BACKEND_INTERNAL_URL}/api/${path.join('/')}${request.nextUrl.search}`;

  // Step 2: Strip Hop-by-Hop Headers <Why: prevent HTTP/1.1 vs HTTP/2 gateway forwarding conflicts>
  const headers = new Headers(request.headers);
  for (const name of HOP_BY_HOP_HEADERS) headers.delete(name);

  // Step 3: Inject Secure Server-Side Token & Tenancy <Why: zero client-side token exposure>
  if (session?.backendToken) {
    headers.set('Authorization', `Bearer ${session.backendToken}`);
  } else {
    headers.delete('authorization');
  }

  if (!headers.has('Project-Id') && session?.user?.projectId) {
    headers.set('Project-Id', session.user.projectId);
  }

  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';

  // Step 4: Dispatch Request via Duplex Stream <Why: direct response pass-through without memory buffering>
  let backendResponse = await fetch(targetUrl, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    duplex: hasBody ? 'half' : undefined,
  } as RequestInit);

  // Step 5: Transparent Silent Refresh on 401 Unauthorized <Why: prevent abrupt user session drops on access token expiry>
  if (backendResponse.status === 401 && session?.refreshToken) {
    const refreshResponse = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    });

    if (refreshResponse.ok) {
      const refreshPayload = await refreshResponse.json();
      const newAccessToken = refreshPayload.data.accessToken;

      // Retry original request once with fresh access token
      headers.set('Authorization', `Bearer ${newAccessToken}`);
      backendResponse = await fetch(targetUrl, {
        method: request.method,
        headers,
        body: hasBody ? request.body : undefined,
        duplex: hasBody ? 'half' : undefined,
      } as RequestInit);
    }
  }

  const responseHeaders = new Headers(backendResponse.headers);
  responseHeaders.delete('content-encoding');
  responseHeaders.delete('content-length');

  return new Response(backendResponse.body, {
    status: backendResponse.status,
    headers: responseHeaders,
  });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
```

### 3.2 Next.js 15 Async Route Page Archetype (`src/app/(app)/templates/[id]/page.tsx`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-FRONT-002]:
* Treats `params` as `Promise<{ id: string }>` and awaits independent inputs (`params` and `auth()`) in parallel via `Promise.all` to avoid a request waterfall.
* Executes server-side session checks (`if (!session?.user?.projectId) redirect('/login')`) and passes the tenant context via the canonical `Project-Id` header.
* Never caches tenant data under shared cache keys; each route segment also provides `loading.tsx` and `error.tsx`.

```tsx
import { redirect } from 'next/navigation';
import { auth } from '@/auth';
import { apiServerClient } from '@/lib/api/server-client';
import { TemplateEditorFeature } from '@/features/templates/components/TemplateEditorFeature';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function TemplateDetailsPage({ params }: PageProps) {
  // Step 1: Await independent inputs together <Why: avoids a request waterfall>
  const [{ id }, session] = await Promise.all([params, auth()]);

  // Step 2: Session guard <Why: unauthenticated access is a redirect, not a 404>
  if (!session?.user?.projectId) {
    redirect('/login');
  }

  const initialData = await apiServerClient.templates.getById(id, {
    headers: { 'Project-Id': session.user.projectId },
  });

  // Step 3: Render the client boundary <Why: pass a serializable DTO across the RSC boundary>
  return (
    <main className="flex h-full flex-col">
      <TemplateEditorFeature templateId={id} initialData={initialData} />
    </main>
  );
}
```

### 3.3 Real-Time Live Preview Pipeline Hook Archetype (`useLivePreview.ts`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-FRONT-003]:
* Debounces live preview calls by at least 800ms to avoid Gotenberg engine thrashing.
* Cancels inflight HTTP requests via `AbortController` before issuing new preview calls.
* Cleans up browser memory by calling `URL.revokeObjectURL(previousUrl)` whenever a new preview Blob URL is created or component unmounts.

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

    // Step 1: Abort Previous In-Flight Request <Why: prevent out-of-order race conditions>
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

      // Step 2: Revoke Previous Object URL <Why: prevent browser heap memory leaks>
      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }

      // Step 3: Create New Blob URL <Why: isolated streaming iframe preview>
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

  // Step 4: 800ms Debounce Lifecycle <Why: protect Gotenberg from CPU thrashing during typing>
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

### 3.4 Canonical Screen Recipes

#### Recipe A: Dynamic Split-Pane Template Editor (`TemplateEditorSplitLayout`)
```tsx
'use client';

import { useState } from 'react';
import { useLivePreview } from '@/features/studio';
import { Button } from '@/components/ui/Button';

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

#### Recipe B: Enterprise Data Table Management (`DataTablePage`)
```tsx
'use client';

import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
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

#### Recipe C: Multi-Step Form Wizard Layout (`WizardLayout`)
```tsx
'use client';

import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/Button';

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

#### Recipe D: Executive Metrics Dashboard Layout (`MetricsDashboardPage`)
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

### 3.5 Optimistic Concurrency Mutation Hook Archetype (`useUpdateTemplate.ts`)
>> MANDATORY CANONICAL BLUEPRINT [RULE-FRONT-004]:
* Captures the integer `version` from the resource query state (`TemplateResultDto.version` / strong `ETag: "v{version}"`).
* Forwards `If-Match: `"v${version}"`` header during `PUT` / `PATCH` / `DELETE` mutations.
* Intercepts HTTP `412 Precondition Failed` (`PRECONDITION_FAILED`) and HTTP `428 Precondition Required` (`PRECONDITION_REQUIRED`), prompting the user with a conflict toast or dialog to reload without discarding uncommitted input.
* Invalidates cache and hydrates updated `version` on success.

```typescript
'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient, ApiError } from '@/lib/api/client';
import { toast } from 'sonner';

interface UpdateTemplatePayload {
  id: string;
  name: string;
  category?: string;
  version: number; // Captured from query cache / ETag ("v{version}")
}

interface TemplateResult {
  id: string;
  name: string;
  category?: string;
  version: number;
}

export function useUpdateTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, name, category, version }: UpdateTemplatePayload) => {
      // Step 1: Forward Strong ETag Concurrency Precondition Header <Why: prevent lost update overwrites>
      return await apiClient<TemplateResult>(`/api/v1/templates/${id}`, {
        method: 'PUT',
        headers: {
          'If-Match': `"v${version}"`,
        },
        body: JSON.stringify({ name, category }),
      });
    },
    onSuccess: (data) => {
      toast.success('Template updated successfully');
      // Step 2: Invalidate & Hydrate New Cache State <Why: synchronize incremented version>
      queryClient.setQueryData(['templates', data.id], data);
      queryClient.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (error: unknown) => {
      if (error instanceof ApiError) {
        // Step 3: Handle Concurrency Precondition Failures <Why: clear user feedback on 412/428>
        if (error.status === 412) {
          toast.error(
            'Concurrency Conflict: This template was updated by another session. Please refresh to view the latest changes.',
            { duration: 8000 }
          );
          return;
        }
      }
      toast.error('Failed to update template');
    },
  });
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
- const httpResponse = await fetch('http://localhost:8080/api/v1/templates');
+ const httpResponse = await fetch('/api/proxy/v1/templates');
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

### [TRAP-FRONT-006]: Blind Mutation Without If-Match Header (Silent Overwrites)
```diff
- await apiClient(`/api/v1/templates/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
+ await apiClient(`/api/v1/templates/${id}`, {
+   method: 'PUT',
+   headers: { 'If-Match': `"v${template.version}"` },
+   body: JSON.stringify(payload),
+ });
```
*Rationale: Executing PUT/PATCH/DELETE without `If-Match: "v{version}"` is rejected with `428 Precondition Required`, and prevents multiple browser tabs or operators from silently overwriting each other's changes.*

### [TRAP-FRONT-007]: Unhandled 401 Session Drop in API Proxy
```diff
  let backendResponse = await fetch(targetUrl, { method: request.method, headers, body });
- // Returning 401 directly drops active user session when 15-minute access token expires!
- return new Response(backendResponse.body, { status: backendResponse.status });
+ if (backendResponse.status === 401 && session?.refreshToken) {
+   // Transparent silent refresh on server side before failing to client
+   const refreshRes = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/auth/refresh`, { ... });
+   if (refreshRes.ok) {
+     headers.set('Authorization', `Bearer ${newAccessToken}`);
+     backendResponse = await fetch(targetUrl, { method: request.method, headers, body });
+   }
+ }
+ return new Response(backendResponse.body, { status: backendResponse.status });
```
*Rationale: Failing to intercept 401 in the Next.js reverse proxy abruptly disconnects active template designers upon short-lived JWT expiration. Transparent server-side silent refresh using the httpOnly refresh token preserves active Monaco Studio sessions without interrupting user input.*
