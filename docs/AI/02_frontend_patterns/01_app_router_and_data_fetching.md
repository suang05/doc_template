# [FRONT-001]: 01_app_router_and_data_fetching.md — Next.js 15 App Router & API Proxy SSoT

<context>
Scope: Next.js 15 App Router Architecture, Server/Client Component Boundaries, Route Handler Proxy & Live Preview Pipeline
Target: `frontend-v2/` (`src/app/`, `src/hooks/`, `src/lib/api/`)
Authority: Absolute SSoT Invariant (Zero client-side JWT token access, 100% debounced streaming preview)
</context>

---

## 1. App Router Architecture & Component Boundary Rules

| Architectural Concern | Prohibited Pattern (! BANNED) | Canonical SSoT Pattern (>> MANDATORY) |
|---|---|---|
| **Client Component Scope** | Placing `'use client'` at root page or layout | React Server Components (RSC) by default; restrict `'use client'` to leaf interactive components |
| **API Token Storage** | Storing JWT tokens in `localStorage` or browser cookies | Next.js Server Proxy (`/api/proxy/[...path]`) with `httpOnly` session cookies |
| **Direct Backend Calls** | Client browser fetching backend API directly (`http://localhost:8080`) | All client fetch calls routed through `/api/proxy/[...path]` |
| **Live Preview Sync** | Firing preview requests on every keystroke | Debounced (800ms) with `AbortController` cancellation |
| **Blob URL Leaks** | Overwriting preview object URLs without revoking | Immediate cleanup: `URL.revokeObjectURL(previousUrl)` |

---

## 2. Secure Server Proxy Pattern (`/api/proxy/[...path]/route.ts`)

The browser client never touches backend Bearer tokens or backend internal ports. The Next.js Route Handler bridges session cookies to backend Bearer JWT headers:

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

---

## 3. Real-Time Live Preview Pipeline Hook (`useLivePreview.ts`)

Prevents race conditions and memory leaks during live template editing:
1. **800ms Debounce**: Throttle rapid input events.
2. **AbortController**: Abort pending in-flight HTTP requests before issuing a new one.
3. **URL Revocation**: Call `URL.revokeObjectURL` on previous blob URL to prevent browser memory leaks.

```typescript
'use client';

import { useState, useRef, useCallback } from 'react';
import { documentsApi } from '@/lib/api/documents.api';

export function useLivePreview(slug?: string, html?: string, sampleDataJson?: string) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const previousUrlRef = useRef<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const generatePreview = useCallback(async () => {
    if (!slug && !html) return;

    // 1. Cancel in-flight request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setLoading(true);
    setError(null);

    try {
      let parsedData: Record<string, unknown> = {};
      if (sampleDataJson) {
        try { parsedData = JSON.parse(sampleDataJson); } catch { /* ignore parse errors while typing */ }
      }

      // 2. Fetch rendered PDF blob via server proxy
      const blob = await documentsApi.previewDocument(
        slug,
        { payload: parsedData, html: html || undefined },
        { signal: controller.signal }
      );

      // 3. Revoke previous blob URL to release browser RAM
      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }

      const newUrl = URL.createObjectURL(blob);
      previousUrlRef.current = newUrl;
      setPdfUrl(newUrl);
    } catch (err: unknown) {
      if ((err as Error).name !== 'AbortError') {
        setError((err as Error).message || 'Failed to render preview');
      }
    } finally {
      setLoading(false);
    }
  }, [slug, html, sampleDataJson]);

  return { pdfUrl, loading, error, generatePreview };
}
```

---

## 4. TanStack Query Data Fetching Pattern

Standardized query key factories and normalized error handling:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { templatesApi } from '@/lib/api/templates.api';

export const templateKeys = {
  all: ['templates'] as const,
  lists: () => [...templateKeys.all, 'list'] as const,
  list: (projectId: string) => [...templateKeys.lists(), { projectId }] as const,
  details: () => [...templateKeys.all, 'detail'] as const,
  detail: (id: string) => [...templateKeys.details(), id] as const,
};

export function useTemplates(projectId?: string) {
  return useQuery({
    queryKey: templateKeys.list(projectId ?? ''),
    queryFn: () => templatesApi.listTemplates(projectId),
    enabled: Boolean(projectId),
  });
}
```

---

## 5. 4 Master Screen Stitching Recipes (Generative UI Blueprints)

### Recipe A: Split-Screen Studio Layout (Editor + Live PDF Preview)
* Target: Template Designer, HTML Authoring, Data Mapping (`src/app/(studio)/templates/[id]/editor/page.tsx`).
* Invariants: Full viewport height (`h-screen overflow-hidden`), resizable or 50/50 split, debounced preview via `useLivePreview`.

```tsx
'use client';

import { useLivePreview } from '@/hooks/useLivePreview';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';

export function StudioPage({ slug, initialHtml }: { slug: string; initialHtml: string }) {
  const [html, setHtml] = useState(initialHtml);
  const { pdfUrl, loading, error, generatePreview } = useLivePreview(slug, html);

  return (
    <div className="flex h-screen w-full flex-col bg-slate-900 text-slate-100">
      <header className="flex h-14 items-center justify-between border-b border-slate-800 px-6">
        <h1 className="text-sm font-semibold tracking-wide text-slate-200">Template Studio: {slug}</h1>
        <Button size="sm" onClick={generatePreview} disabled={loading}>
          {loading ? <Spinner className="mr-2 h-4 w-4" /> : null}
          Render Preview
        </Button>
      </header>
      <main className="grid flex-1 grid-cols-2 overflow-hidden">
        {/* Left Pane: Code/Monaco Editor */}
        <section className="flex flex-col border-r border-slate-800">
          <textarea
            aria-label="Template HTML Source"
            className="h-full w-full resize-none bg-slate-950 p-4 font-mono text-xs text-slate-200 focus:outline-none"
            value={html}
            onChange={(e) => setHtml(e.target.value)}
          />
        </section>
        {/* Right Pane: Live PDF Streaming Viewer */}
        <section className="relative flex flex-col bg-slate-800/40">
          {error ? (
            <div className="p-4 text-xs text-rose-400 bg-rose-950/40 border-b border-rose-800">{error}</div>
          ) : null}
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
* Target: Template Registry, API Key Management, User List (`src/app/(admin)/templates/page.tsx`).
* Invariants: Search + Filter Header, TanStack Table body with row action popovers, sticky pagination footer.

```tsx
'use client';

import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Table, TableHead, TableRow, TableHeader, TableBody, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Plus, Search } from 'lucide-react';

export function DataTablePage({ items, onSearch, onCreateNew }: DataTablePageProps) {
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
* Target: Template Creation Wizard, Data Connection Setup (`src/app/(admin)/templates/new/page.tsx`).
* Invariants: Horizontal step tracker, single card container for current step, persistent Back/Next footer with form validation guard.

```tsx
'use client';

import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export function WizardLayout({ currentStep, totalSteps, title, description, children, onBack, onNext, isNextDisabled }: WizardProps) {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6 py-10">
      {/* Step Tracker Indicator */}
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
* Target: Analytics, Throughput, System Health (`src/app/(admin)/dashboard/page.tsx`).
* Invariants: 4-Column KPI grid, responsive chart container, recent activity event feed.

```tsx
'use client';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';

export function MetricsDashboardPage({ stats, recentLogs }: DashboardProps) {
  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-xl font-bold tracking-tight text-slate-900">System Overview</h1>
        <p className="text-xs text-slate-500">Document generation throughput, error rates, and active tenancy metrics.</p>
      </div>

      {/* 4-Column StatBlock Grid */}
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

      {/* Operational Feed & Health Split */}
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

