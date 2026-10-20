import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  ApiError,
  apiClient,
  apiClientBlob,
  apiClientStream,
  ProblemDetails,
} from '@/lib/api/client';

describe('ApiError and RFC 7807 Problem Details', () => {
  it('instantiates with basic message and status code', () => {
    const error = new ApiError(404, 'Resource not found');
    expect(error.status).toBe(404);
    expect(error.message).toBe('Resource not found');
    expect(error.errorCode).toBeUndefined();
    expect(error.detail).toBeUndefined();
  });

  it('extracts RFC 7807 problem details properties when data is provided', () => {
    const problem: ProblemDetails = {
      type: 'https://tools.ietf.org/html/rfc7807',
      title: 'Validation Failed',
      status: 422,
      detail: 'The provided data schema is invalid',
      errorCode: 'SCHEMA_VALIDATION_FAILED',
      errors: {
        'customer.name': ['Name is required'],
        'payment.amount': ['Amount must be greater than zero'],
      },
    };

    const error = new ApiError(422, 'The provided data schema is invalid', problem);
    expect(error.status).toBe(422);
    expect(error.errorCode).toBe('SCHEMA_VALIDATION_FAILED');
    expect(error.detail).toBe('The provided data schema is invalid');
    expect(error.errors).toEqual(problem.errors);
    expect(error.problemDetails).toEqual(problem);
  });
});

describe('apiClient and RFC 7807 Response Handling', () => {
  const originalFetch = global.fetch;

  function setupMockFetch(
    overrides: Record<string, unknown> = {},
    headersInit: HeadersInit = { 'content-type': 'application/json' },
  ) {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ success: true }),
      headers: new Headers(headersInit),
      ...overrides,
    } as unknown as Response);
    global.fetch = mockFetch;
    return mockFetch;
  }

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('parses RFC 7807 problem details JSON on 400 Bad Request', async () => {
    const problemPayload = {
      type: 'about:blank',
      title: 'Template Compilation Error',
      status: 400,
      detail: 'Handlebars syntax error near line 14: unclosed {{#each}} block',
      errorCode: 'TEMPLATE_SYNTAX_ERROR',
      errors: {
        template: ['Missing matching {{/each}} on line 28'],
      },
    };

    setupMockFetch(
      {
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        text: async () => JSON.stringify(problemPayload),
      },
      { 'content-type': 'application/problem+json' },
    );

    await expect(apiClient('/api/test')).rejects.toMatchObject({
      name: 'ApiError',
      status: 400,
      errorCode: 'TEMPLATE_SYNTAX_ERROR',
      detail: 'Handlebars syntax error near line 14: unclosed {{#each}} block',
      errors: {
        template: ['Missing matching {{/each}} on line 28'],
      },
    });
  });

  it('apiClientBlob parses response headers correctly', async () => {
    const mockBlob = new Blob(['%PDF-1.4 mock content'], { type: 'application/pdf' });

    setupMockFetch(
      { blob: async () => mockBlob },
      {
        'content-type': 'application/pdf',
        'content-disposition': 'attachment; filename="invoice_2026.pdf"',
        'content-length': '21',
      },
    );

    const result = await apiClientBlob('/api/documents/preview/test');
    expect(result.blob).toBeDefined();
    expect(result.fileName).toBe('invoice_2026.pdf');
    expect(result.contentType).toBe('application/pdf');
    expect(result.contentLength).toBe(21);
  });

  it('passes AbortSignal to fetch request options', async () => {
    const mockFetch = setupMockFetch();

    const controller = new AbortController();
    await apiClient('/api/test', { signal: controller.signal });

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const callArgs = mockFetch.mock.calls[0];
    expect(callArgs[1].signal).toBe(controller.signal);
  });

  it('apiClientStream returns readable stream and response', async () => {
    const mockStream = new ReadableStream({
      start(ctrl) {
        ctrl.enqueue(new Uint8Array([1, 2, 3]));
        ctrl.close();
      },
    });

    setupMockFetch(
      { body: mockStream },
      {
        'content-type': 'application/pdf',
        'content-disposition': 'inline; filename="streamed.pdf"',
      },
    );

    const result = await apiClientStream('/api/documents/preview');
    expect(result.stream).toBe(mockStream);
    expect(result.fileName).toBe('streamed.pdf');
    expect(result.contentType).toBe('application/pdf');
  });

  it('does not automatically attach X-API-Key from localStorage to request headers', async () => {
    const mockFetch = setupMockFetch();

    if (typeof window !== 'undefined') {
      window.localStorage.setItem('smk_api_key', 'test-secret-key-123');
    }

    await apiClient('/api/templates');

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const headers = mockFetch.mock.calls[0][1].headers;
    expect(headers['X-API-Key']).toBeUndefined();
    expect(headers['Content-Type']).toBe('application/json');
  });

  it('preserves explicitly provided headers in options', async () => {
    const mockFetch = setupMockFetch();

    await apiClient('/api/m2m-test', {
      headers: {
        'X-API-Key': 'explicit-key-456',
        'X-Project-Id': 'proj-789',
      },
    });

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const headers = mockFetch.mock.calls[0][1].headers;
    expect(headers['X-API-Key']).toBe('explicit-key-456');
    expect(headers['X-Project-Id']).toBe('proj-789');
  });
});
