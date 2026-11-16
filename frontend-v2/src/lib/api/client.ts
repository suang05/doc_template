import {
  mapPointerErrors,
  parseRetryAfterSeconds,
  problemDetailsSchema,
  type ProblemDetailsErrorItem,
  type ProblemDetailsPayload,
} from './problemDetails';
import type { ResourceWithETag } from './types';

export type ProblemDetails = ProblemDetailsPayload;
export type { ProblemDetailsErrorItem };

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '';

export interface RequestConfig extends RequestInit {
  ifMatch?: string | null;
  idempotencyKey?: string;
  projectId?: string;
}

export interface ErrorRateLimitMeta {
  retryAfterSeconds?: number;
  rateLimitRemaining?: number;
  rateLimitResetSeconds?: number;
}

export interface BlobDownloadResult {
  blob: Blob;
  fileName: string | null;
  contentType: string;
  contentLength: number | null;
  etag: string | null;
  headers: Headers;
}

export interface StreamDownloadResult {
  stream: ReadableStream<Uint8Array>;
  fileName: string | null;
  contentType: string;
  etag: string | null;
  headers: Headers;
}

export class ApiError extends Error {
  status: number;
  problemDetails?: ProblemDetailsPayload;
  errorCode?: string;
  traceId?: string;
  detail?: string;
  fieldErrors: Record<string, string>;
  rawErrors: ProblemDetailsErrorItem[];
  retryAfterSeconds?: number;
  rateLimitRemaining?: number;
  rateLimitResetSeconds?: number;

  constructor(
    status: number,
    message: string,
    problemDetails?: ProblemDetailsPayload,
    meta?: ErrorRateLimitMeta,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problemDetails = problemDetails;
    this.errorCode = problemDetails?.errorCode;
    this.traceId = problemDetails?.traceId;
    this.detail = problemDetails?.detail;
    this.rawErrors = problemDetails?.errors ?? [];
    this.fieldErrors = mapPointerErrors(problemDetails?.errors);
    this.retryAfterSeconds = meta?.retryAfterSeconds;
    this.rateLimitRemaining = meta?.rateLimitRemaining;
    this.rateLimitResetSeconds = meta?.rateLimitResetSeconds;
  }
}

export function extractErrorMessage(
  err: unknown,
  fallback = 'เกิดข้อผิดพลาดในการเชื่อมต่อระบบ',
): string {
  if (err instanceof ApiError) {
    return err.detail || err.message || fallback;
  }
  if (err instanceof Error && err.message.trim().length > 0) {
    return err.message;
  }
  if (typeof err === 'string' && err.trim().length > 0) {
    return err;
  }
  return fallback;
}

/**
 * Rewrites backend-relative `/api/v1/*` paths cleanly to Next.js BFF `/api/proxy/v1/*`
 * streaming proxy endpoints while leaving already-proxied URLs, `blob:`, `data:`,
 * `/api/auth/*`, and `/api/health` untouched.
 */
export function toBffProxyUrl(urlOrPath: string): string {
  const trimmed = urlOrPath.trim();
  if (!trimmed) return '#';

  if (
    trimmed.startsWith('blob:') ||
    trimmed.startsWith('data:') ||
    trimmed.startsWith('/api/proxy/') ||
    trimmed.startsWith('/api/auth/') ||
    trimmed === '/api/health'
  ) {
    return trimmed;
  }

  if (trimmed.startsWith('/api/v1/')) {
    return `/api/proxy${trimmed.slice(4)}`;
  }

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    try {
      const parsed = new URL(trimmed);
      if (parsed.pathname.startsWith('/api/v1/')) {
        return `/api/proxy${parsed.pathname.slice(4)}${parsed.search}${parsed.hash}`;
      }
    } catch {
      return trimmed;
    }
  }

  return trimmed;
}

function resolveRequestUrl(endpoint: string): string {
  if (endpoint.startsWith('http://') || endpoint.startsWith('https://')) {
    return endpoint;
  }
  if (endpoint.startsWith('/api/')) {
    return toBffProxyUrl(endpoint);
  }
  return `${API_BASE_URL}${endpoint}`;
}

function buildPlainHeaders(
  config: RequestConfig,
  defaultJsonContentType: boolean,
): Record<string, string> {
  const headers: Record<string, string> = {};

  if (config.headers) {
    if (config.headers instanceof Headers) {
      config.headers.forEach((value, key) => {
        headers[key] = value;
      });
    } else if (Array.isArray(config.headers)) {
      for (const [key, value] of config.headers) {
        headers[key] = value;
      }
    } else {
      Object.assign(headers, config.headers);
    }
  }

  const hasContentType = Object.keys(headers).some(
    (k) => k.toLowerCase() === 'content-type',
  );
  if (
    defaultJsonContentType &&
    !hasContentType &&
    !(config.body instanceof FormData)
  ) {
    headers['Content-Type'] = 'application/json';
  }

  if (config.ifMatch && config.ifMatch.trim().length > 0) {
    headers['If-Match'] = config.ifMatch.trim();
  }
  if (config.idempotencyKey && config.idempotencyKey.trim().length > 0) {
    headers['Idempotency-Key'] = config.idempotencyKey.trim();
  }
  if (config.projectId && config.projectId.trim().length > 0) {
    headers['Project-Id'] = config.projectId.trim();
  }

  return headers;
}

function parseOptionalInt(value: string | null): number | undefined {
  if (!value) return undefined;
  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : undefined;
}

function extractFallbackMessage(raw: unknown): string | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const record = raw as Record<string, unknown>;
  if (typeof record.detail === 'string' && record.detail.length > 0) {
    return record.detail;
  }
  if (typeof record.message === 'string' && record.message.length > 0) {
    return record.message;
  }
  if (typeof record.title === 'string' && record.title.length > 0) {
    return record.title;
  }
  if (typeof record.error === 'string' && record.error.length > 0) {
    return record.error;
  }
  return undefined;
}

function getHeaderCaseInsensitive(headers: Headers | undefined, name: string): string | null {
  if (!headers || typeof headers.get !== 'function') return null;
  return headers.get(name) ?? headers.get(name.toLowerCase());
}

async function parseErrorResponse(response: Response): Promise<ApiError> {
  const defaultMessage = `HTTP Error ${response.status}: ${response.statusText || 'Request failed'}`;
  const retryAfterSeconds = parseRetryAfterSeconds(
    getHeaderCaseInsensitive(response.headers, 'Retry-After'),
  );
  const rateLimitRemaining = parseOptionalInt(
    getHeaderCaseInsensitive(response.headers, 'RateLimit-Remaining') ??
      getHeaderCaseInsensitive(response.headers, 'X-RateLimit-Remaining'),
  );
  const rateLimitResetSeconds = parseOptionalInt(
    getHeaderCaseInsensitive(response.headers, 'RateLimit-Reset') ??
      getHeaderCaseInsensitive(response.headers, 'X-RateLimit-Reset'),
  );
  const meta: ErrorRateLimitMeta = {
    retryAfterSeconds,
    rateLimitRemaining,
    rateLimitResetSeconds,
  };

  let problemDetails: ProblemDetailsPayload | undefined;
  let message = defaultMessage;

  try {
    const raw: unknown = await response.json();
    const parsed = problemDetailsSchema.safeParse(raw);
    if (parsed.success) {
      problemDetails = parsed.data;
    }
    const resolvedMessage = extractFallbackMessage(raw);
    if (resolvedMessage) {
      message = resolvedMessage;
    }
  } catch {
    // Response body is not JSON; retain HTTP status message.
  }

  return new ApiError(response.status, message, problemDetails, meta);
}

function handleUnauthorizedSession(endpoint: string, status: number): void {
  if (
    status === 401 &&
    typeof window !== 'undefined' &&
    !endpoint.includes('/auth/login') &&
    !endpoint.includes('/auth/refresh') &&
    !window.location.pathname.startsWith('/login') &&
    !window.location.pathname.startsWith('/register')
  ) {
    const redirectTarget = `/login?expired=true&callbackUrl=${encodeURIComponent(
      window.location.pathname + window.location.search,
    )}`;
    window.location.assign(redirectTarget);
  }
}

function parseContentDispositionFilename(disposition: string | null): string | null {
  if (!disposition) return null;
  const utfMatch = disposition.match(/filename\*\s*=\s*UTF-8''([^;\n]+)/i);
  if (utfMatch?.[1]) {
    try {
      return decodeURIComponent(utfMatch[1].replace(/['"]/g, '').trim());
    } catch {
      return utfMatch[1].replace(/['"]/g, '').trim();
    }
  }
  const plainMatch = disposition.match(/filename[^;=\n]*=\s*((['"]).*?\2|[^;\n]*)/i);
  return plainMatch?.[1] ? plainMatch[1].replace(/['"]/g, '').trim() : null;
}

function toFetchInit(config: RequestConfig, headers: Record<string, string>): RequestInit {
  const { ifMatch: _ifMatch, idempotencyKey: _idemp, projectId: _pid, headers: _h, ...rest } =
    config;
  return {
    ...rest,
    headers,
  };
}

async function executeRequest(
  endpoint: string,
  config: RequestConfig = {},
  defaultJsonContentType = true,
): Promise<Response> {
  const url = resolveRequestUrl(endpoint);
  const headers = buildPlainHeaders(config, defaultJsonContentType);
  const response = await fetch(url, toFetchInit(config, headers));

  if (!response.ok) {
    handleUnauthorizedSession(endpoint, response.status);
    throw await parseErrorResponse(response);
  }

  return response;
}

function extractDownloadMetadata(headers: Headers) {
  const disposition = getHeaderCaseInsensitive(headers, 'content-disposition');
  return {
    fileName: parseContentDispositionFilename(disposition),
    contentType: getHeaderCaseInsensitive(headers, 'content-type') || 'application/octet-stream',
    etag: getHeaderCaseInsensitive(headers, 'ETag'),
  };
}

export async function apiRequestWithMeta<T>(
  endpoint: string,
  config: RequestConfig = {},
): Promise<ResourceWithETag<T>> {
  const response = await executeRequest(endpoint, config, true);
  const etag = getHeaderCaseInsensitive(response.headers, 'ETag');

  if (response.status === 204 || response.status === 205) {
    return { data: undefined as T, etag, headers: response.headers };
  }

  const contentType = getHeaderCaseInsensitive(response.headers, 'content-type') || '';
  if (contentType && !contentType.includes('application/json') && !contentType.includes('+json')) {
    return { data: undefined as T, etag, headers: response.headers };
  }

  const data = (await response.json()) as T;
  return { data, etag, headers: response.headers };
}

export async function apiClient<T>(
  endpoint: string,
  config: RequestConfig = {},
): Promise<T> {
  const result = await apiRequestWithMeta<T>(endpoint, config);
  return result.data;
}

export async function apiClientText(
  endpoint: string,
  config: RequestConfig = {},
): Promise<string> {
  const response = await executeRequest(endpoint, config, false);
  return response.text();
}

export async function apiClientBlob(
  endpoint: string,
  config: RequestConfig = {},
): Promise<BlobDownloadResult> {
  const response = await executeRequest(endpoint, config, true);
  const { fileName, contentType, etag } = extractDownloadMetadata(response.headers);
  const lengthStr = getHeaderCaseInsensitive(response.headers, 'content-length');
  const parsedLen = lengthStr ? Number.parseInt(lengthStr, 10) : NaN;
  const contentLength = Number.isFinite(parsedLen) ? parsedLen : null;

  const blob = await response.blob();
  return { blob, fileName, contentType, contentLength, etag, headers: response.headers };
}

export async function apiClientStream(
  endpoint: string,
  config: RequestConfig = {},
): Promise<StreamDownloadResult> {
  const response = await executeRequest(endpoint, config, true);

  if (!response.body) {
    throw new ApiError(response.status, 'Response body is empty');
  }

  const { fileName, contentType, etag } = extractDownloadMetadata(response.headers);

  return {
    stream: response.body,
    fileName,
    contentType,
    etag,
    headers: response.headers,
  };
}
