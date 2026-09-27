// Requests go through the Next.js proxy (`/api/proxy/*`) instead of hitting the backend
// directly — the proxy attaches the JWT from the httpOnly session cookie server-side, so
// the browser never has access to the token (see src/app/api/proxy/[...path]/route.ts).
const API_BASE_URL = '/api/proxy';
const STORAGE_KEY = 'smk_api_key';

export class ApiError extends Error {
  constructor(
    public status: number,
    public message: string,
    public data?: unknown
  ) {
    super(message);
    this.name = 'ApiError';
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

async function parseErrorMessage(response: Response): Promise<string> {
  const fallbackMsg = `HTTP Error ${response.status}: ${response.statusText}`;
  
  try {
    const text = await response.text();
    if (!text) return fallbackMsg;

    try {
      const json = JSON.parse(text) as Record<string, string>;
      return json['error'] || json['message'] || fallbackMsg;
    } catch {
      return text; // Not JSON, return raw text
    }
  } catch {
    return fallbackMsg; // Failed to read response body
  }
}

async function assertOk(response: Response, endpoint: string): Promise<void> {
  if (response.ok) return;

  const msg = await parseErrorMessage(response);
  
  if (response.status === 401) {
    dispatchUnauthorized(endpoint);
  }
  
  throw new ApiError(response.status, msg);
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

export async function apiClientBlob(
  endpoint: string,
  options: RequestInit = {}
): Promise<{ blob: Blob; fileName?: string }> {
  const headers = prepareJsonHeaders(options);
  const response = await fetch(resolveUrl(endpoint), { ...options, headers });
  
  await assertOk(response, endpoint);

  const disposition = response.headers.get('content-disposition');
  return { 
    blob: await response.blob(), 
    fileName: extractFileName(disposition) 
  };
}
