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
  }

  interface Session {
    backendToken: string;
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
    role: string | null;
    systemRole?: string | null;
    projectId: string | null;
    firstName: string;
    lastName: string;
  }
}

