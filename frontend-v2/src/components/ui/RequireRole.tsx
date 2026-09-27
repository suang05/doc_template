'use client';

import React from 'react';
import { ShieldOff } from 'lucide-react';
import { useAuthContext } from '@/contexts/AuthContext';
import { hasRole } from '@/lib/rbac';
import type { UserRole } from '@/types/api';

interface RequireRoleProps {
  /** Minimum role required — enforced against UserRole union */
  role: UserRole;
  children: React.ReactNode;
  /** Custom fallback — defaults to AccessDenied card */
  fallback?: React.ReactNode;
}

function AccessDenied() {
  return (
    <div className="flex h-full items-center justify-center pt-20">
      <div className="text-center space-y-3">
        <div className="inline-flex items-center justify-center w-10 h-10 rounded-full bg-red-50 border border-red-200">
          <ShieldOff className="w-5 h-5 text-red-500" />
        </div>
        <div className="space-y-1">
          <h3 className="text-sm font-semibold text-textPrimary">ไม่มีสิทธิ์เข้าถึง</h3>
          <p className="text-xs text-textMuted">บัญชีของคุณไม่มีสิทธิ์ดูหน้านี้</p>
        </div>
      </div>
    </div>
  );
}

export function RequireRole({ role, children, fallback }: RequireRoleProps) {
  const { role: userRole } = useAuthContext();

  if (!hasRole(userRole, role)) {
    return <>{fallback ?? <AccessDenied />}</>;
  }

  return <>{children}</>;
}
