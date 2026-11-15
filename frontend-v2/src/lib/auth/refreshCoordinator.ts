import { NextRequest, NextResponse } from 'next/server';
import { encode, getToken, type JWT } from 'next-auth/jwt';
import { decodeBackendJwt, jwtRole } from './jwt';

export const ACTIVE_PROJECT_COOKIE = 'smk_active_project_id';
export const STANDARD_SESSION_COOKIE = 'authjs.session-token';
export const SECURE_SESSION_COOKIE = '__Secure-authjs.session-token';
const SESSION_MAX_AGE_SECONDS = 30 * 24 * 60 * 60;
const DEFAULT_REPLAY_GRACE_MS = 3000;

export const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

export interface RotatedBackendTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  exp: number;
  systemRole: string | null;
  projectId: string | null;
  role: string | null;
}

interface RefreshCoordinatorOptions {
  fetchImpl?: typeof fetch;
  replayGraceMs?: number;
}

interface RecentRefreshEntry {
  result: RotatedBackendTokens;
  expiresAtMs: number;
}

interface GlobalRefreshStore {
  inFlight: Map<string, Promise<RotatedBackendTokens>>;
  recent: Map<string, RecentRefreshEntry>;
}

const GLOBAL_STORE_SYMBOL = Symbol.for('smk.doc.auth.refreshCoordinator');

function getStore(): GlobalRefreshStore {
  const g = globalThis as Record<symbol, GlobalRefreshStore | undefined>;
  if (!g[GLOBAL_STORE_SYMBOL]) {
    g[GLOBAL_STORE_SYMBOL] = {
      inFlight: new Map(),
      recent: new Map(),
    };
  }
  return g[GLOBAL_STORE_SYMBOL];
}

export function resetRefreshCoordinator(): void {
  const store = getStore();
  store.inFlight.clear();
  store.recent.clear();
}

export function getAuthSecret(): string {
  return process.env.AUTH_SECRET || process.env.NEXTAUTH_SECRET || '';
}

interface BackendRefreshEnvelope {
  data?: {
    accessToken?: string;
    refreshToken?: string;
    expiresIn?: number;
  } | null;
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
}

/**
 * Coordinates single-flight refresh token rotation per incoming refresh token.
 * Concurrent 401 requests or overlapping Auth.js jwt() callbacks share one backend call,
 * avoiding false TokenReuseCompromiseException revocations.
 */
export async function rotateBackendRefreshToken(
  refreshToken: string,
  options: RefreshCoordinatorOptions = {},
): Promise<RotatedBackendTokens> {
  const trimmedToken = refreshToken.trim();
  if (!trimmedToken) {
    throw new Error('Refresh token is missing');
  }

  const store = getStore();
  const nowMs = Date.now();
  const graceMs = options.replayGraceMs ?? DEFAULT_REPLAY_GRACE_MS;

  const recent = store.recent.get(trimmedToken);
  if (recent && recent.expiresAtMs > nowMs) {
    return recent.result;
  }
  if (recent) {
    store.recent.delete(trimmedToken);
  }

  const existing = store.inFlight.get(trimmedToken);
  if (existing) {
    return existing;
  }

  const fetchImpl = options.fetchImpl ?? fetch;

  const refreshPromise = (async (): Promise<RotatedBackendTokens> => {
    const response = await fetchImpl(`${BACKEND_INTERNAL_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: trimmedToken }),
      cache: 'no-store',
    });

    if (!response.ok) {
      throw new Error(`Refresh failed with HTTP ${response.status}`);
    }

    const json = (await response.json()) as BackendRefreshEnvelope;
    const payloadData = json.data ?? undefined;
    const accessToken = payloadData?.accessToken ?? json.accessToken;
    const nextRefreshToken = payloadData?.refreshToken ?? json.refreshToken ?? trimmedToken;
    const expiresIn = payloadData?.expiresIn ?? json.expiresIn ?? 900;

    if (!accessToken) {
      throw new Error('Refresh response missing accessToken');
    }

    const decoded = decodeBackendJwt(accessToken);
    const exp = decoded?.exp ?? Math.floor(Date.now() / 1000) + expiresIn;

    const result: RotatedBackendTokens = {
      accessToken,
      refreshToken: nextRefreshToken,
      expiresIn,
      exp,
      systemRole: decoded?.SystemRole ?? null,
      projectId: decoded?.ProjectId ?? null,
      role: decoded ? jwtRole(decoded) : null,
    };

    if (graceMs > 0) {
      if (store.recent.size > 500) {
        const oldestKey = store.recent.keys().next().value;
        if (oldestKey) store.recent.delete(oldestKey);
      }
      store.recent.set(trimmedToken, {
        result,
        expiresAtMs: Date.now() + graceMs,
      });
    }

    return result;
  })();

  store.inFlight.set(trimmedToken, refreshPromise);
  try {
    return await refreshPromise;
  } finally {
    store.inFlight.delete(trimmedToken);
  }
}

export interface ResolvedSessionJwt {
  token: JWT;
  cookieName: string;
}

export function applyRotatedTokens(token: JWT, rotated: RotatedBackendTokens): JWT {
  return {
    ...token,
    backendToken: rotated.accessToken,
    refreshToken: rotated.refreshToken,
    backendTokenExp: rotated.exp,
    systemRole: rotated.systemRole ?? token.systemRole ?? null,
    projectId: rotated.projectId ?? token.projectId ?? null,
    role: rotated.role ?? token.role ?? null,
    error: null,
  };
}

export function bffProblemJson(status: number, title: string, detail: string): NextResponse {
  return NextResponse.json(
    {
      type: `https://httpstatuses.io/${status}`,
      title,
      status,
      detail,
    },
    { status },
  );
}

export async function readSessionJwt(req: NextRequest): Promise<ResolvedSessionJwt | null> {
  const secret = getAuthSecret();
  if (!secret) return null;

  const candidates = req.cookies.has(SECURE_SESSION_COOKIE)
    ? [SECURE_SESSION_COOKIE, STANDARD_SESSION_COOKIE]
    : [STANDARD_SESSION_COOKIE, SECURE_SESSION_COOKIE];

  for (const cookieName of candidates) {
    if (!req.cookies.has(cookieName)) continue;
    const token = await getToken({
      req,
      secret,
      cookieName,
      salt: cookieName,
    });
    if (token) {
      return { token, cookieName };
    }
  }

  return null;
}

export async function attachSessionCookie(
  response: NextResponse,
  cookieName: string,
  updatedToken: JWT,
): Promise<boolean> {
  const secret = getAuthSecret();
  if (!secret) return false;

  const encoded = await encode({
    token: updatedToken,
    secret,
    salt: cookieName,
    maxAge: SESSION_MAX_AGE_SECONDS,
  });

  const isSecure = cookieName.startsWith('__Secure-');
  response.cookies.set(cookieName, encoded, {
    httpOnly: true,
    sameSite: 'lax',
    path: '/',
    secure: isSecure,
    maxAge: SESSION_MAX_AGE_SECONDS,
  });
  return true;
}

export function clearSessionCookies(response: NextResponse): void {
  response.cookies.delete(STANDARD_SESSION_COOKIE);
  response.cookies.delete(SECURE_SESSION_COOKIE);
  response.cookies.delete(ACTIVE_PROJECT_COOKIE);
}
