import NextAuth, { type Session } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import type { JWT } from 'next-auth/jwt';
import { decodeBackendJwt, jwtRole, jwtGivenName, jwtFamilyName } from '@/lib/auth/jwt';

// Server-only — never exposed to the browser. Falls back to the public URL for local dev
// where the Next.js server and the backend both resolve `localhost` the same way.
const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

interface BackendLoginResponse {
  token: string;
}

async function loginAgainstBackend(email: string, password: string): Promise<string | null> {
  const res = await fetch(`${BACKEND_INTERNAL_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });

  if (!res.ok) return null;

  const data = (await res.json()) as BackendLoginResponse;
  return data.token;
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

        const backendToken = await loginAgainstBackend(email, password);
        if (!backendToken) return null;

        const payload = decodeBackendJwt(backendToken);
        if (!payload) return null;

        return {
          id: payload.sub,
          email: payload.email,
          firstName: jwtGivenName(payload),
          lastName: jwtFamilyName(payload),
          role: jwtRole(payload),
          systemRole: payload.SystemRole ?? null,
          projectId: payload.ProjectId ?? null,
          backendToken,
          backendTokenExp: payload.exp,
        };
      },
    }),
  ],
  callbacks: {
    jwt({ token, user }): JWT {
      if (user) {
        token.backendToken = user.backendToken;
        token.backendTokenExp = user.backendTokenExp;
        token.role = user.role;
        token.systemRole = user.systemRole;
        token.projectId = user.projectId;
        token.firstName = user.firstName;
        token.lastName = user.lastName;
      }
      return token;
    },
    session({ session, token }: { session: Session; token: JWT }): Session {
      session.backendToken = token.backendToken;
      session.expires = new Date(token.backendTokenExp * 1000).toISOString() as Session['expires'];
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
