import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  ApiError,
  apiClient,
  apiClientBlob,
  apiClientStream,
  apiRequestWithMeta,
} from '@/lib/api/client';

describe('ApiError and RFC 9457 Problem Details', () => {
  it('instantiates with basic message and status code', () => {
    const err = new ApiError(404, 'Resource not found');
    expect(err.name).toBe('ApiError');
    expect(err.status).toBe(404);
    expect(err.message).toBe('Resource not found');
    expect(err.errorCode).toBeUndefined();
    expect(err.fieldErrors).toEqual({});
  });

  it('maps RFC 9457 JSON pointers into fieldErrors and preserves metadata', () => {
    const err = new ApiError(
      422,
      'Validation failed',
      {
        type: 'https://smk.internal/problems/validation-error',
        title: 'Validation Failed',
        status: 422,
        detail: 'One or more validation errors occurred.',
        instance: '/api/v1/documents',
        errorCode: 'VALIDATION_FAILED',
        traceId: '00-trace-99',
        errors: [
          { pointer: '/customer/taxId', code: 'INVALID_FORMAT', message: 'Invalid tax ID' },
          { pointer: '/payload/amount', code: 'MIN_VALUE', message: 'Amount must be positive' },
        ],
      },
      {
        retryAfterSeconds: 5,
        rateLimitRemaining: 0,
        rateLimitResetSeconds: 60,
      },
    );

    expect(err.detail).toBe('One or more validation errors occurred.');
    expect(err.errorCode).toBe('VALIDATION_FAILED');
    expect(err.traceId).toBe('00-trace-99');
    expect(err.retryAfterSeconds).toBe(5);
    expect(err.rateLimitRemaining).toBe(0);
    expect(err.rateLimitResetSeconds).toBe(60);
    expect(err.fieldErrors).toEqual({
      'customer.taxId': 'Invalid tax ID',
      'payload.amount': 'Amount must be positive',
    });
  });

  it('retains 412 and 428 statuses for optimistic concurrency handling', () => {
    const err412 = new ApiError(412, 'Precondition Failed');
    const err428 = new ApiError(428, 'Precondition Required');

    expect(err412.status).toBe(412);
    expect(err428.status).toBe(428);
  });
});

describe('apiClient, ETag, Idempotency, and BFF Routing', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('parses RFC 9457 Problem Details JSON and Retry-After on error responses', async () => {
    global.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'https://smk.internal/problems/template-syntax',
          title: 'Syntax Error',
          status: 400,
          detail: 'Handlebars syntax error near line 14',
          errorCode: 'TEMPLATE_SYNTAX_ERROR',
          traceId: 'trace-123',
          errors: [
            {
              pointer: '/html',
              code: 'SYNTAX',
              message: 'Missing matching {{/each}} on line 28',
            },
          ],
        }),
        {
          status: 400,
          headers: {
            'Content-Type': 'application/problem+json',
            'Retry-After': '12',
            'RateLimit-Remaining': '4',
            'RateLimit-Reset': '30',
          },
        },
      ),
    );

    let caught: unknown;
    try {
      await apiClient('/api/v1/templates/1/html', { method: 'PUT', body: '{}' });
    } catch (err) {
      caught = err;
    }

    expect(caught).toBeInstanceOf(ApiError);
    const apiErr = caught as ApiError;
    expect(apiErr.status).toBe(400);
    expect(apiErr.detail).toBe('Handlebars syntax error near line 14');
    expect(apiErr.errorCode).toBe('TEMPLATE_SYNTAX_ERROR');
    expect(apiErr.traceId).toBe('trace-123');
    expect(apiErr.fieldErrors).toEqual({
      html: 'Missing matching {{/each}} on line 28',
    });
    expect(apiErr.retryAfterSeconds).toBe(12);
    expect(apiErr.rateLimitRemaining).toBe(4);
    expect(apiErr.rateLimitResetSeconds).toBe(30);
  });

  it('propagates 412 Precondition Failed and 428 Precondition Required conflicts cleanly', async () => {
    global.fetch = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            title: 'Precondition Failed',
            status: 412,
            detail: 'Resource version mismatch',
            errorCode: 'CONCURRENCY_CONFLICT',
          }),
          { status: 412, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            title: 'Precondition Required',
            status: 428,
            detail: 'If-Match header required',
          }),
          { status: 428, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      );

    await expect(
      apiRequestWithMeta('/api/v1/templates/1', { method: 'DELETE', ifMatch: '"1"' }),
    ).rejects.toMatchObject({
      status: 412,
      errorCode: 'CONCURRENCY_CONFLICT',
    });

    await expect(
      apiRequestWithMeta('/api/v1/templates/1', { method: 'DELETE' }),
    ).rejects.toMatchObject({
      status: 428,
    });
  });

  it('returns strong ETag and forwards If-Match, Idempotency-Key, and Project-Id headers', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ data: { id: 'abc', version: 4 } }), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          ETag: '"4"',
        },
      }),
    );
    global.fetch = fetchMock;

    const result = await apiRequestWithMeta<{ data: { id: string; version: number } }>(
      '/api/v1/templates/abc',
      {
        method: 'PUT',
        ifMatch: '"3"',
        idempotencyKey: 'idem-001',
        projectId: 'proj-99',
        body: JSON.stringify({ name: 'Updated' }),
      },
    );

    expect(result.etag).toBe('"4"');
    expect(result.data.data.version).toBe(4);

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    const sentHeaders = init.headers as Record<string, string>;
    expect(sentHeaders['If-Match']).toBe('"3"');
    expect(sentHeaders['Idempotency-Key']).toBe('idem-001');
    expect(sentHeaders['Project-Id']).toBe('proj-99');
    expect(sentHeaders['Content-Type']).toBe('application/json');
  });

  it('never auto-attaches X-API-Key header to outgoing browser requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ data: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    global.fetch = fetchMock;

    await apiClient('/api/v1/templates');

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/proxy/v1/templates');
    const headers = init.headers as Record<string, string>;
    expect(headers['X-API-Key']).toBeUndefined();
    expect(headers['x-api-key']).toBeUndefined();
  });

  it('maps /api/v1 routes via BFF proxy and keeps /api/health unproxied', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => {
      return new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    global.fetch = fetchMock;

    await apiClient('/api/v1/document-jobs');
    await apiClient('/api/health');

    expect((fetchMock.mock.calls[0] as [string])[0]).toBe('/api/proxy/v1/document-jobs');
    expect((fetchMock.mock.calls[1] as [string])[0]).toBe('/api/health');
  });

  it('apiClientBlob parses Content-Disposition filenames accurately', async () => {
    const mockBlob = new Blob(['%PDF-1.4'], { type: 'application/pdf' });
    global.fetch = vi.fn().mockResolvedValue(
      new Response(mockBlob, {
        status: 200,
        headers: {
          'Content-Type': 'application/pdf',
          'Content-Disposition': "attachment; filename=\"invoice.pdf\"; filename*=UTF-8''invoice.pdf",
          'Content-Length': '8',
        },
      }),
    );

    const res = await apiClientBlob('/api/v1/document-jobs/1/result');
    expect(res.fileName).toBe('invoice.pdf');
    expect(res.contentType).toBe('application/pdf');
    expect(res.contentLength).toBe(8);
  });

  it('passes AbortSignal and supports streaming responses', async () => {
    const controller = new AbortController();
    const fetchMock = vi.fn().mockResolvedValue(
      new Response('stream-content', {
        status: 200,
        headers: { 'Content-Type': 'application/pdf', ETag: '"1"' },
      }),
    );
    global.fetch = fetchMock;

    const streamed = await apiClientStream('/api/v1/documents/preview', {
      method: 'POST',
      signal: controller.signal,
    });

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.signal).toBe(controller.signal);
    expect(streamed.etag).toBe('"1"');
    expect(streamed.stream).toBeDefined();
  });
});
