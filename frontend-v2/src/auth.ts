import NextAuth, { type Session } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import type { JWT } from 'next-auth/jwt';
import { decodeBackendJwt, jwtRole, jwtGivenName, jwtFamilyName } from '@/lib/auth/jwt';

// Server-only — never exposed to the browser. Falls back to the public URL for local dev
// where the Next.js server and the backend both resolve `localhost` the same way.
const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

interface BackendUserData {
  id?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  role?: string;
  systemRole?: string;
}

interface BackendLoginData {
  accessToken: string;
  refreshToken?: string;
  tokenType?: string;
  expiresIn?: number;
  user?: BackendUserData;
  defaultProjectId?: string | null;
  activeProjectId?: string | null;
  activeRole?: string | null;
}

interface BackendRefreshData {
  accessToken: string;
  refreshToken: string;
  expiresIn?: number;
  tokenType?: string;
}

interface BackendApiResponse<T> {
  data?: T;
  success?: boolean;
  message?: string;
  errorCode?: string;
}

async function loginAgainstBackend(email: string, password: string): Promise<BackendLoginData | null> {
  const res = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  }).catch(() => null);

  // Fallback to /api/auth/login if /api/v1/auth/login fails with 404
  const finalRes = res && res.status !== 404
    ? res
    : await fetch(`${BACKEND_INTERNAL_URL}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      }).catch(() => null);

  if (!finalRes || !finalRes.ok) return null;

  const json = (await finalRes.json()) as BackendApiResponse<BackendLoginData> & {
    token?: string;
    accessToken?: string;
    refreshToken?: string;
  };

  const data = json.data;
  const accessToken = data?.accessToken || json.accessToken || json.token;
  if (!accessToken) return null;

  return {
    accessToken,
    refreshToken: data?.refreshToken || json.refreshToken,
    tokenType: data?.tokenType || 'Bearer',
    expiresIn: data?.expiresIn ?? 900,
    user: data?.user,
    defaultProjectId: data?.defaultProjectId,
    activeProjectId: data?.activeProjectId,
    activeRole: data?.activeRole,
  };
}

async function refreshBackendToken(token: JWT): Promise<JWT> {
  if (!token.refreshToken) {
    return { ...token, error: 'RefreshAccessTokenError' };
  }

  try {
    const res = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: token.refreshToken }),
    }).catch(() => null);

    const finalRes = res && res.status !== 404
      ? res
      : await fetch(`${BACKEND_INTERNAL_URL}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: token.refreshToken }),
        }).catch(() => null);

    if (!finalRes || !finalRes.ok) {
      return { ...token, error: 'RefreshAccessTokenError' };
    }

    const json = (await finalRes.json()) as BackendApiResponse<BackendRefreshData> & {
      accessToken?: string;
      refreshToken?: string;
      expiresIn?: number;
    };

    const data = json.data;
    const newAccessToken = data?.accessToken || json.accessToken;
    const newRefreshToken = data?.refreshToken || json.refreshToken || token.refreshToken;
    const expiresIn = data?.expiresIn || json.expiresIn || 900;

    if (!newAccessToken) {
      return { ...token, error: 'RefreshAccessTokenError' };
    }

    const payload = decodeBackendJwt(newAccessToken);

    return {
      ...token,
      backendToken: newAccessToken,
      refreshToken: newRefreshToken,
      backendTokenExp: payload?.exp ?? Math.floor(Date.now() / 1000) + expiresIn,
      role: payload ? (jwtRole(payload) ?? token.role) : token.role,
      systemRole: payload?.SystemRole ?? token.systemRole,
      projectId: payload?.ProjectId ?? token.projectId,
      error: null,
    };
  } catch {
    return { ...token, error: 'RefreshAccessTokenError' };
  }
}

const nextAuth = NextAuth({
  session: { strategy: 'jwt' },
  pages: { signIn: '/' },
  providers: [
    Credentials({
      credentials: {
        email: { label: 'Email', type: 'email' },
        password: { label: 'Password', type: 'password' },
      },
      authorize: async (credentials) => {
        const email = credentials?.email;
        const password = credentials?.password;
        if (typeof email !== 'string' || typeof password !== 'string') return null;

        const loginData = await loginAgainstBackend(email, password);
        if (!loginData) return null;

        const payload = decodeBackendJwt(loginData.accessToken);
        if (!payload) return null;

        const effectiveProjectId = loginData.activeProjectId
          ?? loginData.defaultProjectId
          ?? payload.ProjectId
          ?? null;

        return {
          id: payload.sub,
          email: payload.email,
          firstName: loginData.user?.firstName || jwtGivenName(payload),
          lastName: loginData.user?.lastName || jwtFamilyName(payload),
          role: loginData.activeRole || jwtRole(payload),
          systemRole: payload.SystemRole ?? null,
          projectId: effectiveProjectId,
          backendToken: loginData.accessToken,
          backendTokenExp: payload.exp ?? Math.floor(Date.now() / 1000) + (loginData.expiresIn ?? 900),
          refreshToken: loginData.refreshToken ?? null,
        };
      },
    }),
  ],
  callbacks: {
    async jwt({ token, user, trigger }): Promise<JWT> {
      if (user) {
        token.backendToken = user.backendToken;
        token.backendTokenExp = user.backendTokenExp;
        token.refreshToken = user.refreshToken ?? null;
        token.role = user.role;
        token.systemRole = user.systemRole;
        token.projectId = user.projectId;
        token.firstName = user.firstName;
        token.lastName = user.lastName;
        token.error = null;
        return token;
      }

      // Refresh if token is expiring within 60 seconds or if update was explicitly triggered
      const shouldRefresh = trigger === 'update' || Date.now() >= (token.backendTokenExp - 60) * 1000;
      if (shouldRefresh && token.refreshToken) {
        return await refreshBackendToken(token);
      }

      return token;
    },
    session({ session, token }: { session: Session; token: JWT }): Session {
      session.backendToken = token.backendToken;
      session.expires = new Date(token.backendTokenExp * 1000).toISOString() as Session['expires'];
      session.error = token.error ?? null;
      session.user.id = token.sub ?? '';
      session.user.email = token.email ?? '';
      session.user.role = token.role;
      session.user.systemRole = token.systemRole;
      session.user.projectId = token.projectId;
      session.user.firstName = token.firstName;
      session.user.lastName = token.lastName;
      return session;
    },
  },
});

export const { handlers, auth, signIn, signOut } = nextAuth;
export const { GET, POST } = handlers;
