'use client';

import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useRef,
} from 'react';
import { SessionProvider, useSession, signIn, signOut } from 'next-auth/react';
import { ApiError } from '@/lib/api/client';
import type { LoginRequest } from '@/lib/api/types';
import { isSuperAdmin as checkSuperAdmin } from '@/lib/rbac';

const WARN_BEFORE_MS = 60_000; // warn 60s before expiry

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  systemRole: string | null;
  projectId: string | null;
}

export interface AuthContextValue {
  user: AuthUser | null;
  role: string | null;
  systemRole: string | null;
  isSuperAdmin: boolean;
  projectId: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  sessionExpiring: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  extendSession: () => Promise<void> | void;
  switchProject: (projectId: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function InnerAuthProvider({ children }: { children: React.ReactNode }) {
  const { data: session, status, update } = useSession();
  const [sessionExpiring, setSessionExpiring] = useState(false);
  const warnTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const expireTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimers = useCallback(() => {
    if (warnTimer.current) clearTimeout(warnTimer.current);
    if (expireTimer.current) clearTimeout(expireTimer.current);
  }, []);

  const logout = useCallback(() => {
    clearTimers();
    setSessionExpiring(false);
    void fetch('/api/auth/active-project', { method: 'DELETE' }).catch(() => undefined);
    void signOut({ redirect: false });
  }, [clearTimers]);

  // If token refresh fails on the backend, force logout immediately
  useEffect(() => {
    if (session?.error === 'RefreshAccessTokenError') {
      logout();
    }
  }, [session?.error, logout]);

  // (Re)schedule the expiry warning whenever the session's expiry changes.
  useEffect(() => {
    clearTimers();
    setSessionExpiring(false);

    if (!session?.expires) return;

    const msLeft = new Date(session.expires).getTime() - Date.now();
    if (msLeft <= 0) {
      logout();
      return;
    }

    const warnIn = msLeft - WARN_BEFORE_MS;
    if (warnIn > 0) {
      warnTimer.current = setTimeout(() => setSessionExpiring(true), warnIn);
    } else {
      setSessionExpiring(true);
    }
    expireTimer.current = setTimeout(() => logout(), msLeft);

    return clearTimers;
  }, [session?.expires, clearTimers, logout]);

  useEffect(() => {
    const onUnauthorized = () => logout();
    window.addEventListener('auth:unauthorized', onUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', onUnauthorized);
  }, [logout]);

  const login = useCallback(async (req: LoginRequest) => {
    const res = await signIn('credentials', {
      email: req.email,
      password: req.password,
      redirect: false,
    });

    if (!res || res.error) {
      throw new ApiError(401, 'อีเมลหรือรหัสผ่านไม่ถูกต้อง');
    }
  }, []);

  // Real Refresh Token Rotation via single-flight BFF endpoint + NextAuth update()
  const extendSession = useCallback(async () => {
    try {
      const refreshRes = await fetch('/api/auth/refresh', { method: 'POST' });
      if (!refreshRes.ok) {
        logout();
        return;
      }
      await update();
      setSessionExpiring(false);
    } catch {
      logout();
    }
  }, [update, logout]);

  const switchProject = useCallback(
    async (nextProjectId: string) => {
      const res = await fetch('/api/auth/active-project', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ projectId: nextProjectId }),
      });

      if (!res.ok) {
        throw new ApiError(res.status, 'ไม่สามารถเปลี่ยนโปรเจกต์ที่ใช้งานอยู่ได้');
      }

      await update();
    },
    [update],
  );

  const systemRole = session?.user.systemRole ?? null;
  const projectId = session?.user.projectId ?? null;

  const user: AuthUser | null = session?.user
    ? {
        id: session.user.id ?? '',
        email: session.user.email ?? '',
        firstName: session.user.firstName ?? '',
        lastName: session.user.lastName ?? '',
        role: session.user.role ?? 'Viewer',
        systemRole,
        projectId,
      }
    : null;

  return (
    <AuthContext.Provider
      value={{
        user,
        role: session?.user.role ?? null,
        systemRole,
        isSuperAdmin: checkSuperAdmin(systemRole),
        projectId,
        isAuthenticated: status === 'authenticated',
        isLoading: status === 'loading',
        sessionExpiring,
        login,
        logout,
        extendSession,
        switchProject,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  return (
    <SessionProvider>
      <InnerAuthProvider>{children}</InnerAuthProvider>
    </SessionProvider>
  );
}

export function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuthContext must be inside <AuthProvider>');
  return ctx;
}
