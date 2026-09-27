'use client';

import React from 'react';
import { AuthProvider, useAuthContext } from '@/contexts/AuthContext';
import { AppShell } from '@/components/layout/AppShell';
import { LoginView } from '@/components/features/auth/LoginView';
import { SessionWarningModal } from '@/components/features/auth/SessionWarningModal';

function Gate({ children }: { children: React.ReactNode }) {
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

export default function AppGroupLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthProvider>
      <Gate>{children}</Gate>
    </AuthProvider>
  );
}
