// Requests go through the Next.js proxy (`/api/proxy/*`) instead of hitting the backend
// directly — the proxy attaches the JWT from the httpOnly session cookie server-side, so
// the browser never has access to the token (see src/app/api/proxy/[...path]/route.ts).
const API_BASE_URL = '/api/proxy';
const STORAGE_KEY = 'smk_api_key';

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class ApiError extends Error {
  public errorCode?: string;
  public detail?: string;
  public errors?: Record<string, string[]>;
  public problemDetails?: ProblemDetails;

  constructor(
    public status: number,
    public message: string,
    public data?: unknown
  ) {
    super(message);
    this.name = 'ApiError';

    if (data && typeof data === 'object') {
      const p = data as ProblemDetails;
      this.errorCode = p.errorCode;
      this.detail = p.detail;
      this.errors = p.errors;
      this.problemDetails = p;
    }
  }
}

export const getStoredApiKey = (): string => {
  const defaultKey = process.env.NEXT_PUBLIC_DEFAULT_API_KEY || '';
  if (typeof window === 'undefined') return defaultKey;
  return localStorage.getItem(STORAGE_KEY) || defaultKey;
};

export function setStoredApiKey(key: string): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, key.trim());
}

// ── Shared helpers ──────────────────────────────────────────────────────────

function buildHeaders(extra: Record<string, string> = {}): Record<string, string> {
  const headers: Record<string, string> = { ...extra };
  const apiKey = getStoredApiKey();

  if (apiKey) headers['X-API-Key'] = apiKey;

  return headers;
}

function prepareJsonHeaders(options: RequestInit): Record<string, string> {
  const headers = buildHeaders(options.headers as Record<string, string>);
  
  if (!(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }
  
  return headers;
}

function dispatchUnauthorized(endpoint: string): void {
  if (typeof window === 'undefined') return;
  if (endpoint.includes('/auth/login')) return;
  
  window.dispatchEvent(new CustomEvent('auth:unauthorized'));
}

interface ParsedErrorPayload {
  message: string;
  data?: unknown;
}

async function parseErrorPayload(response: Response): Promise<ParsedErrorPayload> {
  const fallbackMsg = `HTTP Error ${response.status}: ${response.statusText}`;
  
  try {
    const text = await response.text();
    if (!text) return { message: fallbackMsg };

    try {
      const json = JSON.parse(text) as Record<string, any>;
      // RFC 7807 Problem Details inspection
      let message = json.detail || json.title || json.message || json.error || fallbackMsg;
      if (json.errorCode && !message.includes(json.errorCode)) {
        message = `[${json.errorCode}] ${message}`;
      }
      return { message, data: json };
    } catch {
      return { message: text }; // Not JSON, return raw text
    }
  } catch {
    return { message: fallbackMsg }; // Failed to read response body
  }
}

async function assertOk(response: Response, endpoint: string): Promise<void> {
  if (response.ok) return;

  const { message, data } = await parseErrorPayload(response);
  
  if (response.status === 401) {
    dispatchUnauthorized(endpoint);
  }
  
  throw new ApiError(response.status, message, data);
}

function resolveUrl(endpoint: string): string {
  const normalizedEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
  // The proxy re-applies the `/api` prefix itself (see [...path]/route.ts), so strip it here.
  const withoutApiPrefix = normalizedEndpoint.startsWith('/api/')
    ? normalizedEndpoint.slice(4)
    : normalizedEndpoint;
  return `${API_BASE_URL}${withoutApiPrefix}`;
}

function extractFileName(disposition: string | null): string | undefined {
  if (!disposition || !disposition.includes('filename=')) return undefined;
  
  const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
  return match?.[1]?.replace(/['"]/g, '');
}

// ── Public API ──────────────────────────────────────────────────────────────

export async function apiClient<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const headers = prepareJsonHeaders(options);
  const response = await fetch(resolveUrl(endpoint), { ...options, headers });
  
  await assertOk(response, endpoint);
  
  if (response.status === 204) return {} as T;
  return response.json() as Promise<T>;
}

export async function apiClientText(endpoint: string, options: RequestInit = {}): Promise<string> {
  const headers = buildHeaders(options.headers as Record<string, string>);
  const response = await fetch(resolveUrl(endpoint), { ...options, headers });
  
  await assertOk(response, endpoint);
  
  return response.text();
}

export interface ApiBlobResult {
  blob: Blob;
  fileName?: string;
  contentType?: string;
  contentLength?: number;
}

export async function apiClientBlob(
  endpoint: string,
  options: RequestInit = {}
): Promise<ApiBlobResult> {
  const headers = prepareJsonHeaders(options);
  const response = await fetch(resolveUrl(endpoint), { ...options, headers });
  
  await assertOk(response, endpoint);

  const disposition = response.headers.get('content-disposition');
  const contentType = response.headers.get('content-type') || undefined;
  const contentLengthHeader = response.headers.get('content-length');
  const contentLength = contentLengthHeader ? parseInt(contentLengthHeader, 10) : undefined;

  return { 
    blob: await response.blob(), 
    fileName: extractFileName(disposition),
    contentType,
    contentLength,
  };
}

export interface ApiStreamResult {
  stream: ReadableStream<Uint8Array>;
  response: Response;
  fileName?: string;
  contentType?: string;
}

export async function apiClientStream(
  endpoint: string,
  options: RequestInit = {}
): Promise<ApiStreamResult> {
  const headers = prepareJsonHeaders(options);
  const response = await fetch(resolveUrl(endpoint), { ...options, headers });

  await assertOk(response, endpoint);

  if (!response.body) {
    throw new ApiError(response.status, 'Response body is empty');
  }

  const disposition = response.headers.get('content-disposition');
  const contentType = response.headers.get('content-type') || undefined;

  return {
    stream: response.body,
    response,
    fileName: extractFileName(disposition),
    contentType,
  };
}
