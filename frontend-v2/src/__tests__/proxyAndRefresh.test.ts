import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { NextRequest } from 'next/server';

const mockReadSessionJwt = vi.hoisted(() => vi.fn());
const mockAuth = vi.hoisted(() => vi.fn());

vi.mock('@/auth', () => ({
  auth: mockAuth,
}));

vi.mock('@/lib/auth/refreshCoordinator', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/auth/refreshCoordinator')>();
  return {
    ...actual,
    readSessionJwt: mockReadSessionJwt,
  };
});

import {
  ACTIVE_PROJECT_COOKIE,
  BACKEND_INTERNAL_URL,
  resetRefreshCoordinator,
  rotateBackendRefreshToken,
} from '@/lib/auth/refreshCoordinator';
import { handleProxyRequest } from '@/lib/proxy/forwardBackendRequest';

function createUnsignedJwt(claims: Record<string, unknown>): string {
  const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
  const payload = Buffer.from(JSON.stringify(claims)).toString('base64url');
  return `${header}.${payload}.sig`;
}

describe('Single-Flight Refresh Coordinator (rotateBackendRefreshToken)', () => {
  beforeEach(() => {
    resetRefreshCoordinator();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('deduplicates concurrent refreshes with the same refresh token and honors the replay grace window', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-15T10:00:00Z'));

    const futureExp = Math.floor(Date.now() / 1000) + 900;
    const accessToken = createUnsignedJwt({
      exp: futureExp,
      SystemRole: 'PortalUser',
      ProjectId: 'proj-1',
      role: 'Developer',
    });

    const fetchMock = vi.fn().mockImplementation(async () => {
      return new Response(
        JSON.stringify({
          data: {
            accessToken,
            refreshToken: 'rt-rotated-2',
            expiresIn: 900,
          },
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      );
    });

    // Two concurrent calls share a single backend /auth/refresh invocation.
    const [first, second] = await Promise.all([
      rotateBackendRefreshToken('rt-initial-1', { fetchImpl: fetchMock, replayGraceMs: 3000 }),
      rotateBackendRefreshToken('rt-initial-1', { fetchImpl: fetchMock, replayGraceMs: 3000 }),
    ]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(first.accessToken).toBe(accessToken);
    expect(second.refreshToken).toBe('rt-rotated-2');
    expect(first.role).toBe('Developer');

    // Replay within the 3s grace window returns cached result without calling backend again.
    vi.advanceTimersByTime(1500);
    const cached = await rotateBackendRefreshToken('rt-initial-1', {
      fetchImpl: fetchMock,
      replayGraceMs: 3000,
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(cached.accessToken).toBe(accessToken);

    // After grace window elapses, a new upstream refresh is permitted.
    vi.advanceTimersByTime(2000);
    await rotateBackendRefreshToken('rt-initial-1', {
      fetchImpl: fetchMock,
      replayGraceMs: 3000,
    });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});

describe('BFF Proxy Route (handleProxyRequest)', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    resetRefreshCoordinator();
    process.env.AUTH_SECRET = 'test-auth-secret-32-chars-minimum!';
    mockAuth.mockResolvedValue(null);
    mockReadSessionJwt.mockResolvedValue({
      cookieName: 'authjs.session-token',
      token: {
        backendToken: 'jwt-initial',
        refreshToken: 'rt-1',
        backendTokenExp: Math.floor(Date.now() / 1000) + 600,
        projectId: 'proj-fallback',
        systemRole: 'PortalUser',
        role: 'Admin',
      },
    });
  });

  afterEach(() => {
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('forwards smk_active_project_id as Project-Id on runtime routes and skips /v1/management/*', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => {
      return new Response(JSON.stringify({ data: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    global.fetch = fetchMock;

    const runtimeReq = new NextRequest('http://localhost:3001/api/proxy/v1/templates', {
      method: 'GET',
      headers: {
        cookie: `${ACTIVE_PROJECT_COOKIE}=proj-cookie-override`,
      },
    });
    await handleProxyRequest(runtimeReq, ['v1', 'templates']);

    const [, runtimeInit] = fetchMock.mock.calls[0] as [string, RequestInit];
    const runtimeHeaders = new Headers(runtimeInit.headers);
    expect(runtimeHeaders.get('Project-Id')).toBe('proj-cookie-override');
    expect(runtimeHeaders.get('Authorization')).toBe('Bearer jwt-initial');

    const mgmtReq = new NextRequest('http://localhost:3001/api/proxy/v1/management/tenants', {
      method: 'GET',
      headers: {
        cookie: `${ACTIVE_PROJECT_COOKIE}=proj-cookie-override`,
      },
    });
    await handleProxyRequest(mgmtReq, ['v1', 'management', 'tenants']);

    const [, mgmtInit] = fetchMock.mock.calls[1] as [string, RequestInit];
    const mgmtHeaders = new Headers(mgmtInit.headers);
    expect(mgmtHeaders.get('Project-Id')).toBeNull();
  });

  it('strips browser x-api-key header unless x-m2m-test is true', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => {
      return new Response(JSON.stringify({ data: {} }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    global.fetch = fetchMock;

    const normalReq = new NextRequest('http://localhost:3001/api/proxy/v1/templates', {
      method: 'GET',
      headers: { 'x-api-key': 'should-be-stripped' },
    });
    await handleProxyRequest(normalReq, ['v1', 'templates']);
    const [, normalInit] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(new Headers(normalInit.headers).get('x-api-key')).toBeNull();

    const m2mReq = new NextRequest('http://localhost:3001/api/proxy/v1/documents', {
      method: 'GET',
      headers: { 'x-api-key': 'm2m-secret', 'x-m2m-test': 'true' },
    });
    await handleProxyRequest(m2mReq, ['v1', 'documents']);
    const [, m2mInit] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(new Headers(m2mInit.headers).get('x-api-key')).toBe('m2m-secret');
  });

  it('replays buffered JSON POST body after single-flight 401 refresh and forwards ETag/Retry-After/RateLimit headers', async () => {
    const freshExp = Math.floor(Date.now() / 1000) + 900;
    const rotatedAccessToken = createUnsignedJwt({
      exp: freshExp,
      SystemRole: 'PortalUser',
      ProjectId: 'proj-fallback',
      role: 'Admin',
    });

    const payloadBody = JSON.stringify({ slug: 'invoice', payload: { amount: 500 } });

    const fetchMock = vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === `${BACKEND_INTERNAL_URL}/api/v1/auth/refresh`) {
        return new Response(
          JSON.stringify({
            data: {
              accessToken: rotatedAccessToken,
              refreshToken: 'rt-rotated-next',
              expiresIn: 900,
            },
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      const hdrs = new Headers(init?.headers);
      if (hdrs.get('Authorization') === 'Bearer jwt-initial') {
        return new Response(JSON.stringify({ status: 401, title: 'Unauthorized' }), {
          status: 401,
          headers: { 'Content-Type': 'application/problem+json' },
        });
      }

      const replayedText = init?.body ? Buffer.from(init.body as ArrayBuffer).toString('utf8') : '';
      expect(replayedText).toBe(payloadBody);
      expect(hdrs.get('Authorization')).toBe(`Bearer ${rotatedAccessToken}`);

      return new Response(JSON.stringify({ data: { jobId: 'job-123' } }), {
        status: 202,
        headers: {
          'Content-Type': 'application/json',
          ETag: '"1"',
          Location: '/api/v1/document-jobs/job-123',
          'Document-Id': 'doc-999',
          'Retry-After': '3',
          'RateLimit-Limit': '120',
          'RateLimit-Remaining': '119',
          'RateLimit-Reset': '60',
        },
      });
    });
    global.fetch = fetchMock;

    const postReq = new NextRequest('http://localhost:3001/api/proxy/v1/document-jobs', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Idempotency-Key': 'idem-key-1',
      },
      body: payloadBody,
    });

    const res = await handleProxyRequest(postReq, ['v1', 'document-jobs']);

    expect(res.status).toBe(202);
    expect(res.headers.get('etag')).toBe('"1"');
    expect(res.headers.get('location')).toBe('/api/v1/document-jobs/job-123');
    expect(res.headers.get('document-id')).toBe('doc-999');
    expect(res.headers.get('retry-after')).toBe('3');
    expect(res.headers.get('ratelimit-remaining')).toBe('119');
    expect(res.headers.get('ratelimit-reset')).toBe('60');
    expect(res.headers.get('set-cookie')).toContain('authjs.session-token=');
    // 1 initial 401 + 1 single-flight refresh + 1 replay = 3 calls
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });
});
