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
import type { LoginRequest, AuthUser } from '@/types/api';

const WARN_BEFORE_MS = 60_000; // warn 60s before expiry

export interface AuthContextValue {
  user: AuthUser | null;
  role: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  sessionExpiring: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  extendSession: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function InnerAuthProvider({ children }: { children: React.ReactNode }) {
  const { data: session, status } = useSession();
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
    void signOut({ redirect: false });
  }, [clearTimers]);

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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.expires]);

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

  // Phase 3: replace with an actual refresh-token call — today this only dismisses the
  // warning, the underlying backend JWT still expires at its original exp.
  const extendSession = useCallback(() => {
    setSessionExpiring(false);
  }, []);

  const user: AuthUser | null = session?.user
    ? {
        email: session.user.email ?? '',
        firstName: session.user.firstName,
        lastName: session.user.lastName,
      }
    : null;

  return (
    <AuthContext.Provider
      value={{
        user,
        role: session?.user.role ?? null,
        isAuthenticated: status === 'authenticated',
        isLoading: status === 'loading',
        sessionExpiring,
        login,
        logout,
        extendSession,
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
