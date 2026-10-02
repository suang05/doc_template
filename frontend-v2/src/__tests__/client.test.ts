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

    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      text: async () => JSON.stringify(problemPayload),
      headers: new Headers({ 'content-type': 'application/problem+json' }),
    } as unknown as Response);

    await expect(apiClient('/api/test')).rejects.toThrow(ApiError);

    try {
      await apiClient('/api/test');
    } catch (err: any) {
      expect(err).toBeInstanceOf(ApiError);
      expect(err.status).toBe(400);
      expect(err.errorCode).toBe('TEMPLATE_SYNTAX_ERROR');
      expect(err.detail).toBe('Handlebars syntax error near line 14: unclosed {{#each}} block');
      expect(err.errors).toEqual({
        template: ['Missing matching {{/each}} on line 28'],
      });
      expect(err.message).toContain('TEMPLATE_SYNTAX_ERROR');
    }
  });

  it('apiClientBlob parses response headers correctly', async () => {
    const mockBlob = new Blob(['%PDF-1.4 mock content'], { type: 'application/pdf' });

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      blob: async () => mockBlob,
      headers: new Headers({
        'content-type': 'application/pdf',
        'content-disposition': 'attachment; filename="invoice_2026.pdf"',
        'content-length': '21',
      }),
    } as unknown as Response);

    const result = await apiClientBlob('/api/documents/preview/test');
    expect(result.blob).toBeDefined();
    expect(result.fileName).toBe('invoice_2026.pdf');
    expect(result.contentType).toBe('application/pdf');
    expect(result.contentLength).toBe(21);
  });

  it('passes AbortSignal to fetch request options', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ success: true }),
      headers: new Headers({ 'content-type': 'application/json' }),
    } as unknown as Response);

    global.fetch = mockFetch;

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

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      body: mockStream,
      headers: new Headers({
        'content-type': 'application/pdf',
        'content-disposition': 'inline; filename="streamed.pdf"',
      }),
    } as unknown as Response);

    const result = await apiClientStream('/api/documents/preview');
    expect(result.stream).toBe(mockStream);
    expect(result.fileName).toBe('streamed.pdf');
    expect(result.contentType).toBe('application/pdf');
  });
});
