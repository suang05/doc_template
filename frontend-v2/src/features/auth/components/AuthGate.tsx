'use client';

import React from 'react';
import { useAuthContext } from '@/contexts/AuthContext';
import { AppShell } from '@/components/layout/AppShell';
import { LoginView } from './LoginView';
import { SessionWarningModal } from './SessionWarningModal';

export interface AuthGateProps {
  children: React.ReactNode;
}

export function AuthGate({ children }: AuthGateProps) {
  const { isAuthenticated, isLoading, sessionExpiring, logout, extendSession } = useAuthContext();

  if (isLoading) {
    return (
      <div className="min-h-screen bg-canvas flex items-center justify-center">
        <div className="w-5 h-5 border-2 border-primary border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginView />;
  }

  return (
    <>
      {sessionExpiring && <SessionWarningModal onExtend={extendSession} onLogout={logout} />}
      <AppShell>{children}</AppShell>
    </>
  );
}
