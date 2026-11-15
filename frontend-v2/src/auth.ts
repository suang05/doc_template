import NextAuth, { type Session } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import type { JWT } from 'next-auth/jwt';
import { decodeBackendJwt, jwtRole, jwtGivenName, jwtFamilyName, shouldRefreshBackendToken } from '@/lib/auth/jwt';
import { applyRotatedTokens, BACKEND_INTERNAL_URL, rotateBackendRefreshToken } from '@/lib/auth/refreshCoordinator';
import type { ApiEnvelope, LoginResultDto } from '@/lib/api/types';

async function loginAgainstBackend(
  email: string,
  password: string,
): Promise<(LoginResultDto & { accessToken: string }) | null> {
  const res = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  }).catch(() => null);

  if (!res || !res.ok) return null;

  const json = (await res.json()) as ApiEnvelope<LoginResultDto | null>;
  const accessToken = json.data?.accessToken;
  if (!json.data || !accessToken) return null;
  return { ...json.data, accessToken };
}

async function refreshBackendToken(token: JWT): Promise<JWT> {
  if (!token.refreshToken) {
    return { ...token, error: 'RefreshAccessTokenError' };
  }

  try {
    const rotated = await rotateBackendRefreshToken(token.refreshToken);
    return applyRotatedTokens(token, rotated);
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

      // Refresh if token is expiring within threshold or if update was explicitly triggered
      const shouldRefresh = shouldRefreshBackendToken(token.backendTokenExp, trigger);
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
