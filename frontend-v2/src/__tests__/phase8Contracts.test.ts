import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { jobsApi } from '@/features/jobs/api/jobs.api';
import { logsApi } from '@/features/logs/api/logs.api';
import { projectsApi } from '@/features/projects/api/projects.api';
import { templatesApi } from '@/features/templates/api/templates.api';
import { GeneratorResultCard } from '@/features/generator/components/GeneratorResultCard';
import {
  RouteLoadingSkeleton,
  RouteErrorFallback,
} from '@/components/ui/RouteBoundaryStates';
import type { GenerateDocumentResultDto } from '@/lib/api/types';

describe('Phase 8 API Contract Alignments', () => {
  const originalFetch = global.fetch;

  afterEach(() => {
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('jobsApi.listJobs preserves PagedEnvelope data and nextCursor', async () => {
    const payload = {
      data: [
        {
          jobId: 'job-1',
          projectId: 'proj-1',
          status: 'Queued',
          totalCount: 2,
          succeededCount: 0,
          failedCount: 0,
          createdAt: '2026-11-16T00:00:00Z',
        },
      ],
      pagination: {
        limit: 20,
        nextCursor: 'cursor-page-2',
      },
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    global.fetch = fetchMock;

    const result = await jobsApi.listJobs({
      status: 'Queued',
      cursor: 'cursor-1',
      limit: 20,
    });

    const [calledUrl] = fetchMock.mock.calls[0] as [string];
    expect(calledUrl).toBe(
      '/api/proxy/v1/document-jobs?status=Queued&cursor=cursor-1&limit=20',
    );
    expect(result.data).toHaveLength(1);
    expect(result.pagination?.nextCursor).toBe('cursor-page-2');
  });

  it('logsApi.listLogs sends page, limit, app filter, and includeTotal=true by default', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          data: [],
          pagination: { page: 2, limit: 50, totalCount: 0, totalPages: 1 },
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );
    global.fetch = fetchMock;

    await logsApi.listLogs(2, 50, 'billing-app');

    const [calledUrl] = fetchMock.mock.calls[0] as [string];
    const parsed = new URL(calledUrl, 'http://localhost');
    expect(parsed.pathname).toBe('/api/proxy/v1/logs');
    expect(parsed.searchParams.get('page')).toBe('2');
    expect(parsed.searchParams.get('limit')).toBe('50');
    expect(parsed.searchParams.get('app')).toBe('billing-app');
    expect(parsed.searchParams.get('includeTotal')).toBe('true');
  });

  it('projectsApi.listAccessible and listTenants serialize limit and includeTotal only', async () => {
    const fetchMock = vi.fn().mockImplementation(async () =>
      new Response(JSON.stringify({ data: [], pagination: { page: 1, limit: 100 } }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    global.fetch = fetchMock;

    await projectsApi.listAccessible({ limit: 100, includeTotal: false });
    await projectsApi.listTenants({ page: 1, limit: 20, includeTotal: true });

    const [accessibleUrl] = fetchMock.mock.calls[0] as [string];
    const accessibleQs = new URL(accessibleUrl, 'http://localhost').searchParams;
    expect(Array.from(accessibleQs.keys())).toEqual(['limit', 'includeTotal']);
    expect(accessibleQs.get('limit')).toBe('100');
    expect(accessibleQs.get('includeTotal')).toBe('false');

    const [tenantsUrl] = fetchMock.mock.calls[1] as [string];
    const tenantsQs = new URL(tenantsUrl, 'http://localhost').searchParams;
    expect(Array.from(tenantsQs.keys())).toEqual(['page', 'limit', 'includeTotal']);
    expect(tenantsQs.get('limit')).toBe('20');
    expect(tenantsQs.get('includeTotal')).toBe('true');
  });

  it('templatesApi.listTemplates queries limit=100 & includeTotal=true and unwraps PagedEnvelope data', async () => {
    const templateItem = {
      id: 'tpl-1',
      name: 'Invoice',
      slug: 'invoice',
      format: 'html',
      currentVersion: 3,
      isActive: true,
      createdAt: '2026-11-16T00:00:00Z',
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          data: [templateItem],
          pagination: { page: 1, limit: 100, totalCount: 1, totalPages: 1 },
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );
    global.fetch = fetchMock;

    const list = await templatesApi.listTemplates();
    const [calledUrl] = fetchMock.mock.calls[0] as [string];
    const qs = new URL(calledUrl, 'http://localhost').searchParams;

    expect(qs.get('limit')).toBe('100');
    expect(qs.get('includeTotal')).toBe('true');
    expect(list).toEqual([templateItem]);
  });
});

describe('Phase 8 SSR Components & Proxy URL Wiring', () => {
  it('RouteLoadingSkeleton renders accessible status markup for page and studio variants', () => {
    const pageHtml = renderToStaticMarkup(
      React.createElement(RouteLoadingSkeleton, { variant: 'page' }),
    );
    expect(pageHtml).toContain('role="status"');
    expect(pageHtml).toContain('aria-live="polite"');
    expect(pageHtml).toContain('motion-reduce:animate-none');
    expect(pageHtml).not.toContain('rounded-xl');
    expect(pageHtml).not.toContain('rounded-2xl');

    const studioHtml = renderToStaticMarkup(
      React.createElement(RouteLoadingSkeleton, { variant: 'studio' }),
    );
    expect(studioHtml).toContain('role="status"');
    expect(studioHtml).toContain('Template Studio');
  });

  it('RouteErrorFallback renders alert card with digest badge, retry button, and home link', () => {
    const err = Object.assign(new Error('Backend connection refused'), {
      digest: 'ERR-500-XYZ',
    });
    const html = renderToStaticMarkup(
      React.createElement(RouteErrorFallback, {
        error: err,
        reset: () => undefined,
        homeHref: '/templates',
        homeLabel: 'กลับหน้าหลัก',
      }),
    );

    expect(html).toContain('role="alert"');
    expect(html).toContain('Backend connection refused');
    expect(html).toContain('ERR-500-XYZ');
    expect(html).toContain('ลองใหม่อีกครั้ง');
    expect(html).toContain('href="/templates"');
    expect(html).toContain('class="lucide');
  });

  it('GeneratorResultCard rewrites Gateway /api/v1 URLs through /api/proxy/v1', () => {
    const result: GenerateDocumentResultDto = {
      generationId: 'gen-uuid-1',
      documentRef: 'INV-001',
      outputFormat: 'pdf',
      fileSizeBytes: 2048,
      sha256: 'abc123',
      expiresAt: '2026-11-17T00:00:00Z',
      url: '/api/v1/generation-logs/log-1/artifact/file',
    };

    const html = renderToStaticMarkup(
      React.createElement(GeneratorResultCard, {
        result,
        error: null,
        onCopy: () => undefined,
      }),
    );

    expect(html).toContain('href="/api/proxy/v1/generation-logs/log-1/artifact/file"');
    expect(html).not.toContain('href="/api/v1/');
    expect(
      html.match(/href="\/api\/proxy\/v1\/generation-logs\/log-1\/artifact\/file"/g)?.length,
    ).toBe(2);
    expect(html).toContain('/api/v1/generation-logs/log-1/artifact/file');
  });
});
