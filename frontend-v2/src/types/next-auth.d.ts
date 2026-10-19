import type { DefaultSession } from 'next-auth';

declare module 'next-auth' {
  interface User {
    role: string | null;
    systemRole?: string | null;
    projectId: string | null;
    firstName: string;
    lastName: string;
    backendToken: string;
    backendTokenExp: number;
    refreshToken?: string | null;
  }

  interface Session {
    backendToken: string;
    error?: 'RefreshAccessTokenError' | null;
    user: {
      id: string;
      role: string | null;
      systemRole?: string | null;
      projectId: string | null;
      firstName: string;
      lastName: string;
    } & DefaultSession['user'];
  }
}

declare module 'next-auth/jwt' {
  interface JWT {
    backendToken: string;
    backendTokenExp: number;
    refreshToken?: string | null;
    error?: 'RefreshAccessTokenError' | null;
    role: string | null;
    systemRole?: string | null;
    projectId: string | null;
    firstName: string;
    lastName: string;
  }
}

