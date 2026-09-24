/**
 * API Client Core Gateway
 * Handles authentication header, baseUrl configuration, and error unwrapping.
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
const STORAGE_KEY = 'smk_api_key';

export class ApiError extends Error {
  constructor(
    public status: number,
    public message: string,
    public data?: any
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

// Helper to retrieve API key dynamically
export const getStoredApiKey = (): string => {
  const defaultKey = process.env.NEXT_PUBLIC_DEFAULT_API_KEY || '';
  if (typeof window === 'undefined') return defaultKey;
  return localStorage.getItem(STORAGE_KEY) || defaultKey;
};

export function setStoredApiKey(key: string): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, key.trim());
}

export async function apiClient<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const apiKey = getStoredApiKey();
  const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;

  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string> || {}),
  };

  if (apiKey) {
    headers['X-API-Key'] = apiKey;
  }

  // If not FormData, default to application/json
  if (!(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMessage = `HTTP Error ${response.status}: ${response.statusText}`;
    let errorData = null;
    try {
      const text = await response.text();
      if (text) {
        try {
          const json = JSON.parse(text);
          errorData = json;
          errorMessage = json.error || json.message || errorMessage;
        } catch {
          errorMessage = text;
        }
      }
    } catch {
      // ignore read error
    }
    throw new ApiError(response.status, errorMessage, errorData);
  }

  // Handle empty responses
  if (response.status === 204) {
    return {} as T;
  }

  return response.json();
}

export async function apiClientText(
  endpoint: string,
  options: RequestInit = {}
): Promise<string> {
  const apiKey = getStoredApiKey();
  const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;

  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string> || {}),
  };

  if (apiKey) {
    headers['X-API-Key'] = apiKey;
  }

  const response = await fetch(url, { ...options, headers });

  if (!response.ok) {
    let errorMessage = `HTTP Error ${response.status}: ${response.statusText}`;
    try {
      const text = await response.text();
      if (text) errorMessage = text;
    } catch { /* ignore */ }
    throw new ApiError(response.status, errorMessage);
  }

  return response.text();
}

export async function apiClientBlob(
  endpoint: string,
  options: RequestInit = {}
): Promise<{ blob: Blob; fileName?: string }> {
  const apiKey = getStoredApiKey();
  const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;

  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string> || {}),
  };

  if (apiKey) {
    headers['X-API-Key'] = apiKey;
  }

  // If not FormData, default to application/json
  if (!(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMessage = `HTTP Error ${response.status}: ${response.statusText}`;
    try {
      const text = await response.text();
      if (text) {
        try {
          const json = JSON.parse(text);
          errorMessage = json.error || json.message || errorMessage;
        } catch {
          errorMessage = text;
        }
      }
    } catch {
      // ignore read error
    }
    throw new ApiError(response.status, errorMessage);
  }

  const disposition = response.headers.get('content-disposition');
  let fileName: string | undefined;
  if (disposition && disposition.includes('filename=')) {
    const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
    if (match && match[1]) {
      fileName = match[1].replace(/['"]/g, '');
    }
  }

  const blob = await response.blob();
  return { blob, fileName };
}
