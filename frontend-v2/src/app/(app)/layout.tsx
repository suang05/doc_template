import React from 'react';
import { AuthProvider } from '@/contexts/AuthContext';
import { AuthGate } from '@/components/features/auth/AuthGate';

export interface AppGroupLayoutProps {
  children: React.ReactNode;
}

export default function AppGroupLayout({ children }: AppGroupLayoutProps) {
  return (
    <AuthProvider>
      <AuthGate>{children}</AuthGate>
    </AuthProvider>
  );
}
