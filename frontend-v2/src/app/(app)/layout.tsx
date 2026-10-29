import React from 'react';
import { AuthProvider } from '@/contexts/AuthContext';
import { AuthGate } from '@/features/auth';
import { QueryProvider } from '@/contexts/QueryProvider';

export interface AppGroupLayoutProps {
  children: React.ReactNode;
}

export default function AppGroupLayout({ children }: AppGroupLayoutProps) {
  return (
    <QueryProvider>
      <AuthProvider>
        <AuthGate>{children}</AuthGate>
      </AuthProvider>
    </QueryProvider>
  );
}
